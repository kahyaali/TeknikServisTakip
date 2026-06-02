using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class MailSettingController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMailService _mailService;
        private readonly ILogService _logService;

        public MailSettingController(IUnitOfWork unitOfWork, IMailService mailService, ILogService logService)
        {
            _unitOfWork = unitOfWork;
            _mailService = mailService;
            _logService = logService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _unitOfWork.MailSettings.GetAllAsync();
            var activeSetting = settings.FirstOrDefault(s => s.IsActive) ?? new MailSetting();
            return View(activeSetting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(MailSetting model)
        {
            if (ModelState.IsValid)
            {
                var allSettings = await _unitOfWork.MailSettings.GetAllAsync();

                foreach (var setting in allSettings)
                {
                    setting.IsActive = false;
                    _unitOfWork.MailSettings.Update(setting);
                }

                if (model.Id == 0)
                {
                    model.IsActive = true;
                    await _unitOfWork.MailSettings.AddAsync(model);
                }
                else
                {
                    var existing = await _unitOfWork.MailSettings.GetByIdAsync(model.Id);
                    if (existing != null)
                    {
                        existing.SmtpServer = model.SmtpServer;
                        existing.Port = model.Port;
                        existing.SenderEmail = model.SenderEmail;
                        existing.SenderPassword = model.SenderPassword;
                        existing.UseSSL = model.UseSSL;
                        existing.IsActive = true;
                        _unitOfWork.MailSettings.Update(existing);
                    }
                }

                await _unitOfWork.CompleteAsync();
                // ========== İŞLEM LOGU ==========
                await _logService.LogAsync(
                    action: "MailSetting/Save",
                    actionType: "Update",
                    entityName: "MailSetting",
                    entityId: model.Id,
                    description: $"Mail ayarları güncellendi. SMTP: {model.SmtpServer}, Port: {model.Port}, Sender: {model.SenderEmail}",
                    oldValues: null,
                    newValues: new { model.SmtpServer, model.Port, model.SenderEmail, model.UseSSL }
                );

                TempData["Success"] = "Mail ayarları başarıyla kaydedildi!";
                return RedirectToAction("Index");
            }

            return View("Index", model);
        }

 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestMail(string testEmail)
        {
            if (string.IsNullOrEmpty(testEmail))
            {
                TempData["Error"] = "Lütfen test e-postası girin!";
                return RedirectToAction("Index");
            }

            var result = await _mailService.SendTestMailAsync(testEmail);
            if (result)
            {
                TempData["Success"] = $"Test maili {testEmail} adresine başarıyla gönderildi!";
            }
            else
            {
                TempData["Error"] = "Test maili gönderilemedi! Lütfen ayarlarınızı kontrol edin.";
            }

            return RedirectToAction("Index");
        }
    }
}