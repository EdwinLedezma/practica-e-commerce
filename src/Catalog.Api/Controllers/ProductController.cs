using Microsoft.AspNetCore.Mvc;
using Catalog.Application;
namespace Catalog.Api.Controllers;
[ApiController]
[Route("microservices/products")]
public sealed class ProductController(ProductQuery query) : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^P-[0-9]{3}$"))
            return Problem(statusCode:400, title:"Identificador inválido", detail:"Usa el formato P-101.");
        var item=query.Execute(id);
        return item is null ? Problem(statusCode:404,title:"Recurso no encontrado") : Ok(item);
    }
}
