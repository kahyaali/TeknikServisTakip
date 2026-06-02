using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class PositionsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;
        public PositionsController(IUnitOfWork unitOfWork, ILogService logService, UserManager<AppUser> userManager) { _unitOfWork = unitOfWork; _logService = logService; _userManager = userManager; }

        // Positions List
        public async Task<IActionResult> Index()
        {
            var positions = await _unitOfWork.Positions.GetAllAsync();
            return View(positions);
        }

        // Pozisyon Ekleme
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Pozisyon Ekleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Position position)
        {
            if (ModelState.IsValid)
            {
                position.CreatedAt = DateTime.Now;
                await _unitOfWork.Positions.AddAsync(position);
                await _unitOfWork.CompleteAsync();

                //======== İşlem Logu ========//
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Pozisyon Ekleme",
                    actionType: "Create",
                    entityName: "Position",
                    entityId: position.Id,
                    description: $"Yeni pozisyon eklendi: {position.Name}",
                    oldValues: null,
                    newValues: new { position.Name, position.Description }
                );

                TempData["Success"] = "Pozisyon başarıyla eklendi!";
                return RedirectToAction("Index");
            }
            return View(position);
        }

        // Pozisyon Güncelleme
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var position = await _unitOfWork.Positions.GetByIdAsync(id);
            if (position == null) return NotFound();
            return View(position);
        }

        // Pozisyon Güncelleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Position position)
        {
            if (ModelState.IsValid)
            {
                var existing = await _unitOfWork.Positions.GetByIdAsync(position.Id);
                if (existing == null) return NotFound();

                var oldValues = new { existing.Name, existing.Description };
                var newValues = new { position.Name, position.Description };

                existing.Name = position.Name;
                existing.Description = position.Description;
                existing.IsActive = position.IsActive;

                _unitOfWork.Positions.Update(existing);
                await _unitOfWork.CompleteAsync();

                //========= İşlem Logu ===========//
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Pozisyon Güncelleme",
                    actionType: "Update",
                    entityName: "Position",
                    entityId: position.Id,
                    description: $"{currentUserName} tarafından pozisyon güncellendi: {position.Name}",
                    oldValues: oldValues,
                    newValues: newValues
                );

               

                TempData["Success"] = "Pozisyon başarıyla güncellendi!";
                return RedirectToAction("Index");
            }
            return View(position);
        }

        // Pozisyon Silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var position = await _unitOfWork.Positions.GetByIdAsync(id);
            if (position == null)
            {
                return Json(new { success = false, message = "Pozisyon bulunamadı!" });
            }

            var personels = await _unitOfWork.Personels.GetWhereAsync(p => p.PositionId == id);
            if (personels.Any())
            {
                return Json(new { success = false, message = "Bu pozisyona ait personeller var! Önce personelleri başka pozisyona taşıyın." });
            }

            _unitOfWork.Positions.Delete(position);
            await _unitOfWork.CompleteAsync();

            //========= İşlem Logu ===========//
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Pozisyon Silme",
                actionType: "Delete",
                entityName: "Position",
                entityId: id,
                description: $"Pozisyon silindi: {position.Name}",
                oldValues: null,
                newValues: null
            );

            return Json(new { success = true, message = "Pozisyon başarıyla silindi!" });
        }

    }
}
