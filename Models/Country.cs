using System.ComponentModel.DataAnnotations;

namespace CountryCurrencyApi.Models;

public class Country
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    public string? Capital { get; set; }

    public string? Region { get; set; }

    [Required]
    public long Population { get; set; }

    // ISO currency code, e.g. USD, NGN. May be null if country has none.
    public string? CurrencyCode { get; set; }

    // exchange rate relative to USD from exchange API (value: e.g. 1600.23)
    public double? ExchangeRate { get; set; }

    // computed: population × random(1000–2000) ÷ exchange_rate
    public double? EstimatedGdp { get; set; }

    public string? FlagUrl { get; set; }

    // auto set when inserted/updated
    public DateTime LastRefreshedAt { get; set; }
}
