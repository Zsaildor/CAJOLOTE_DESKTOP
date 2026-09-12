using Cajolote.Models;
using Cajolote.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cajolote.Services
{
    public class CatalogCacheService : ICatalogCacheService
    {
        private readonly IServiceProvider _serviceProvider;
        private ConcurrentDictionary<int, Product> _productsCache = new();
        private bool _isInitialized = false;

        public CatalogCacheService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private Product CloneDetached(Product product)
        {
            return new Product
            {
                Id = product.Id,
                Barcode = product.Barcode,
                Name = product.Name,
                Price = product.Price,
                CategoryId = product.CategoryId,
                IsQuickProduct = product.IsQuickProduct,
                IsBulk = product.IsBulk,
                IconKey = product.IconKey,
                Category = product.Category != null 
                    ? new Category { Id = product.Category.Id, Name = product.Category.Name } 
                    : null!
            };
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CajoloteDbContext>();
            var products = await context.Products.Include(p => p.Category).AsNoTracking().ToListAsync();

            _productsCache.Clear();
            foreach (var product in products)
            {
                _productsCache.TryAdd(product.Id, CloneDetached(product));
            }
            
            _isInitialized = true;
        }

        public IEnumerable<Product> GetAllProducts()
        {
            return _productsCache.Values;
        }

        public IEnumerable<Product> GetQuickProducts()
        {
            return _productsCache.Values.Where(p => string.IsNullOrWhiteSpace(p.Barcode) || p.IsQuickProduct).ToList();
        }

        public Product? FindProduct(string query)
        {
            return _productsCache.Values.FirstOrDefault(p => 
                string.Equals(p.Barcode, query, StringComparison.OrdinalIgnoreCase) || 
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<Product> SearchProducts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<Product>();

            return _productsCache.Values.Where(p => 
                (p.Barcode != null && p.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase)) || 
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void UpdateProductPrice(int productId, decimal newPrice)
        {
            if (_productsCache.TryGetValue(productId, out var product))
            {
                product.Price = newPrice;
            }
        }

        public void AddProduct(Product product)
        {
            var detached = CloneDetached(product);
            _productsCache.AddOrUpdate(detached.Id, detached, (id, oldProduct) => detached);
        }

        public void RemoveProduct(int productId)
        {
            _productsCache.TryRemove(productId, out _);
        }

        public void RefreshCache()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CajoloteDbContext>();
            var products = context.Products.Include(p => p.Category).AsNoTracking().ToList();

            _productsCache.Clear();
            foreach (var product in products)
            {
                _productsCache.TryAdd(product.Id, CloneDetached(product));
            }
        }
    }
}
