namespace Gateway.Domain;
public sealed record ProductSnapshot(string Id, string Name, string Description, string Category, decimal PriceUsd);
public sealed record StockSnapshot(string ProductId, int AvailableUnits, string Warehouse, int EstimatedDeliveryDays);
public sealed record ExchangeRate(DateOnly Date, string Base, string Quote, decimal Rate);
public static class Pricing
{
    public static decimal ConvertToMxn(decimal priceUsd, decimal rate)
    {
        if (priceUsd < 0 || priceUsd > 1_000_000) throw new ArgumentOutOfRangeException(nameof(priceUsd));
        if (rate <= 0 || rate > 100_000) throw new ArgumentOutOfRangeException(nameof(rate));
        return decimal.Round(priceUsd * rate, 2, MidpointRounding.AwayFromZero);
    }
}
