using CountryCurrencyApi.Data;
using CountryCurrencyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CountryCurrencyApi.Services;

public class CountryRefreshService(
    AppDbContext db,
    ExternalApiClient external,
    ImageGenerator imageGenerator,
    ILogger<CountryRefreshService> logger)
{
    private readonly Random _random = new();

    public async Task<(int totalSaved, DateTime lastRefreshedAt)> RefreshAllAsync(CancellationToken ct)
    {
        // Fetch external data first
        List<ExternalApiClient.RestCountry> countries;
        ExternalApiClient.ExchangeApiResponse ratesResp;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            countries = await external.FetchAllCountriesAsync(cts.Token);
            ratesResp = await external.FetchUsdRatesAsync(cts.Token);
        }
        catch (ExternalApiException ex)
        {
            // bubble up so controller returns 503
            logger.LogError(ex, "External API failure: {Api}", ex.ApiName);
            throw;
        }

        // We will apply updates in a transaction; if anything fails we rollback.
        using var tx = await db.Database.BeginTransactionAsync(ct);
        var lastRefreshedAt = DateTime.UtcNow;
        try
        {
            int count = 0;
            var rateMap = ratesResp.rates ?? new Dictionary<string, double>();

            foreach (var rc in countries)
            {
                var currencyCode = ExtractFirstCurrencyCode(rc.currencies);
                double? exchangeRate = null;
                double? estimatedGdp = null;

                if (currencyCode == null)
                {
                    // per spec: store currency_code null, exchange_rate null, estimated_gdp 0
                    estimatedGdp = 0;
                }
                else
                {
                    if (rateMap.TryGetValue(currencyCode, out var r))
                    {
                        exchangeRate = r;
                        // compute estimated_gdp
                        var mult = _random.Next(1000, 2001); // inclusive 2000
                                                             // per spec: population × random(1000–2000) ÷ exchange_rate.
                        estimatedGdp = rc.population * (double)mult / exchangeRate;
                    }
                    else
                    {
                        // currency not found => exchange_rate null, estimated_gdp null
                        exchangeRate = null;
                        estimatedGdp = null;
                    }
                }

                // Upsert by name (case-insensitive)
                var existing = await db.Countries
                    .Where(c => c.Name.ToLower() == rc.name.ToLower())
                    .FirstOrDefaultAsync(ct);

                if (existing != null)
                {
                    existing.Capital = rc.capital;
                    existing.Region = rc.region;
                    existing.Population = rc.population;
                    existing.CurrencyCode = currencyCode;
                    existing.ExchangeRate = exchangeRate;
                    existing.EstimatedGdp = estimatedGdp;
                    existing.FlagUrl = rc.flag;
                    existing.LastRefreshedAt = lastRefreshedAt;
                    db.Countries.Update(existing);
                }
                else
                {
                    var newC = new Country
                    {
                        Name = rc.name,
                        Capital = rc.capital,
                        Region = rc.region,
                        Population = rc.population,
                        CurrencyCode = currencyCode,
                        ExchangeRate = exchangeRate,
                        EstimatedGdp = estimatedGdp,
                        FlagUrl = rc.flag,
                        LastRefreshedAt = lastRefreshedAt
                    };
                    await db.Countries.AddAsync(newC, ct);
                }

                count++;
            }

            await db.SaveChangesAsync(ct);

            // commit transaction only after save succeeded
            await tx.CommitAsync(ct);

            // generate image after commit
            var allCountries = await db.Countries.AsNoTracking().ToListAsync(ct);
            Directory.CreateDirectory("cache");
            await imageGenerator.GenerateSummaryAsync(allCountries, lastRefreshedAt, "cache/summary.png", ct);

            return (count, lastRefreshedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh countries. Rolling back.");
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private string? ExtractFirstCurrencyCode(object[]? currencies)
    {
        if (currencies == null || currencies.Length == 0) return null;

        try
        {
            // currencies come as array of objects like { "code": "NGN", "name":"Nigerian naira", ... }
            var first = currencies[0];
            // Use serialization to get code
            var json = System.Text.Json.JsonSerializer.Serialize(first);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("code", out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var code = prop.GetString();
                return code;
            }
        }
        catch
        {
            // ignore, return null
        }

        return null;
    }
}
