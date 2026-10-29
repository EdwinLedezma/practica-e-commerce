using Inventory.Application;
using Inventory.Domain;
namespace Inventory.Infrastructure;
public sealed class DemoStockRepository : IStockRepository
{
    private static readonly Dictionary<string,Stock> Stocks = new(StringComparer.Ordinal)
    {
        ["P-101"] = new("P-101",15,"Chihuahua · A-12",3),
        ["P-102"] = new("P-102",4,"Chihuahua · B-03",2),
        ["P-103"] = new("P-103",0,"Chihuahua · C-07",5)
    };
    public Stock? Find(string productId) => Stocks.GetValueOrDefault(productId);
}
