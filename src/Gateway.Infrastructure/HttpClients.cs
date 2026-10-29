using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Application;
using Gateway.Domain;
namespace Gateway.Infrastructure;
internal static class JsonTransport
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };
    public static async Task<T> GetAsync<T>(HttpClient http, string path, string service, CancellationToken ct)
    {
        try
        {
            using var response=await http.GetAsync(path,HttpCompletionOption.ResponseHeadersRead,ct);
            if (response.StatusCode==HttpStatusCode.NotFound) throw new DependencyException(service,"NOT_FOUND");
            if (!response.IsSuccessStatusCode) throw new DependencyException(service,"UPSTREAM_ERROR");
            return await response.Content.ReadFromJsonAsync<T>(Options,ct) ?? throw new DependencyException(service,"INVALID_RESPONSE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new DependencyException(service,"TIMEOUT"); }
        catch (HttpRequestException) { throw new DependencyException(service,"UNREACHABLE"); }
        catch (JsonException) { throw new DependencyException(service,"INVALID_RESPONSE"); }
        catch (NotSupportedException) { throw new DependencyException(service,"INVALID_RESPONSE"); }
    }
}
public sealed class ProductHttpClient(HttpClient http):IProductClient
{
    public async Task<ProductSnapshot> GetAsync(string id,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(http.Timeout);
        ProductSnapshot p;
        try { p=await JsonTransport.GetAsync<ProductSnapshot>(http,$"microservices/products/{Uri.EscapeDataString(id)}","catalog",deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new DependencyException("catalog","TIMEOUT"); }
        if (p.Id!=id || string.IsNullOrWhiteSpace(p.Name) || p.PriceUsd<0 || p.PriceUsd>1_000_000)
            throw new DependencyException("catalog","INVALID_RESPONSE");
        return p;
    }
}
public sealed class InventoryHttpClient(HttpClient http):IInventoryClient
{
    public async Task<StockSnapshot> GetAsync(string id,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(http.Timeout);
        StockSnapshot s;
        try { s=await JsonTransport.GetAsync<StockSnapshot>(http,$"microservices/inventory/{Uri.EscapeDataString(id)}","inventory",deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new DependencyException("inventory","TIMEOUT"); }
        if(s.ProductId!=id || s.AvailableUnits<0 || s.EstimatedDeliveryDays<0 || string.IsNullOrWhiteSpace(s.Warehouse))
            throw new DependencyException("inventory","INVALID_RESPONSE");
        return s;
    }
}
public sealed class FrankfurterHttpClient(HttpClient http):IExchangeRateClient
{
    public async Task<ExchangeRate> GetAsync(CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(http.Timeout);
        ExchangeRate r;
        try { r=await JsonTransport.GetAsync<ExchangeRate>(http,"v2/rate/USD/MXN","currency",deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new DependencyException("currency","TIMEOUT"); }
        if(r.Base!="USD" || r.Quote!="MXN" || r.Rate<=0 || r.Rate>100_000 || r.Date==default || r.Date> DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DependencyException("currency","INVALID_RESPONSE");
        return r;
    }
}
