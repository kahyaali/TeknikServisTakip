using DataAccess.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using TeknikServisTakip.Business.Abstract;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class MailLogController : Controller
    {
        private readonly IMailService _mailService;
        private readonly IUnitOfWork _unitOfWork;
        public MailLogController(IMailService mailService, IUnitOfWork unitOfWork)
        {
            _mailService = mailService;
            _unitOfWork = unitOfWork;
        }
        public async Task<IActionResult> Index(string? mailType = null)
        {
            ViewBag.MailType = mailType;
            var logs = await _mailService.GetMailLogsAsync(200, mailType);
            return View(logs);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMailLog(int id)
        {
            var log = await _mailService.GetMailLogByIdAsync(id);
            if (log == null)
                return Json(new { success = false, message = "Kayıt bulunamadı!" });

            _unitOfWork.MailLogs.Delete(log);
            await _unitOfWork.CompleteAsync();

            return Json(new { success = true, message = "Mail log silindi!" });
        }
    }
}
