using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class ServiceController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;

        public ServiceController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ILogService logService, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _logService = logService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var services = await _unitOfWork.Services.GetAllAsync();
            return View(services.OrderBy(x => x.Order));
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Service service, IFormFile? imageFile)
        {
            // ModelState hatalarını görmek için
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelState Hatası: {error.ErrorMessage}");
                    TempData["Error"] = error.ErrorMessage;
                }
                return View(service);
            }

            // Resim yükleme (opsiyonel)
            if (imageFile != null && imageFile.Length > 0)
            {
                // Dosya tipi kontrolü
                var extension = Path.GetExtension(imageFile.FileName).ToLower();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".gif" && extension != ".webp")
                {
                    TempData["Error"] = "Sadece resim dosyaları (jpg, jpeg, png, gif, webp) yükleyebilirsiniz!";
                    return View(service);
                }

                // Dosya boyutu kontrolü (max 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Resim dosyası 5MB'dan büyük olamaz!";
                    return View(service);
                }

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                service.ImagePath = "/images/services/" + uniqueFileName;
            }

            service.CreatedAt = DateTime.Now;
            await _unitOfWork.Services.AddAsync(service);
            await _unitOfWork.CompleteAsync();

            // ========== İŞLEM LOGU ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Hizmet Ekleme",
                actionType: "Create",
                entityName: "Service",
                entityId: service.Id,
                description: $"Yeni hizmet eklendi: {service.Title}",
                oldValues: null,
                newValues: new { service.Title, service.Description, service.Order, service.IsActive }
            );

            TempData["Success"] = "Hizmet başarıyla eklendi!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var service = await _unitOfWork.Services.GetByIdAsync(id);
            if (service == null) return NotFound();
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Service service, IFormFile? imageFile)
        {
            // DEBUG: Gelen verileri kontrol et
            System.Diagnostics.Debug.WriteLine("===== EDIT POST BAŞLADI =====");
            System.Diagnostics.Debug.WriteLine($"Title: '{service.Title}'");
            System.Diagnostics.Debug.WriteLine($"Description: '{service.Description}'");
            System.Diagnostics.Debug.WriteLine($"Order: {service.Order}");
            System.Diagnostics.Debug.WriteLine($"IsActive: {service.IsActive}");
            System.Diagnostics.Debug.WriteLine($"ImageFile: {(imageFile != null ? imageFile.FileName : "NULL")}");

            // ImagePath boşsa null yap (required hatasını önlemek için)
            if (string.IsNullOrEmpty(service.ImagePath))
            {
                service.ImagePath = null;
            }

            // ModelState hatalarını detaylı göster
            if (!ModelState.IsValid)
            {
                System.Diagnostics.Debug.WriteLine("ModelState Hataları:");
                var errorMessages = new List<string>();

                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"  {key}: {error.ErrorMessage}");
                        errorMessages.Add($"{key}: {error.ErrorMessage}");
                    }
                }

                TempData["Error"] = string.Join(" | ", errorMessages);
                return View(service);
            }

            var existing = await _unitOfWork.Services.GetByIdAsync(service.Id);
            if (existing == null) return NotFound();

            // Resim yükleme (yeni resim varsa)
            if (imageFile != null && imageFile.Length > 0)
            {
                // Dosya tipi kontrolü
                var extension = Path.GetExtension(imageFile.FileName).ToLower();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".gif" && extension != ".webp")
                {
                    TempData["Error"] = "Sadece resim dosyaları (jpg, jpeg, png, gif, webp) yükleyebilirsiniz!";
                    return View(service);
                }

                // Dosya boyutu kontrolü (max 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Resim dosyası 5MB'dan büyük olamaz!";
                    return View(service);
                }

                // Eski resmi sil
                if (!string.IsNullOrEmpty(existing.ImagePath))
                {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, existing.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                existing.ImagePath = "/images/services/" + uniqueFileName;
            }

            existing.Title = service.Title;
            existing.Description = service.Description;
            existing.Order = service.Order;
            existing.IsActive = service.IsActive;
            existing.UpdatedAt = DateTime.Now;

            _unitOfWork.Services.Update(existing);
            await _unitOfWork.CompleteAsync();

            // ========== İŞLEM LOGU ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Hizmet Güncelleme",
                actionType: "Update",
                entityName: "Service",
                entityId: service.Id,
                description: $"Hizmet güncellendi: {service.Title}",
                oldValues: new { existing.Title, existing.Description, existing.Order, existing.IsActive },
                newValues: new { service.Title, service.Description, service.Order, service.IsActive }
            );

            TempData["Success"] = "Hizmet başarıyla güncellendi!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _unitOfWork.Services.GetByIdAsync(id);
            if (service == null)
            {
                return Json(new { success = false, message = "Hizmet bulunamadı!" });
            }

            // Resmi sil
            if (!string.IsNullOrEmpty(service.ImagePath))
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, service.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _unitOfWork.Services.Delete(service);
            await _unitOfWork.CompleteAsync();

            // ========== İŞLEM LOGU ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Hizmet Silme",
                actionType: "Delete",
                entityName: "Service",
                entityId: id,
                description: $"Hizmet silindi: {service.Title}",
                oldValues: new { service.Title, service.Description },
                newValues: null
            );

            return Json(new { success = true, message = "Hizmet başarıyla silindi!" });
        }
    }
}