using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TeknikServisTakip.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            return View();
        }
        [AllowAnonymous]
        public async Task<IActionResult> About()
        {
            var about = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "About")).FirstOrDefault();
            ViewBag.About = about ?? new PageContent();
            return View();
        }
        [AllowAnonymous]
        public async Task<IActionResult> Services()
        {
            var services = await _unitOfWork.Services.GetAllAsync();
            return View(services.Where(x => x.IsActive).OrderBy(x => x.Order));
        }
        [AllowAnonymous]
        public async Task<IActionResult> References()
        {
            var references = await _unitOfWork.References.GetAllAsync();
            return View(references.Where(x => x.IsActive).OrderBy(x => x.Order));
        }

        [AllowAnonymous]
        public async Task<IActionResult> VisionMission()
        {
            var vm = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "VisionMission")).FirstOrDefault();
            ViewBag.VisionMission = vm ?? new PageContent();
            return View();
        }
        [AllowAnonymous]
        public async Task<IActionResult> Contact()
        {
            var contact = (await _unitOfWork.PageContents.GetWhereAsync(p => p.PageName == "Contact")).FirstOrDefault();
            ViewBag.Contact = contact ?? new PageContent();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // 404 - Sayfa Bulunamadı
        [AllowAnonymous]
        public IActionResult NotFound404()
        {
            Response.StatusCode = 404;
            return View();
        }

        // 401 - Yetkisiz Erişim
        [AllowAnonymous]
        public IActionResult Unauthorized401()
        {
            Response.StatusCode = 401;
            return View();
        }

        // 403 - Yasak Erişim
        [AllowAnonymous]
        public IActionResult Forbidden403()
        {
            Response.StatusCode = 403;
            return View();
        }

        // Genel Hata
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }
    }
}