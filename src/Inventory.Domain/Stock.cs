namespace Inventory.Domain;

public sealed record Stock
{
    public string ProductId { get; }
    public int AvailableUnits { get; }
    public string Warehouse { get; }
    public int EstimatedDeliveryDays { get; }
    public Stock(string productId, int availableUnits, string warehouse, int estimatedDeliveryDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(warehouse);
        if (availableUnits < 0) throw new ArgumentOutOfRangeException(nameof(availableUnits));
        if (estimatedDeliveryDays < 0) throw new ArgumentOutOfRangeException(nameof(estimatedDeliveryDays));
        ProductId = productId; AvailableUnits = availableUnits; Warehouse = warehouse; EstimatedDeliveryDays = estimatedDeliveryDays;
    }
}
