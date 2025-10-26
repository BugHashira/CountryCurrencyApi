namespace CountryCurrencyApi.Services;

public class ExternalApiClient(HttpClient http, ILogger<ExternalApiClient> logger)
{
    public record RestCountryCurrency(string code, string name);
    public record RestCountry(
        string name,
        string? capital,
        string? region,
        long population,
        string flag,
        object[]? currencies // from API
    );

    public async Task<List<RestCountry>> FetchAllCountriesAsync(CancellationToken ct)
    {
        var url = "https://restcountries.com/v2/all?fields=name,capital,region,population,flag,currencies";
        try
        {
            var res = await http.GetFromJsonAsync<List<RestCountry>>(url, ct);
            return res ?? new List<RestCountry>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch countries from restcountries");
            throw new ExternalApiException("Countries API", ex);
        }
    }

    public record ExchangeApiResponse(string result, string base_code, Dictionary<string, double>? rates);

    public async Task<ExchangeApiResponse> FetchUsdRatesAsync(CancellationToken ct)
    {
        var url = "https://open.er-api.com/v6/latest/USD";
        try
        {
            var res = await http.GetFromJsonAsync<ExchangeApiResponse>(url, ct);
            if (res == null) throw new Exception("Empty response");
            return res;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch exchange rates");
            throw new ExternalApiException("Exchange API", ex);
        }
    }
}

public class ExternalApiException(string apiName, Exception inner) : Exception($"External API error: {apiName}", inner)
{
    public string ApiName { get; } = apiName;
}
