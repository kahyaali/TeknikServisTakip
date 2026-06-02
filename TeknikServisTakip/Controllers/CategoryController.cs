using Business.Abstract;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari,Depo")]
    public class CategoryController : Controller
    {
        private readonly ICategoryService _categoryService;
        private readonly IUnitOfWork _unitOfWork;

        public CategoryController(ICategoryService categoryService, IUnitOfWork unitOfWork)
        {
            _categoryService = categoryService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var categories = await _categoryService.GetAllAsync();
            return View(categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name));
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var userId = User.Identity?.Name ?? "System";
                    await _categoryService.AddAsync(category, userId);
                    TempData["Success"] = "Kategori başarıyla eklendi.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            return View(category);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null)
                return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var userId = User.Identity?.Name ?? "System";
                    await _categoryService.UpdateAsync(category, userId);
                    TempData["Success"] = "Kategori başarıyla güncellendi.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }   
            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";

                // Önce ilişkili ürün var mı kontrol et
                var hasProducts = await _unitOfWork.Products.GetWhereAsync(p => p.CategoryId == id);
                if (hasProducts.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bu kategoriye ait ürünler var. Önce ürünleri silmeli veya başka kategoriye taşımalısınız!"
                    });
                }

                var result = await _categoryService.DeleteAsync(id, userId);

                if (result)
                    return Json(new { success = true, message = "Kategori başarıyla silindi." });

                return Json(new { success = false, message = "Kategori bulunamadı." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SoftDelete(int id)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";
                var result = await _categoryService.SoftDeleteAsync(id, userId);

                if (result)
                    return Json(new { success = true, message = "Kategori pasif duruma getirildi." });

                return Json(new { success = false, message = "Kategori bulunamadı." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
