using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IProductService
    {
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByIdWithMovementsAsync(int id);
        Task<IEnumerable<Product>> GetAllAsync();
        Task<(ICollection<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchTerm = null);

        Task<Product> AddAsync(Product product, string userId);
        Task<Product> UpdateAsync(Product product, string userId);
        Task<bool> DeleteAsync(int id, string userId);
        Task<bool> SoftDeleteAsync(int id, string userId);

     

        // Stok Yönetimi
        Task<(bool Success, string Message, int NewStock)> StockInAsync(int productId, int quantity, string referenceNo, string description, string userId);
        Task<(bool Success, string Message, int NewStock)> StockOutAsync(int productId, int quantity, string referenceNo, string description, string userId);

        // Validasyon
        Task<bool> IsSerialNoExistsAsync(string serialNo, int? excludeId = null);
        Task<bool> IsIMEINoExistsAsync(string imeiNo, int? excludeId = null);

        // Stok Uyarıları
        Task<List<Product>> GetLowStockProductsAsync();
        Task<List<Product>> GetCriticalStockProductsAsync();
        Task SendStockAlertsAsync();

        // Bulk Import
        Task<(int SuccessCount, List<string> Errors)> BulkImportAsync(List<Product> products, string userId);

        // Stok durumuna göre CSS sınıfı döndürür
        string GetStockStatusClass(int quantity, int minStockLevel, int maxStockLevel);
    }
}
