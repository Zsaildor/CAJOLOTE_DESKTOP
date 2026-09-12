using Cajolote.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cajolote.Services
{
    public interface ICatalogCacheService
    {
        Task InitializeAsync();
        IEnumerable<Product> GetAllProducts();
        IEnumerable<Product> GetQuickProducts();
        Product? FindProduct(string query);
        IEnumerable<Product> SearchProducts(string query);
        void UpdateProductPrice(int productId, decimal newPrice);
        void AddProduct(Product product);
        void RemoveProduct(int productId);
        void RefreshCache();
    }
}
