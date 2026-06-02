using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class PageContentController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogService _logService;

        public PageContentController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ILogService logService)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _logService = logService;
        }

        public async Task<IActionResult> Index(string activeTab = "about")
        {
            var about = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "About")).FirstOrDefault();
            var contact = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "Contact")).FirstOrDefault();
            var visionMission = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "VisionMission")).FirstOrDefault();

            ViewBag.About = about ?? new PageContent { PageName = "About", Title = "Hakkımızda", Content = "Hakkımızda içeriği henüz eklenmemiştir." };
            ViewBag.Contact = contact ?? new PageContent { PageName = "Contact", Title = "İletişim", Content = "İletişim bilgileri henüz eklenmemiştir." };
            ViewBag.VisionMission = visionMission ?? new PageContent { PageName = "VisionMission", Title = "Vizyon & Misyon" };
            ViewBag.ActiveTab = activeTab;

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAbout(string title, string content, IFormFile? mainImage, List<IFormFile> sliderImages)
        {
            var about = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "About")).FirstOrDefault();

            string imagePath = null;
            List<string> sliderImagePaths = new List<string>();

            // Mevcut slider resimlerini al
            if (about?.SliderImages != null)
            {
                sliderImagePaths = JsonSerializer.Deserialize<List<string>>(about.SliderImages) ?? new List<string>();
            }

            // Ana resim yükleme
            if (mainImage != null && mainImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "pages");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + mainImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await mainImage.CopyToAsync(fileStream);
                }

                imagePath = "/images/pages/" + uniqueFileName;
            }

            // Yeni slider resimleri ekle (mevcutları silmeden)
            if (sliderImages != null && sliderImages.Any())
            {
                string sliderFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "pages", "slider");
                if (!Directory.Exists(sliderFolder))
                    Directory.CreateDirectory(sliderFolder);

                foreach (var img in sliderImages)
                {
                    if (img != null && img.Length > 0)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + img.FileName;
                        string filePath = Path.Combine(sliderFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await img.CopyToAsync(fileStream);
                        }

                        sliderImagePaths.Add("/images/pages/slider/" + uniqueFileName);
                    }
                }
            }

            if (about == null)
            {
                about = new PageContent
                {
                    PageName = "About",
                    Title = title,
                    Content = content,
                    ImageUrl = imagePath,
                    SliderImages = sliderImagePaths.Any() ? JsonSerializer.Serialize(sliderImagePaths) : null,
                    UpdatedAt = DateTime.Now
                };
                await _unitOfWork.PageContents.AddAsync(about);
            }
            else
            {
                about.Title = title;
                about.Content = content;
                if (imagePath != null)
                    about.ImageUrl = imagePath;
                if (sliderImagePaths.Any())
                    about.SliderImages = JsonSerializer.Serialize(sliderImagePaths);
                about.UpdatedAt = DateTime.Now;
                _unitOfWork.PageContents.Update(about);
            }

            await _unitOfWork.CompleteAsync();
            // ========== İŞLEM LOGU ==========
            await _logService.LogAsync(
                action: "PageContent/SaveAbout",
                actionType: "Update",
                entityName: "PageContent",
                entityId: about?.Id,
                description: $"Hakkımızda sayfası güncellendi. Başlık: {title}",
                oldValues: null,
                newValues: new { title, content, imagePath, sliderCount = sliderImagePaths.Count }
            );
            TempData["Success"] = "Hakkımızda sayfası güncellendi!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveContact(string title, string content, string address, string phone, string email, string mapUrl)
        {
            bool hasError = false;

            // Telefon validasyonu
            if (!string.IsNullOrEmpty(phone))
            {
                var digits = new string(phone.Where(char.IsDigit).ToArray());
                if (digits.Length != 11 || !digits.StartsWith("05"))
                {
                    ModelState.AddModelError("phone", "Geçerli bir Türkiye telefon numarası giriniz! (05XX XXX XX XX)");
                    hasError = true;
                }
                else
                {
                    phone = $"{digits.Substring(0, 3)} {digits.Substring(3, 3)} {digits.Substring(6, 2)} {digits.Substring(8, 3)}";
                }
            }

            // Email validasyonu
            if (!string.IsNullOrEmpty(email))
            {
                try
                {
                    var mailAddress = new System.Net.Mail.MailAddress(email);
                    if (mailAddress.Address != email)
                    {
                        ModelState.AddModelError("email", "Geçerli bir e-posta adresi giriniz!");
                        hasError = true;
                    }
                }
                catch
                {
                    ModelState.AddModelError("email", "Geçerli bir e-posta adresi giriniz!");
                    hasError = true;
                }
            }

            // Zorunlu alan kontrolleri
            if (string.IsNullOrEmpty(title))
            {
                ModelState.AddModelError("title", "Başlık zorunludur!");
                hasError = true;
            }

            if (string.IsNullOrEmpty(content))
            {
                ModelState.AddModelError("content", "İçerik zorunludur!");
                hasError = true;
            }

            if (hasError)
            {
                // Hataları ViewBag'e aktar
                var about = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "About")).FirstOrDefault();
                ViewBag.About = about ?? new PageContent();
                ViewBag.Contact = new PageContent
                {
                    Title = title,
                    Content = content,
                    Address = address,
                    Phone = phone,
                    Email = email,
                    MapUrl = mapUrl
                };
                TempData["Error"] = "Lütfen hataları düzeltin!";
                return View("Index");
            }

            var contact = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "Contact")).FirstOrDefault();

            if (contact == null)
            {
                contact = new PageContent
                {
                    PageName = "Contact",
                    Title = title,
                    Content = content,
                    Address = address,
                    Phone = phone,
                    Email = email,
                    MapUrl = mapUrl,
                    UpdatedAt = DateTime.Now
                };
                await _unitOfWork.PageContents.AddAsync(contact);
            }
            else
            {
                contact.Title = title;
                contact.Content = content;
                contact.Address = address;
                contact.Phone = phone;
                contact.Email = email;
                contact.MapUrl = mapUrl;
                contact.UpdatedAt = DateTime.Now;
                _unitOfWork.PageContents.Update(contact);
            }

            await _unitOfWork.CompleteAsync();
            // ========== İŞLEM LOGU ==========
            await _logService.LogAsync(
                action: "PageContent/SaveContact",
                actionType: "Update",
                entityName: "PageContent",
                entityId: contact?.Id,
                description: $"İletişim sayfası güncellendi. Başlık: {title}",
                oldValues: null,
                newValues: new { title, content, address, phone, email }
            );
            TempData["Success"] = "İletişim sayfası başarıyla güncellendi!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSliderImage(string imageUrl)
        {
            try
            {
                var about = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "About")).FirstOrDefault();
                if (about == null || string.IsNullOrEmpty(about.SliderImages))
                {
                    return Json(new { success = false, message = "Resim bulunamadı!" });
                }

                var sliderImages = JsonSerializer.Deserialize<List<string>>(about.SliderImages) ?? new List<string>();
                var urlsToDelete = imageUrl.Split(',').ToList();

                foreach (var url in urlsToDelete)
                {
                    if (sliderImages.Contains(url))
                    {
                        sliderImages.Remove(url);

                        // Dosyayı sil
                        string filePath = Path.Combine(_webHostEnvironment.WebRootPath, url.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }

                about.SliderImages = sliderImages.Any() ? JsonSerializer.Serialize(sliderImages) : null;
                _unitOfWork.PageContents.Update(about);
                await _unitOfWork.CompleteAsync();
                // ========== İŞLEM LOGU ==========
                await _logService.LogAsync(
                    action: "PageContent/DeleteSliderImage",
                    actionType: "Delete",
                    entityName: "SliderImage",
                    entityId: null,
                    description: $"{urlsToDelete.Count} slider resmi silindi. Silinenler: {imageUrl}",
                    oldValues: null,
                    newValues: null
                );

                return Json(new { success = true, message = $"{urlsToDelete.Count} resim silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        // ========== VİZYON & MİSYON ==========
        [HttpPost]
        public async Task<IActionResult> SaveVisionMission(string title, string content)
        {
            var vm = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "VisionMission")).FirstOrDefault();

            if (vm == null)
            {
                vm = new PageContent
                {
                    PageName = "VisionMission",
                    Title = title,
                    Content = content,
                    UpdatedAt = DateTime.Now
                };
                await _unitOfWork.PageContents.AddAsync(vm);
            }
            else
            {
                vm.Title = title;
                vm.Content = content;
                vm.UpdatedAt = DateTime.Now;
                _unitOfWork.PageContents.Update(vm);
            }

            await _unitOfWork.CompleteAsync();

            // ========== İŞLEM LOGU ==========
            await _logService.LogAsync(
                action: "PageContent/EditVisionMission",
                actionType: "Update",
                entityName: "PageContent",
                entityId: vm?.Id,
                description: $"Vizyon & Misyon sayfası güncellendi. Başlık: {title}",
                oldValues: null,
                newValues: new { title, content }
            );
            TempData["Success"] = "Vizyon & Misyon sayfası güncellendi!";
            return RedirectToAction("Index", new { activeTab = "visionmission" });
        }
    }
}