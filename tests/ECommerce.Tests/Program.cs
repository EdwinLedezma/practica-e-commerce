using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Gateway.Application;
using Gateway.Domain;
using Gateway.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var passed=0;
void Check(bool value,string name) { if(!value) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); passed++; }
int Port() { var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();return port; }
var children=new List<Process>();
var logs=new System.Collections.Concurrent.ConcurrentQueue<string>();
Process Launch(string name,int port,Dictionary<string,string>? config=null)
{
    var psi=new ProcessStartInfo("dotnet") { WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };
    foreach(var arg in new[]{Path.Combine(root,$"src/{name}.Api/bin/Debug/net10.0/{name}.Api.dll"),"--urls",$"http://127.0.0.1:{port}","--contentRoot",Path.Combine(root,$"src/{name}.Api")}) psi.ArgumentList.Add(arg);
    psi.Environment["ASPNETCORE_ENVIRONMENT"]="Production";
    if(config is not null) foreach(var kv in config) psi.Environment[kv.Key]=kv.Value;
    var p=Process.Start(psi)!;children.Add(p);
    p.OutputDataReceived+=(_,e)=>{if(e.Data is not null)logs.Enqueue(e.Data);};
    p.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)logs.Enqueue(e.Data);};
    p.BeginOutputReadLine();p.BeginErrorReadLine();return p;
}
using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};
async Task Ready(int port)
{
    for(var i=0;i<100;i++){try{if((await http.GetAsync($"http://127.0.0.1:{port}/health")).IsSuccessStatusCode)return;}catch(HttpRequestException){}await Task.Delay(100);}
    throw new Exception("Servicio sin iniciar: "+port+Environment.NewLine+string.Join(Environment.NewLine,logs));
}
var mode="ok";
var catalogMode="broken";
var fakePort=Port();
var builder=WebApplication.CreateBuilder();builder.WebHost.UseUrls($"http://127.0.0.1:{fakePort}");builder.Logging.ClearProviders();
await using var fake=builder.Build();
fake.MapGet("/v2/rate/USD/MXN",async (HttpContext context)=>
{
    if(mode=="slow")await Task.Delay(1400,context.RequestAborted);
    if(mode=="broken"){context.Response.StatusCode=503;return;}
    context.Response.ContentType="application/json";
    await context.Response.WriteAsync(mode=="malformed"?"{broken":mode=="invalid"?"""{"date":"2026-01-01","base":"EUR","quote":"MXN","rate":-5}""":"""{"date":"2026-01-01","base":"USD","quote":"MXN","rate":17}""");
});
fake.MapGet("/microservices/products/{id}",async (HttpContext context)=>
{
    if(catalogMode=="broken"){context.Response.StatusCode=503;return;}
    await Task.Delay(1400,context.RequestAborted);
    await context.Response.WriteAsJsonAsync(new {id="P-101",name="Laptop",priceUsd=1200});
});
try
{
    Check(Pricing.ConvertToMxn(1200,17)==20400m,"conversión decimal USD/MXN");
    Check(Pricing.ConvertToMxn(1.005m,1)==1.01m,"redondeo monetario a dos decimales");
    try{_=new Catalog.Domain.Product("P-101","Producto","","Prueba",-1);throw new Exception("Aceptó precio negativo");}catch(ArgumentOutOfRangeException){Check(true,"dominio rechaza precio negativo");}
    try{_=new Inventory.Domain.Stock("P-101",-1,"A",1);throw new Exception("Aceptó stock negativo");}catch(ArgumentOutOfRangeException){Check(true,"dominio rechaza stock negativo");}
    var barrier=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var count=0;
    async Task Gate(){if(Interlocked.Increment(ref count)==3)barrier.SetResult();await barrier.Task.WaitAsync(TimeSpan.FromSeconds(2));}
    var service=new ProductSummaryService(new StubProducts(Gate),new StubStock(Gate),new StubRates(Gate));
    var concurrent=await service.ExecuteAsync("P-101",CancellationToken.None);
    Check(count==3 && concurrent.Status=="SUCCESS","las tres consultas empiezan antes de esperar resultados");
    using(var canceled=new CancellationTokenSource())
    {
        canceled.Cancel();
        var canceledService=new ProductSummaryService(new StubProducts(()=>Task.FromCanceled(canceled.Token)),new StubStock(()=>Task.CompletedTask),new StubRates(()=>Task.CompletedTask));
        try{await canceledService.ExecuteAsync("P-101",canceled.Token);throw new Exception("Cancelación absorbida");}catch(OperationCanceledException){Check(true,"cancelación del cliente se propaga");}
    }
    await fake.StartAsync();
    var catalogPort=Port();var inventoryPort=Port();var gatewayPort=Port();
    var catalog=Launch("Catalog",catalogPort);var inventory=Launch("Inventory",inventoryPort);
    var config=new Dictionary<string,string>{{"Services__CatalogUrl",$"http://127.0.0.1:{catalogPort}/"},{"Services__InventoryUrl",$"http://127.0.0.1:{inventoryPort}/"},{"Services__CurrencyUrl",$"http://127.0.0.1:{fakePort}/"},{"Services__TimeoutSeconds","0.7"}};
    Launch("Gateway",gatewayPort,config);
    await Task.WhenAll(Ready(catalogPort),Ready(inventoryPort),Ready(gatewayPort));
    var url=$"http://127.0.0.1:{gatewayPort}";
    async Task<(HttpStatusCode Code,JsonElement Data)> Query(string path="/api/v1/products/P-101/summary")
    {using var response=await http.GetAsync(url+path);return(response.StatusCode,JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone());}
    var product=await http.GetAsync($"http://127.0.0.1:{catalogPort}/microservices/products/P-101");Check(product.StatusCode==HttpStatusCode.OK,"catálogo HTTP independiente");
    var stock=await http.GetAsync($"http://127.0.0.1:{inventoryPort}/microservices/inventory/P-101");Check(stock.StatusCode==HttpStatusCode.OK,"inventario HTTP independiente");
    var ok=await Query();Check(ok.Code==HttpStatusCode.OK && ok.Data.GetProperty("price_mxn").GetDecimal()==20400 && ok.Data.GetProperty("available_units").GetInt32()==15 && ok.Data.GetProperty("status").GetString()=="SUCCESS","composite HTTP consolida contrato y conversión");
    Check((await Query("/api/v1/product-summary/P-101")).Code==HttpStatusCode.OK,"alias del endpoint");
    Check((await Query("/api/v1/products/P-999/summary")).Code==HttpStatusCode.NotFound,"producto inexistente devuelve 404");
    Check((await Query("/api/v1/products/invalid/summary")).Code==HttpStatusCode.BadRequest,"identificador inválido devuelve 400");
    var zero=await Query("/api/v1/products/P-103/summary");Check(zero.Data.GetProperty("stock_status").GetString()=="OUT_OF_STOCK" && zero.Data.GetProperty("estimated_delivery_days").ValueKind==JsonValueKind.Null,"sin stock no promete entrega");
    mode="broken";var partial=await Query();Check(partial.Code==HttpStatusCode.OK && partial.Data.GetProperty("status").GetString()=="PARTIAL" && partial.Data.GetProperty("price_mxn").ValueKind==JsonValueKind.Null,"divisas 503 produce respuesta parcial sin precio inventado");
    mode="malformed";Check((await Query()).Data.GetProperty("status").GetString()=="PARTIAL","JSON externo malformado se controla");
    mode="invalid";Check((await Query()).Data.GetProperty("exchange_rate").ValueKind==JsonValueKind.Null,"tasa externa inválida se rechaza");
    mode="slow";var watch=Stopwatch.StartNew();var slow=await Query();Check(slow.Data.GetProperty("status").GetString()=="PARTIAL" && watch.ElapsedMilliseconds<3000,"timeout externo limita la espera");
    mode="ok";Check((await Query()).Data.GetProperty("status").GetString()=="SUCCESS","recuperación después del fallo");
    inventory.Kill(true);await inventory.WaitForExitAsync();
    var missingStock=await Query();Check(missingStock.Data.GetProperty("stock_status").GetString()=="UNKNOWN" && missingStock.Data.GetProperty("available_units").ValueKind==JsonValueKind.Null,"inventario caído no se confunde con cero existencias");
    using var page=await http.GetAsync(url);Check(page.IsSuccessStatusCode && (await page.Content.ReadAsStringAsync()).Contains("Todo sobre tu producto"),"dashboard se sirve desde gateway");
    Check(page.Headers.Contains("Content-Security-Policy"),"cabeceras de seguridad del dashboard");
    var js=await http.GetStringAsync(url+"/app.js");Check(!js.Contains("innerHTML"),"interfaz usa textContent para datos externos");
    catalog.Kill(true);await catalog.WaitForExitAsync();
    var stoppedCatalog=await Query();
    Check(stoppedCatalog.Code is HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout,"catálogo caído devuelve error 502 o 504 según conexión");
    var badGatewayPort=Port();config["Services__CatalogUrl"]=$"http://127.0.0.1:{fakePort}/";
    Launch("Gateway",badGatewayPort,config);await Ready(badGatewayPort);
    using(var rejected=await http.GetAsync($"http://127.0.0.1:{badGatewayPort}/api/v1/products/P-101/summary"))
        Check(rejected.StatusCode==HttpStatusCode.BadGateway,"catálogo HTTP 503 se traduce a 502");
    catalogMode="slow";
    using(var timed=await http.GetAsync($"http://127.0.0.1:{badGatewayPort}/api/v1/products/P-101/summary"))
        Check(timed.StatusCode==HttpStatusCode.GatewayTimeout,"timeout del catálogo devuelve 504");
    // Additional wire-contract validation through the actual infrastructure client.
    using var badHttp=new HttpClient(new StaticHandler("""{"id":"WRONG","name":"Product","priceUsd":5}""")){BaseAddress=new Uri("http://stub/"),Timeout=TimeSpan.FromSeconds(1)};
    try{await new ProductHttpClient(badHttp).GetAsync("P-101",CancellationToken.None);throw new Exception("Aceptó producto ajeno");}catch(DependencyException ex){Check(ex.Code=="INVALID_RESPONSE","catálogo con identificador distinto se rechaza");}
    Console.WriteLine($"RESULT: {passed} verificaciones satisfactorias; 0 fallos.");
}
catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
finally{foreach(var child in children){if(!child.HasExited)child.Kill(true);child.Dispose();}await fake.StopAsync();}

sealed class StubProducts(Func<Task> gate):IProductClient { public async Task<ProductSnapshot> GetAsync(string id,CancellationToken ct){await gate();return new(id,"Laptop","Demo","Computación",1200);} }
sealed class StubStock(Func<Task> gate):IInventoryClient { public async Task<StockSnapshot> GetAsync(string id,CancellationToken ct){await gate();return new(id,15,"A",3);} }
sealed class StubRates(Func<Task> gate):IExchangeRateClient { public async Task<ExchangeRate> GetAsync(CancellationToken ct){await gate();return new(new DateOnly(2026,1,1),"USD","MXN",17);} }
sealed class StaticHandler(string json):HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")}); }
