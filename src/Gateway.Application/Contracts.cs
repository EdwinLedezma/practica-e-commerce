using Gateway.Domain;
namespace Gateway.Application;
public interface IProductClient { Task<ProductSnapshot> GetAsync(string id, CancellationToken ct); }
public interface IInventoryClient { Task<StockSnapshot> GetAsync(string id, CancellationToken ct); }
public interface IExchangeRateClient { Task<ExchangeRate> GetAsync(CancellationToken ct); }
public sealed class DependencyException(string service, string code) : Exception($"{service}: {code}")
{
    public string Service { get; } = service;
    public string Code { get; } = code;
}
public sealed record ServiceObservation(string Service, string Status, long DurationMs, string? ErrorCode);
public sealed record ProductSummary(
    string ProductId, string Title, string Description, string Category,
    decimal PriceUsd, decimal? PriceMxn, string StockStatus, int? AvailableUnits,
    string? Warehouse, int? EstimatedDeliveryDays,
    string? CurrencyConversionSource, decimal? ExchangeRate, DateOnly? ExchangeRateDate,
    string Status, IReadOnlyList<ServiceObservation> Services);
