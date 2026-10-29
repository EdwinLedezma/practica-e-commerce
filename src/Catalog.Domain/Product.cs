namespace Catalog.Domain;
public sealed record Product
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Category { get; }
    public decimal PriceUsd { get; }
    public Product(string id, string name, string description, string category, decimal priceUsd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (priceUsd < 0 || priceUsd > 1_000_000) throw new ArgumentOutOfRangeException(nameof(priceUsd));
        Id=id; Name=name; Description=description; Category=category; PriceUsd=priceUsd;
    }
}
