using Inventory.Domain;
namespace Inventory.Application;

public interface IStockRepository { Stock? Find(string productId); }
public sealed class InventoryQuery(IStockRepository repository)
{
    public Stock? Execute(string productId) => repository.Find(productId);
}
