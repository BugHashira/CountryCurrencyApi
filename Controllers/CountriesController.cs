using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CountryCurrencyApi.Controllers;

[ApiController]
[Route("[controller]")]
public class CountriesController(AppDbContext db, CountryRefreshService refresher, ILogger<CountriesController> logger) : ControllerBase
{
    // POST /countries/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        try
        {
            var (count, lastRefreshedAt) = await refresher.RefreshAllAsync(ct);
            return Ok(new
            {
                message = "Refresh successful",
                total = count,
                last_refreshed_at = lastRefreshedAt.ToString("o")
            });
        }
        catch (ExternalApiException ex)
        {
            return StatusCode(503, new { error = "External data source unavailable", details = $"Could not fetch data from {ex.ApiName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during refresh");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // GET /countries
    // filters: ?region=Africa | ?currency=NGN | ?sort=gdp_desc
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? region, [FromQuery] string? currency, [FromQuery] string? sort)
    {
        var query = db.Countries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(c => c.Region != null && c.Region.ToLower() == region.ToLower());

        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(c => c.CurrencyCode != null && c.CurrencyCode.ToLower() == currency.ToLower());

        if (!string.IsNullOrWhiteSpace(sort))
        {
            if (sort.Equals("gdp_desc", StringComparison.OrdinalIgnoreCase))
                query = query.OrderByDescending(c => c.EstimatedGdp);
            else if (sort.Equals("gdp_asc", StringComparison.OrdinalIgnoreCase))
                query = query.OrderBy(c => c.EstimatedGdp);
        }

        var list = await query.Select(c => new {
            c.Id,
            c.Name,
            c.Capital,
            c.Region,
            c.Population,
            c.CurrencyCode,
            c.ExchangeRate,
            c.EstimatedGdp,
            c.FlagUrl,
            last_refreshed_at = c.LastRefreshedAt.ToString("o")
        }).ToListAsync();

        return Ok(list);
    }

    // GET /countries/{name}
    [HttpGet("{name}")]
    public async Task<IActionResult> GetByName(string name)
    {
        var c = await db.Countries.Where(x => x.Name.ToLower() == name.ToLower())
            .Select(c => new {
                c.Id,
                c.Name,
                c.Capital,
                c.Region,
                c.Population,
                c.CurrencyCode,
                c.ExchangeRate,
                c.EstimatedGdp,
                c.FlagUrl,
                last_refreshed_at = c.LastRefreshedAt.ToString("o")
            }).FirstOrDefaultAsync();

        if (c == null) return NotFound(new { error = "Country not found" });
        return Ok(c);
    }

    // DELETE /countries/{name}
    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name)
    {
        var existing = await db.Countries.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        if (existing == null) return NotFound(new { error = "Country not found" });

        db.Countries.Remove(existing);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /countries/image
    [HttpGet("image")]
    public IActionResult GetImage()
    {
        var path = "cache/summary.png";
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { error = "Summary image not found" });
        }
        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, "image/png");
    }
}
