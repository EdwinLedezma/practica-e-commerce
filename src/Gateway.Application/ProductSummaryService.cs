using System.Diagnostics;
using Gateway.Domain;
namespace Gateway.Application;

public sealed class ProductSummaryService(IProductClient products, IInventoryClient inventory, IExchangeRateClient rates)
{
    public async Task<ProductSummary> ExecuteAsync(string id, CancellationToken ct)
    {
        // Start all three I/O operations before awaiting any result.
        var productTask = Observe("catalog", () => products.GetAsync(id, ct));
        var stockTask = Observe("inventory", () => inventory.GetAsync(id, ct));
        var rateTask = Observe("currency", () => rates.GetAsync(ct));
        await Task.WhenAll(productTask, stockTask, rateTask);
        var product = await productTask;
        if (product.Error is not null) throw product.Error;
        var stock = await stockTask;
        var rate = await rateTask;
        var p = product.Value!; var s = stock.Value; var r = rate.Value;
        return new(p.Id, p.Name, p.Description, p.Category, p.PriceUsd,
            r is null ? null : Pricing.ConvertToMxn(p.PriceUsd, r.Rate),
            s is null ? "UNKNOWN" : s.AvailableUnits > 0 ? "IN_STOCK" : "OUT_OF_STOCK",
            s?.AvailableUnits, s?.Warehouse, s?.AvailableUnits > 0 ? s.EstimatedDeliveryDays : null,
            r is null ? null : "Frankfurter External API", r?.Rate, r?.Date,
            s is null || r is null ? "PARTIAL" : "SUCCESS",
            [product.Observation, stock.Observation, rate.Observation]);
    }
    private static async Task<Observed<T>> Observe<T>(string service, Func<Task<T>> action) where T : class
    {
        var timer = Stopwatch.StartNew();
        try { return new(await action(), null, new(service, "SUCCESS", timer.ElapsedMilliseconds, null)); }
        catch (DependencyException ex) { return new(null, ex, new(service, "UNAVAILABLE", timer.ElapsedMilliseconds, ex.Code)); }
        // Request cancellation and unexpected programming errors propagate to the host.
    }
    private sealed record Observed<T>(T? Value, DependencyException? Error, ServiceObservation Observation);
}
