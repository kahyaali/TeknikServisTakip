using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Entities.Concrete;
using TeknikServisTakip.Models.ViewModels;

namespace TeknikServisTakip.Controllers
{
    [Authorize] 
    public class PasswordController : Controller
    {
        private readonly UserManager<AppUser> _userManager;

        public PasswordController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }


        // oturum açmış kullanıcı kendi şifresini değiştirir
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // oturum açmış kullanıcı kendi şifresini değiştirir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Yeni şifreler eşleşmiyor!";
                return View();
            }

          
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                // Şifre değişti, çıkış yapıp tekrar giriş yapmasını sağlıyoruz
                await _userManager.UpdateSecurityStampAsync(user);
                // Çıkış yaptırıyoruz
                await _userManager.UpdateAsync(user);

                // Role göre yönlendir
                if (User.IsInRole("Customer"))
                    return RedirectToAction("Login", "Account", new { message = "password_changed" });
                else
                    return RedirectToAction("Login", "Account", new { message = "password_changed" });
            }

   
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            TempData["Error"] = errors;
            return View();
        }


        // ========== Yetkili kullanıcı başka kullanıcıların şifresini değiştirme ==========
        [HttpGet]
        public async Task<IActionResult> ResetUserPassword(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Geçersiz kullanıcı!";
                return RedirectToAction("Index", "Admin");
            }

            var targetUser = await _userManager.FindByIdAsync(id);
            if (targetUser == null)
            {
                TempData["Error"] = "Kullanıcı bulunamadı!";
                return RedirectToAction("Index", "Admin");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);

            // SuperAdmin kontrolü: Kendisinden başkası SuperAdmin'in şifresini değiştiremez
            var targetUserRoles = await _userManager.GetRolesAsync(targetUser);
            if (targetUserRoles.Contains("SuperAdmin") && currentUser.Id != targetUser.Id)
            {
                TempData["Error"] = "Super Admin şifresini sadece kendisi değiştirebilir!";
                return RedirectToAction("Index", "Admin");
            }

            var model = new ResetUserPasswordViewModel
            {
                UserId = targetUser.Id,
                UserName = targetUser.UserName,
                FullName = targetUser.FullName,
                Email = targetUser.Email,
                Roles = string.Join(", ", targetUserRoles)
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(ResetUserPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.IsNullOrEmpty(model.NewPassword) || model.NewPassword.Length < 6)
            {
                ModelState.AddModelError("NewPassword", "Şifre en az 6 karakter olmalıdır!");
                return View(model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Şifreler eşleşmiyor!");
                return View(model);
            }

            var targetUser = await _userManager.FindByIdAsync(model.UserId);
            if (targetUser == null)
            {
                TempData["Error"] = "Kullanıcı bulunamadı!";
                return RedirectToAction("Index", "Admin");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var targetUserRoles = await _userManager.GetRolesAsync(targetUser);

            // SuperAdmin kontrolü
            if (targetUserRoles.Contains("SuperAdmin") && currentUser.Id != targetUser.Id)
            {
                TempData["Error"] = "Super Admin şifresini sadece kendisi değiştirebilir!";
                return RedirectToAction("Index", "Admin");
            }

            // Token oluştur ve şifreyi sıfırla (mevcut şifre gerekmez)
            var token = await _userManager.GeneratePasswordResetTokenAsync(targetUser);
            var result = await _userManager.ResetPasswordAsync(targetUser, token, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = $"{targetUser.FullName} kullanıcısının şifresi başarıyla değiştirildi!";
                return RedirectToAction("Index", "Admin");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
