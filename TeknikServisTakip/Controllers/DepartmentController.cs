using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class DepartmentController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;
        public DepartmentController(IUnitOfWork unitOfWork, ILogService logService, UserManager<AppUser> userManager) { _unitOfWork = unitOfWork; _logService = logService; _userManager = userManager; }

        // Departman Listesi
        public async Task<IActionResult> Index()
        {
            var departments = await _unitOfWork.Departments.GetAllAsync();
            return View(departments);
        }

        // Departman Ekleme
        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        // Departman Ekleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department)
        {
            if (ModelState.IsValid)
            {
                department.CreatedAt = DateTime.Now;
                await _unitOfWork.Departments.AddAsync(department);
                await _unitOfWork.CompleteAsync();

                //========== İşlem Logu ======== //
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";

                await _logService.LogAsync(
                    action: $"{currentUserName} - Departman Ekleme",
                    actionType: "Create",
                    entityName: "Department",
                    entityId: department.Id,
                    description: $"Yeni departman eklendi: {department.Name}",
                    oldValues: null,
                    newValues: new { department.Name, department.Description }
                );

                TempData["Success"] = "Departman başarıyla eklendi!";
                return RedirectToAction("Index");
            }
            return View(department);
        }

        // Departman Güncelleme
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _unitOfWork.Departments.GetByIdAsync(id);
            if (department == null) return NotFound();
            return View(department);
        }

        // Departman Güncelleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Department department)
        {
            if (ModelState.IsValid)
            {
                var existing = await _unitOfWork.Departments.GetByIdAsync(department.Id);
                if (existing == null) return NotFound();

                var oldValues = new { existing.Name, existing.Description };
                var newValues = new { department.Name, department.Description };

                existing.Name = department.Name;
                existing.Description = department.Description;
                existing.IsActive = department.IsActive;

                _unitOfWork.Departments.Update(existing);
                await _unitOfWork.CompleteAsync();

                // İşlem Logu
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Departman Güncelleme",
                    actionType: "Update",
                    entityName: "Department",
                    entityId: department.Id,
                    description: $"Departman güncellendi: {department.Name}",
                    oldValues: oldValues,
                    newValues: newValues
                );

                TempData["Success"] = "Departman başarıyla güncellendi!";
                return RedirectToAction("Index");
            }
            return View(department);
        }

        //Departman Silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _unitOfWork.Departments.GetByIdAsync(id);
            if (department == null)
            {
                return Json(new { success = false, message = "Departman bulunamadı!" });
            }

            var personels = await _unitOfWork.Personels.GetWhereAsync(p => p.DepartmentId == id);
            if (personels.Any())
            {
                return Json(new { success = false, message = "Bu departmana ait personeller var! Önce personelleri başka departmana taşıyın." });
            }

            _unitOfWork.Departments.Delete(department);
            await _unitOfWork.CompleteAsync();


            //========== İşlem Logu ============
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";

            await _logService.LogAsync(
                action: $"{currentUserName} - Departman Silme",
                actionType: "Delete",
                entityName: "Department",
                entityId: id,
                description: $"Departman silindi: {department.Name}",
                oldValues: null,
                newValues: null
            );

            return Json(new { success = true, message = "Departman başarıyla silindi!" });
        }

    }
}
