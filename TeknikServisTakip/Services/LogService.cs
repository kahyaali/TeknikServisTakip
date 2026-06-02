using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using System.Security.Claims;

namespace TeknikServisTakip.Services
{
 
    public class LogService : ILogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<AppUser> _userManager;

        public LogService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        private async Task<(string userId, string userName, string userEmail, string userRole)> GetUserInfo()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = httpContext.User.Identity.Name;
                var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
                var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userRole) && !string.IsNullOrEmpty(userId))
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        userRole = roles.FirstOrDefault();
                    }
                }

                return (userId, userName, userEmail, userRole);
            }
            return (null, "Anonim", null, null);
        }
        public async Task LogAsync(string action, string actionType, string entityName, int? entityId,
                                string description, object oldValues = null, object newValues = null)
        {
            try
            {
                var (userId, userName, userEmail, userRole) = await GetUserInfo();
                var httpContext = _httpContextAccessor.HttpContext;

                // NULL olabilecek değerleri kontrol et
                var log = new Log
                {
                    UserId = userId ?? "unknown",
                    UserName = userName ?? "unknown",
                    UserEmail = userEmail ?? "unknown",
                    UserRole = userRole ?? "unknown",
                    IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown",
                    Action = action ?? "unknown",
                    ActionType = actionType ?? "unknown",
                    EntityName = entityName ?? "unknown",
                    EntityId = entityId,
                    Description = description ?? "unknown",
                    OldValues = oldValues != null ? JsonConvert.SerializeObject(oldValues) : "",
                    NewValues = newValues != null ? JsonConvert.SerializeObject(newValues) : "",
                    RequestMethod = httpContext?.Request?.Method ?? "unknown",
                    RequestUrl = httpContext?.Request?.Path.ToString() ?? "unknown",
                    UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "unknown",
                    CreatedAt = DateTime.Now,
                    IsSuccess = true,
                    ErrorMessage = ""
                };

                await _unitOfWork.Logs.AddAsync(log);
                await _unitOfWork.CompleteAsync();

                Console.WriteLine($"✅ LOG KAYDEDİLDİ: {action}");
            }
            catch (Exception ex)
            {
                // Hata olursa dosyaya yaz
                var errorLog = $"{DateTime.Now} - LOG HATASI: {ex.Message} - INNER: {ex.InnerException?.Message}";
                System.IO.File.AppendAllText("logs_error.txt", errorLog + Environment.NewLine);
                Console.WriteLine($"❌ LOG HATASI: {ex.Message}");
            }
        }

        public async Task LogProductTrackingAsync(int repairId, string action, string oldStatus, string newStatus, string description)
        {
            try
            {
                var (userId, userName, userEmail, userRole) = await GetUserInfo();
                var repair = await _unitOfWork.RepairItems.GetByIdWithIncludeAsync(repairId, r => r.AppUser);

                if (repair != null)
                {
                    var trackingLog = new ProductTrackingLog
                    {
                        RepairItemId = repairId,
                        TrackingCode = repair.TrackingCode ?? "",
                        ProductName = repair.ProductName ?? "",
                        CustomerNumber = repair.CustomerNumber ?? "",
                        CustomerEmail = repair.AppUser?.Email ?? "Bilinmiyor", 
                        OldStatus = oldStatus ?? "",
                        NewStatus = newStatus ?? "",
                        Action = action ?? "",
                        PerformedBy = userName ?? "Sistem",
                        PerformedById = userId,
                        Description = description ?? "",
                        CreatedAt = DateTime.Now
                    };

                    await _unitOfWork.ProductTrackingLogs.AddAsync(trackingLog);
                    await _unitOfWork.CompleteAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ürün takip log kaydı sırasında hata: {ex.Message}");
                System.IO.File.AppendAllText("logs_error.txt", $"{DateTime.Now} - ProductTrackingLog Hatası: {ex.Message}\n");
            }
        }

        public async Task LogErrorAsync(Exception ex, string controller, string action, HttpContext httpContext)
        {
            try
            {
                var userId = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = httpContext?.User?.Identity?.Name;
                var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

                var errorLog = new ErrorLog
                {
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerException = ex.InnerException?.ToString(),
                    Controller = controller,
                    Action = action,
                    RequestUrl = httpContext?.Request?.Path + httpContext?.Request?.QueryString,
                    RequestMethod = httpContext?.Request?.Method,
                    UserId = userId,
                    UserEmail = userEmail,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.Now,
                    IsResolved = false
                };

                await _unitOfWork.ErrorLogs.AddAsync(errorLog);
                await _unitOfWork.CompleteAsync();
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"Hata logu kaydedilirken hata: {logEx.Message}");
            }
        }

        public async Task<IEnumerable<Log>> GetAllLogsAsync()
        {
            return await _unitOfWork.Logs.GetAllAsync();
        }

        public async Task<IEnumerable<Log>> GetUserLogsAsync(string userId)
        {
            var logs = await _unitOfWork.Logs.GetAllAsync();
            return logs.Where(l => l.UserId == userId).OrderByDescending(l => l.CreatedAt);
        }

        public async Task<IEnumerable<ProductTrackingLog>> GetProductTrackingLogsAsync(int? repairId = null)
        {
            var logs = await _unitOfWork.ProductTrackingLogs.GetAllAsync();
            if (repairId.HasValue)
            {
                logs = logs.Where(l => l.RepairItemId == repairId.Value);
            }
            return logs.OrderByDescending(l => l.CreatedAt);
        }

        public async Task<IEnumerable<ErrorLog>> GetErrorLogsAsync(bool onlyUnresolved = false)
        {
            var logs = await _unitOfWork.ErrorLogs.GetAllAsync();
            if (onlyUnresolved)
            {
                logs = logs.Where(l => !l.IsResolved);
            }
            return logs.OrderByDescending(l => l.CreatedAt);
        }

        public async Task<Log> GetLogByIdAsync(int id)
        {
            return await _unitOfWork.Logs.GetByIdAsync(id);
        }

        public async Task<ProductTrackingLog> GetProductTrackingLogByIdAsync(int id)
        {
            return await _unitOfWork.ProductTrackingLogs.GetByIdAsync(id);
        }

        public async Task<ErrorLog> GetErrorLogByIdAsync(int id)
        {
            return await _unitOfWork.ErrorLogs.GetByIdAsync(id);
        }

        public async Task MarkErrorAsResolvedAsync(int errorId, string note)
        {
            var error = await _unitOfWork.ErrorLogs.GetByIdAsync(errorId);
            if (error != null)
            {
                error.IsResolved = true;
                error.ResolvedNote = note;
                error.ResolvedAt = DateTime.Now;
                _unitOfWork.ErrorLogs.Update(error);
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task DeleteLogAsync(int id)
        {
            var log = await _unitOfWork.Logs.GetByIdAsync(id);
            if (log != null)
            {
                _unitOfWork.Logs.Delete(log);
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task DeleteProductTrackingLogAsync(int id)
        {
            var log = await _unitOfWork.ProductTrackingLogs.GetByIdAsync(id);
            if (log != null)
            {
                _unitOfWork.ProductTrackingLogs.Delete(log);
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task DeleteErrorLogAsync(int id)
        {
            var log = await _unitOfWork.ErrorLogs.GetByIdAsync(id);
            if (log != null)
            {
                _unitOfWork.ErrorLogs.Delete(log);
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}