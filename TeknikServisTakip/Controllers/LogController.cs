using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServisTakip.Models.ViewModels;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class LogController : Controller
    {
        private readonly ILogService _logService;

        public LogController(ILogService logService)
        {
            _logService = logService;
        }

        // Ana Log Listesi
        public async Task<IActionResult> Index()
        {
            var logs = await _logService.GetAllLogsAsync();
            return View(logs);
        }

        // Ürün Takip Logları
        public async Task<IActionResult> ProductTracking(int? repairId)
        {
            ViewBag.RepairId = repairId;
            var logs = await _logService.GetProductTrackingLogsAsync(repairId);
            return View(logs);
        }

        // Hata Logları
        public async Task<IActionResult> Errors(bool onlyUnresolved = false)
        {
            ViewBag.OnlyUnresolved = onlyUnresolved;
            var logs = await _logService.GetErrorLogsAsync(onlyUnresolved);
            return View(logs);
        }
        // Hata çözüldü işaretle

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveError(int id, string note)
        {
            await _logService.MarkErrorAsResolvedAsync(id, note);
            // ========== İŞLEM LOGU ==========
            await _logService.LogAsync(
                action: "Log/ResolveError",
                actionType: "Update",
                entityName: "ErrorLog",
                entityId: id,
                description: $"Hata çözüldü olarak işaretlendi. Not: {note}",
                oldValues: new { IsResolved = false },
                newValues: new { IsResolved = true, note }
            );
            TempData["Success"] = "Hata çözüldü olarak işaretlendi.";
            return RedirectToAction("Errors");
        }


        // Tek bir işlem logu sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLog(int id)
        {
            var log = await _logService.GetLogByIdAsync(id);
            if (log == null)
            {
                return Json(new { success = false, message = "Log bulunamadı!" });
            }

            await _logService.DeleteLogAsync(id);
            return Json(new { success = true, message = "Log silindi!" });
        }

        // Tek bir ürün takip logu sil
        [HttpPost]
        public async Task<IActionResult> DeleteProductTrackingLog(int id)
        {
            var log = await _logService.GetProductTrackingLogByIdAsync(id);
            if (log == null)
            {
                return Json(new { success = false, message = "Log bulunamadı!" });
            }

            await _logService.DeleteProductTrackingLogAsync(id);
            return Json(new { success = true, message = "Ürün takip logu silindi!" });
        }

        // Tek bir hata logu sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteErrorLog(int id)
        {
            var log = await _logService.GetErrorLogByIdAsync(id);
            if (log == null)
            {
                return Json(new { success = false, message = "Hata logu bulunamadı!" });
            }

            await _logService.DeleteErrorLogAsync(id);
            return Json(new { success = true, message = "Hata logu silindi!" });
        }

        // Toplu işlem logu sil (seçili olanlar)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedLogs(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                return Json(new { success = false, message = "Silmek için log seçin!" });
            }

            var idList = ids.Split(',').Select(int.Parse).ToList();
            int deletedCount = 0;

            foreach (var id in idList)
            {
                await _logService.DeleteLogAsync(id);
                deletedCount++;
            }

            return Json(new { success = true, message = $"{deletedCount} log silindi!" });
        }

        // Toplu ürün takip logu sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedProductTrackingLogs(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                return Json(new { success = false, message = "Silmek için log seçin!" });
            }

            var idList = ids.Split(',').Select(int.Parse).ToList();
            int deletedCount = 0;

            foreach (var id in idList)
            {
                await _logService.DeleteProductTrackingLogAsync(id);
                deletedCount++;
            }

            return Json(new { success = true, message = $"{deletedCount} ürün takip logu silindi!" });
        }

        // Toplu hata logu sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedErrorLogs(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                return Json(new { success = false, message = "Silmek için hata logu seçin!" });
            }

            var idList = ids.Split(',').Select(int.Parse).ToList();
            int deletedCount = 0;

            foreach (var id in idList)
            {
                await _logService.DeleteErrorLogAsync(id);
                deletedCount++;
            }

            return Json(new { success = true, message = $"{deletedCount} hata logu silindi!" });
        }

        // Tüm işlem loglarını sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllLogs()
        {
            var allLogs = await _logService.GetAllLogsAsync();
            int deletedCount = 0;

            foreach (var log in allLogs)
            {
                await _logService.DeleteLogAsync(log.Id);
                deletedCount++;
            }

            return Json(new { success = true, message = $"Tüm {deletedCount} işlem logu silindi!" });
        }

        // Tüm ürün takip loglarını sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllProductTrackingLogs()
        {
            var allLogs = await _logService.GetProductTrackingLogsAsync();
            int deletedCount = 0;

            foreach (var log in allLogs)
            {
                await _logService.DeleteProductTrackingLogAsync(log.Id);
                deletedCount++;
            }

            return Json(new { success = true, message = $"Tüm {deletedCount} ürün takip logu silindi!" });
        }

        // Tüm hata loglarını sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllErrorLogs()
        {
            var allLogs = await _logService.GetErrorLogsAsync();
            int deletedCount = 0;

            foreach (var log in allLogs)
            {
                await _logService.DeleteErrorLogAsync(log.Id);
                deletedCount++;
            }

            return Json(new { success = true, message = $"Tüm {deletedCount} hata logu silindi!" });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogClientError([FromBody] ClientErrorLogViewModel model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { success = false, message = "Model null" });
                }

                await _logService.LogAsync(
                    action: "ClientError",
                    actionType: "Error",
                    entityName: "Browser",
                    entityId: null,
                    description: $"JS Hatası: {model.Message} | Sayfa: {model.PageUrl} | Satır: {model.Line}",
                    oldValues: null,
                    newValues: new
                    {
                        model.Message,
                        model.Url,
                        model.Line,
                        model.Column,
                        model.Stack,
                        model.UserAgent,
                        model.PageUrl,
                        model.Timestamp
                    }
                );

                return Json(new { success = true, message = "Log kaydedildi" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}