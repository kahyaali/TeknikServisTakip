using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TeknikServisTakip.Controllers
{

    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class LogoController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public LogoController(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            string logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "firmaimages", "logo.png");
            ViewBag.HasLogo = System.IO.File.Exists(logoPath);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadLogo(IFormFile logo)
        {
            if (logo == null || logo.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir dosya seçin!";
                return RedirectToAction("Index");
            }

            // Sadece resim dosyaları
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg" };
            var extension = Path.GetExtension(logo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                TempData["ErrorMessage"] = "Lütfen geçerli bir resim dosyası seçin (jpg, jpeg, png, gif, bmp, svg)";
                return RedirectToAction("Index");
            }

            // Boyut kontrolü (max 5MB)
            if (logo.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Dosya boyutu 5MB'dan büyük olamaz!";
                return RedirectToAction("Index");
            }

            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "firmaimages");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, "logo.png");

            // Eski logo varsa sil
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await logo.CopyToAsync(stream);
            }

            TempData["SuccessMessage"] = "Logo başarıyla güncellendi!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteLogo()
        {
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "firmaimages", "logo.png");
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                TempData["SuccessMessage"] = "Logo başarıyla silindi!";
            }
            else
            {
                TempData["ErrorMessage"] = "Logo bulunamadı!";
            }
            return RedirectToAction("Index");
        }
    }
}
