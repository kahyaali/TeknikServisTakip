using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class ReferenceController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;

        public ReferenceController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ILogService logService, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _logService = logService;
            _userManager = userManager;
        }

        // Liste
        public async Task<IActionResult> Index()
        {
            var references = await _unitOfWork.References.GetAllAsync();
            return View(references.OrderBy(x => x.Order));
        }

        // Ekle - GET
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Ekle - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reference reference, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                // Resim yükleme
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "references");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    reference.ImagePath = "/images/references/" + uniqueFileName;
                }

                reference.CreatedAt = DateTime.Now;
                await _unitOfWork.References.AddAsync(reference);
                await _unitOfWork.CompleteAsync();

                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Reference Ekleme",
                    actionType: "Create",
                    entityName: "Reference",
                    entityId: reference.Id,
                    description: $"Yeni referans eklendi. Müşteri: {reference.CustomerName}",
                    oldValues: null,
                    newValues: new { reference.CustomerName, reference.Title, reference.Comment, reference.Order, reference.IsActive }
                );

                TempData["Success"] = "Referans başarıyla eklendi!";
                return RedirectToAction("Index");
            }
            return View(reference);
        }

        // Düzenle - GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reference = await _unitOfWork.References.GetByIdAsync(id);
            if (reference == null) return NotFound();
            return View(reference);
        }

        // Düzenle - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Reference reference, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                var existing = await _unitOfWork.References.GetByIdAsync(reference.Id);
                if (existing == null) return NotFound();

                // Resim yükleme (yeni resim varsa)
                if (imageFile != null && imageFile.Length > 0)
                {
                    // Eski resmi sil
                    if (!string.IsNullOrEmpty(existing.ImagePath))
                    {
                        string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, existing.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "references");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    existing.ImagePath = "/images/references/" + uniqueFileName;
                }

                existing.CustomerName = reference.CustomerName;
                existing.Title = reference.Title;
                existing.Comment = reference.Comment;
                existing.Order = reference.Order;
                existing.IsActive = reference.IsActive;
                existing.UpdatedAt = DateTime.Now;

                _unitOfWork.References.Update(existing);
                await _unitOfWork.CompleteAsync();

                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Reference Güncelleme",
                    actionType: "Update",
                    entityName: "Reference",
                    entityId: reference.Id,
                    description: $"Referans güncellendi. Müşteri: {reference.CustomerName}",
                    oldValues: new { existing.CustomerName, existing.Title, existing.Comment, existing.Order, existing.IsActive },
                    newValues: new { reference.CustomerName, reference.Title, reference.Comment, reference.Order, reference.IsActive }
                );

                TempData["Success"] = "Referans başarıyla güncellendi!";
                return RedirectToAction("Index");
            }
            return View(reference);
        }

        // Sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var reference = await _unitOfWork.References.GetByIdAsync(id);
                if (reference == null)
                {
                    return Json(new { success = false, message = "Referans bulunamadı!" });
                }

                // Resmi sil
                if (!string.IsNullOrEmpty(reference.ImagePath))
                {
                    string filePath = Path.Combine(_webHostEnvironment.WebRootPath, reference.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }

                _unitOfWork.References.Delete(reference);
                await _unitOfWork.CompleteAsync();

                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Reference Silme",
                    actionType: "Delete",
                    entityName: "Reference",
                    entityId: id,
                    description: $"Referans silindi. Müşteri: {reference.CustomerName}",
                    oldValues: new { reference.CustomerName, reference.Title, reference.Comment },
                    newValues: null
                );

                return Json(new { success = true, message = "Referans başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                // HATAYI GÖR
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}