using Catalog.Domain;
namespace Catalog.Application;
public interface IProductRepository { Product? Find(string id); }
public sealed class ProductQuery(IProductRepository repository)
{
    public Product? Execute(string id) => repository.Find(id);
}
