using Catalog.Application;
using Catalog.Domain;
namespace Catalog.Infrastructure;
public sealed class DemoProductRepository : IProductRepository
{
    private static readonly Dictionary<string, Product> Products = new(StringComparer.Ordinal)
    {
        ["P-101"] = new("P-101", "Laptop Gamer XYZ", "Potencia para crear, jugar y trabajar. 16 GB RAM, SSD de 1 TB y pantalla de 15.6 pulgadas.", "Computación", 1200m),
        ["P-102"] = new("P-102", "Audífonos Studio", "Audio envolvente, conexión inalámbrica y hasta 30 horas de batería.", "Audio", 85.50m),
        ["P-103"] = new("P-103", "Teclado Mecánico Pro", "Teclado compacto con interruptores táctiles y conexión USB-C.", "Accesorios", 59.99m)
    };
    public Product? Find(string id) => Products.GetValueOrDefault(id);
}
