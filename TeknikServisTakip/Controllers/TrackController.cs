using DataAccess.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TeknikServisTakip.Controllers
{
    public class TrackController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public TrackController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string trackingCode)
        {
            if (string.IsNullOrEmpty(trackingCode))
                return View();

            trackingCode = trackingCode.Trim().ToUpper();

            var repairs = await _unitOfWork.RepairItems
                .GetWhereAsync(r => r.TrackingCode == trackingCode && r.IsDeleted == false);

            var repair = repairs.FirstOrDefault();

            if (repair == null)
            {
                ViewBag.Error = $"Geçersiz takip kodu: {trackingCode}";
                return View();
            }

            return RedirectToAction("Details", new { id = repair.Id });
        }

      

        public async Task<IActionResult> Details(int id)
        {
            var repair = await _unitOfWork.RepairItems.GetByIdAsync(id);
            if (repair == null) return NotFound();

            if (repair.PersonelId.HasValue)
                repair.Personel = await _unitOfWork.Personels.GetByIdAsync(repair.PersonelId.Value);
         
            return View(repair);
        }

        [HttpPost]
        public async Task<IActionResult> CheckTrackingCode(string trackingCode)
        {
            if (string.IsNullOrEmpty(trackingCode))
                return Json(new { success = false, message = "Takip kodu giriniz!" });

            trackingCode = trackingCode.Trim().ToUpper();

            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.TrackingCode == trackingCode && r.IsDeleted == false);
            var repair = repairs.FirstOrDefault();

            if (repair == null)
                return Json(new { success = false, message = $"Geçersiz takip kodu: {trackingCode}" });

            return Json(new { success = true, redirectUrl = Url.Action("Details", "Track", new { id = repair.Id }) });
        }
    }
}