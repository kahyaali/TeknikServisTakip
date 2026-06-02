using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repositories.Abstract
{
    public interface IProductRepository: IGenericRepository<Product>
    {
        Task<Product?> GetProductWithMovementsAsync(int id);
        Task<List<Product>> GetLowStockProductsAsync(int threshold = 5);
        Task<List<Product>> GetCriticalStockProductsAsync();
        Task<string> GenerateProductCodeAsync();
        Task<bool> IsSerialNoExistsAsync(string serialNo, int? excludeId = null);
        Task<bool> IsIMEINoExistsAsync(string imeiNo, int? excludeId = null);
        Task<(ICollection<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchTerm = null);
    }
}
