using System.Text.RegularExpressions;
using Gateway.Application;
using Microsoft.AspNetCore.Mvc;
namespace Gateway.Api.Controllers;

[ApiController]
public sealed class ProductSummaryController(ProductSummaryService service, ILogger<ProductSummaryController> logger) : ControllerBase
{
    [HttpGet("/api/v1/products/{id}/summary")]
    [HttpGet("/api/v1/product-summary/{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!Regex.IsMatch(id, @"^P-[0-9]{3}$")) return Problem(statusCode: 400, title: "Identificador inválido", detail: "Usa el formato P-101.");
        try
        {
            var summary = await service.ExecuteAsync(id, ct);
            foreach (var observation in summary.Services.Where(s => s.ErrorCode is not null))
                logger.LogWarning("Dependencia {Service}: {Code}", observation.Service, observation.ErrorCode);
            return Ok(summary);
        }
        catch (DependencyException ex)
        {
            logger.LogWarning("Dependencia {Service}: {Code}", ex.Service, ex.Code);
            var status = ex.Code == "NOT_FOUND" ? 404 : ex.Code == "TIMEOUT" ? 504 : 502;
            return Problem(statusCode: status, title: status == 404 ? "Producto no encontrado" : "Catálogo no disponible",
                detail: status == 404 ? "Comprueba el identificador del producto." : "No se pudo consultar el producto. Intenta nuevamente.",
                extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
        }
    }
}
