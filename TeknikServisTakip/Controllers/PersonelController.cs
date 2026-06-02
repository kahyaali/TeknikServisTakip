using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Models.ViewModels;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class PersonelController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMailService _mailService;

        public PersonelController(IUnitOfWork unitOfWork, ILogService logService, UserManager<AppUser> userManager, IMailService mailService) 
        { _unitOfWork = unitOfWork; _logService = logService; _userManager = userManager; _mailService = mailService; }

        // Perosonel Listesi
        public async Task<IActionResult> Index()
        {
       
            var personels = await _unitOfWork.Personels.GetAllAsync(p => p.Position, p => p.Department);

            // Her personelin rollerini getir
            var personelList = new List<PersonelWithRolesViewModel>();

            foreach (var personel in personels)
            {
                // AppUser'ı bul
                var user = await _userManager.FindByIdAsync(personel.AppUserId);
                var roles = await _userManager.GetRolesAsync(user);

                personelList.Add(new PersonelWithRolesViewModel
                {
                    Personel = personel,
                    HasAnyRole = roles.Any(),
                    Roles = string.Join(", ", roles)
                });
            }

            return View(personelList);
        }

        // Personel Ekle
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var activeDepartments = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
            var activePositions = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);

            ViewBag.Departments = new SelectList(activeDepartments, "Id", "Name");
            ViewBag.Positions = new SelectList(activePositions, "Id", "Name");

            return View();
        }

        // Personel Ekle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Personel personel, string password, [FromForm]  bool sendMail = false, string selectedRole = null)
        {
            var email = personel.Email;

            if (string.IsNullOrEmpty(personel.FullName) || personel.FullName.Length < 3)
                ModelState.AddModelError("FullName", "Ad Soyad en az 3 karakter olmalıdır!");

            if (personel.PositionId == null || personel.PositionId == 0)
                ModelState.AddModelError("PositionId", "Pozisyon seçilmelidir!");

            // ========== TELEFON KONTROLÜ  ==========
            if (string.IsNullOrEmpty(personel.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "Telefon numarası zorunludur!");
            }
            else if (!personel.PhoneNumber.IsValidTurkishPhone())
            {
                ModelState.AddModelError("PhoneNumber", "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 05XX XXX XX XX)");
            }
            else
            {
                personel.PhoneNumber = personel.PhoneNumber.NormalizePhone(); 
            }

            if (string.IsNullOrEmpty(personel.Email))
                ModelState.AddModelError("Email", "E-posta adresi zorunludur!");

            if (string.IsNullOrEmpty(password) || password.Length < 6)
                ModelState.AddModelError("", "Şifre en az 6 karakter olmalıdır!");

            ModelState.Remove("AppUserId");
            ModelState.Remove("AppUser");
            ModelState.Remove("Department");
            ModelState.Remove("Position");

            if (!ModelState.IsValid)
            {
                var activeDepartments = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
                var activePositions = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);

                ViewBag.Departments = new SelectList(activeDepartments, "Id", "Name", personel.DepartmentId);
                ViewBag.Positions = new SelectList(activePositions, "Id", "Name", personel.PositionId);

                return View(personel);
            }

            try
            {
                var user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FullName = personel.FullName,
                    PhoneNumber = personel.PhoneNumber,
                    Address = personel.Address,
                    City = personel.City,
                    District = personel.District,
                    IsActive = personel.IsActive,
                    CreatedAt = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {

                    personel.AppUserId = user.Id;
                    personel.Email = email;
                    personel.CreatedAt = DateTime.Now;

                    await _unitOfWork.Personels.AddAsync(personel);
                    await _unitOfWork.CompleteAsync();

                    if (sendMail)
                    {
                        try
                        {
                            var position = await _unitOfWork.Positions.GetByIdAsync(personel.PositionId ?? 0);
                            var department = await _unitOfWork.Departments.GetByIdAsync(personel.DepartmentId ?? 0);

                            string body = $@"
                                           <!DOCTYPE html>
                                           <html>
                                           <head><meta charset='utf-8'></head>
                                           <body style='font-family:Arial; text-align:center; padding:20px;'>
                                           <h2 style='color:#0d6efd;'>Teknik Servis Takip Sistemi</h2>
                                           <p>Sayın <b>{personel.FullName}</b>,</p>
                                           <p>Admin tarafından sisteme personel olarak eklendiniz.</p>
                                           <p><strong>Personel Bilgileriniz:</strong></p>
                                           <div style='background:#f8fafc; padding:15px; border-radius:8px; margin:15px 0; text-align:left;'>
                                           <p><strong>👤 Ad Soyad:</strong> {personel.FullName}</p>
                                           <p><strong>📧 E-posta:</strong> {email}</p>
                                           <p><strong>📱 Telefon:</strong> {personel.PhoneNumber}</p>
                                           <p><strong>💼 Pozisyon:</strong> {position?.Name ?? "-"}</p>
                                           <p><strong>🏢 Departman:</strong> {department?.Name ?? "-"}</p>
                                           <p><strong>🔑 Şifre:</strong> {password}</p>
                                           </div>
                                           <p>Aşağıdaki butona tıklayarak sisteme giriş yapabilirsiniz:</p>
                                           <div style='margin:20px 0;'>
                                           <a href='{Url.Action("Login", "Account", null, Request.Scheme)}' style='display:inline-block; padding:12px 24px; background:#0d6efd; color:white; text-decoration:none; border-radius:8px;'>Giriş Yap</a>
                                           </div>
                                           <hr/>
                                           <small>Bu e-posta otomatik olarak gönderilmiştir. Lütfen cevaplamayınız.</small>
                                           </body>
                                           </html>";

                            await _mailService.SendMailAsync(email, "Teknik Servis Takip - Personel Hesabı Oluşturuldu", body, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Personel maili gönderilemedi: {ex.Message}");
                        }
                    }

                    //======== İşlem Logu ========//
                    var currentUser = await _userManager.GetUserAsync(User);
                    var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                    await _logService.LogAsync(
                        action: $"{currentUserName} - Personel Ekleme",
                        actionType: "Create",
                        entityName: "Personel",
                        entityId: personel.Id,
                        description: $"Yeni personel eklendi: {personel.FullName}",
                        oldValues: null,
                        newValues: new { personel.FullName, personel.Email, PositionId = personel.PositionId, DepartmentId = personel.DepartmentId }
                    );

                    TempData["Success"] = "Personel başarıyla eklendi!" + (sendMail ? " Bilgilendirme maili gönderildi." : "");
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                var errorDepts = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
                var errorPoss = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);
                ViewBag.Departments = new SelectList(errorDepts, "Id", "Name", personel.DepartmentId);
                ViewBag.Positions = new SelectList(errorPoss, "Id", "Name", personel.PositionId);
                return View(personel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Kayıt hatası: " + ex.Message);
                var catchDepts = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
                var catchPoss = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);

                ViewBag.Departments = new SelectList(catchDepts, "Id", "Name", personel.DepartmentId);
                ViewBag.Positions = new SelectList(catchPoss, "Id", "Name", personel.PositionId);
                return View(personel);
            }
        }

        // Personel Düzenle
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var personel = await _unitOfWork.Personels.GetByIdAsync(id);
            if (personel == null) return NotFound();

            var activeDepartments = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
            var activePositions = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);
            ViewBag.Departments = new SelectList(activeDepartments, "Id", "Name", personel.DepartmentId);
            ViewBag.Positions = new SelectList(activePositions, "Id", "Name", personel.PositionId);
            return View(personel);
        }

        // Personel Düzenle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Personel personel, string selectedRole = null)
        {
            ModelState.Remove("AppUser");
            ModelState.Remove("AppUserId");
            ModelState.Remove("Repairs");
            ModelState.Remove("Department");
            ModelState.Remove("Position");

            if (string.IsNullOrEmpty(personel.FullName) || personel.FullName.Length < 3)
                ModelState.AddModelError("FullName", "Ad Soyad en az 3 karakter olmalıdır!");

            if (personel.PositionId == null || personel.PositionId == 0)
                ModelState.AddModelError("PositionId", "Pozisyon seçilmelidir!");

            // ========== TELEFON KONTROLÜ  ==========
            if (string.IsNullOrEmpty(personel.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "Telefon numarası zorunludur!");
            }
            else if (!personel.PhoneNumber.IsValidTurkishPhone())
            {
                ModelState.AddModelError("PhoneNumber", "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 05XX XXX XX XX)");
            }
            else
            {
                personel.PhoneNumber = personel.PhoneNumber.NormalizePhone(); 
            }

            if (string.IsNullOrEmpty(personel.Email))
                ModelState.AddModelError("Email", "E-posta adresi zorunludur!");

            if (!ModelState.IsValid)
            {
                var activeDepartments = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
                var activePositions = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);
                ViewBag.Departments = new SelectList(activeDepartments, "Id", "Name", personel.DepartmentId);
                ViewBag.Positions = new SelectList(activePositions, "Id", "Name", personel.PositionId);
                return View(personel);
            }

            try
            {
                var existingPersonel = await _unitOfWork.Personels.GetByIdAsync(personel.Id);
                if (existingPersonel == null) return NotFound();

                var oldValues = new { existingPersonel.FullName, existingPersonel.PositionId, existingPersonel.DepartmentId, existingPersonel.PhoneNumber, existingPersonel.Email };

                existingPersonel.FullName = personel.FullName;
                existingPersonel.PositionId = personel.PositionId;
                existingPersonel.DepartmentId = personel.DepartmentId;
                existingPersonel.PhoneNumber = personel.PhoneNumber;
                existingPersonel.Address = personel.Address;
                existingPersonel.City = personel.City;
                existingPersonel.District = personel.District;
                existingPersonel.Email = personel.Email;
                existingPersonel.IsActive = personel.IsActive;

                var user = await _userManager.FindByIdAsync(existingPersonel.AppUserId);
                if (user != null)
                {
                    user.FullName = personel.FullName;
                    user.Email = personel.Email;
                    user.UserName = personel.Email;
                    await _userManager.UpdateAsync(user);
                }

                _unitOfWork.Personels.Update(existingPersonel);
                await _unitOfWork.CompleteAsync();


                //========= İşlem Logu ===========//
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Personel Güncelleme",
                    actionType: "Update",
                    entityName: "Personel",
                    entityId: personel.Id,
                    description: $"Personel güncellendi: {personel.FullName}",
                    oldValues: oldValues,
                    newValues: new { personel.FullName, personel.PositionId, personel.DepartmentId, personel.PhoneNumber, personel.Email }
                );

                TempData["Success"] = "Personel başarıyla güncellendi!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Güncelleme hatası: " + ex.Message);
                var activeDepartments = await _unitOfWork.Departments.GetWhereAsync(d => d.IsActive == true);
                var activePositions = await _unitOfWork.Positions.GetWhereAsync(p => p.IsActive == true);
                ViewBag.Departments = new SelectList(activeDepartments, "Id", "Name", personel.DepartmentId);
                ViewBag.Positions = new SelectList(activePositions, "Id", "Name", personel.PositionId);
                return View(personel);
            }
        }

        // Personel Sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var personel = await _unitOfWork.Personels.GetByIdAsync(id);
                if (personel == null)
                {
                    return Json(new { success = false, message = "Personel bulunamadı!" });
                }

                var relatedRepairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.PersonelId == id);
                if (relatedRepairs != null && relatedRepairs.Any())
                {
                    return Json(new { success = false, message = "Bu personele ait tamir kayıtları var! Önce tamir kayıtlarını silin veya başka personele atayın." });
                }

                var userId = personel.AppUserId;
                var personelName = personel.FullName;

                _unitOfWork.Personels.Delete(personel);
                await _unitOfWork.CompleteAsync();

                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        var result = await _userManager.DeleteAsync(user);
                        if (!result.Succeeded)
                        {
                            return Json(new { success = false, message = "Kullanıcı silinemedi: " + string.Join(", ", result.Errors.Select(e => e.Description)) });
                        }
                    }
                }


                //========== İŞlem Logu ==========//
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
            action: $"{currentUserName} - Personel Silme",
            actionType: "Delete",
            entityName: "Personel",
            entityId: id,
            description: $"Personel silindi: {personelName}",
            oldValues: null,
            newValues: null
        );


                return Json(new { success = true, message = "Personel ve kullanıcı başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
        }

    }
}
