using CountryCurrencyApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CountryCurrencyApi.Controllers;

[ApiController]
[Route("status")]
public class StatusController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var total = await db.Countries.CountAsync();
        var last = await db.Countries.OrderByDescending(c => c.LastRefreshedAt).Select(c => (DateTime?)c.LastRefreshedAt).FirstOrDefaultAsync();
        return Ok(new
        {
            total_countries = total,
            last_refreshed_at = last?.ToString("o")
        });
    }
}
