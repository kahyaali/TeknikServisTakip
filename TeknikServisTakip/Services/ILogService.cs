using Entities.Concrete;

namespace TeknikServisTakip.Services
{
    public interface ILogService
    {
        // Ana log metodu
        Task LogAsync(string action, string actionType, string entityName, int? entityId,
                      string description, object oldValues = null, object newValues = null);

        // Ürün takip logu
        Task LogProductTrackingAsync(int repairId, string action, string oldStatus, string newStatus, string description);

        // Hata logu
        Task LogErrorAsync(Exception ex, string controller, string action, HttpContext httpContext);

        // Logları getirme
        Task<IEnumerable<Log>> GetAllLogsAsync();
        Task<IEnumerable<Log>> GetUserLogsAsync(string userId);
        Task<IEnumerable<ProductTrackingLog>> GetProductTrackingLogsAsync(int? repairId = null);
        Task<IEnumerable<ErrorLog>> GetErrorLogsAsync(bool onlyUnresolved = false);

        // Log detayı
        Task<Log> GetLogByIdAsync(int id);
        Task<ProductTrackingLog> GetProductTrackingLogByIdAsync(int id);
        Task<ErrorLog> GetErrorLogByIdAsync(int id);

        // Hata çözüldü işaretleme
        Task MarkErrorAsResolvedAsync(int errorId, string note);

        // Silme metodları
        Task DeleteLogAsync(int id);
        Task DeleteProductTrackingLogAsync(int id);
        Task DeleteErrorLogAsync(int id);
    }
}