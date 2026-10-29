using System.Text.Json;
using Gateway.Application;
using Gateway.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddProblemDetails();
var timeout = builder.Configuration.GetValue<double?>("Services:TimeoutSeconds") ?? 4;
if (timeout < 0.1 || timeout > 30) throw new InvalidOperationException("Services:TimeoutSeconds debe estar entre 0.1 y 30.");
void Configure(HttpClient http, string key, string fallback)
{
    var address = new Uri(builder.Configuration[$"Services:{key}"] ?? fallback);
    if (address.Scheme != "http" && address.Scheme != "https") throw new InvalidOperationException("URL de servicio inválida.");
    http.BaseAddress = address; http.Timeout = TimeSpan.FromSeconds(timeout);
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}
builder.Services.AddHttpClient<IProductClient, ProductHttpClient>(h => Configure(h, "CatalogUrl", "http://localhost:5101/"));
builder.Services.AddHttpClient<IInventoryClient, InventoryHttpClient>(h => Configure(h, "InventoryUrl", "http://localhost:5102/"));
builder.Services.AddHttpClient<IExchangeRateClient, FrankfurterHttpClient>(h => Configure(h, "CurrencyUrl", "https://api.frankfurter.dev/"));
builder.Services.AddScoped<ProductSummaryService>();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));
app.Run();
