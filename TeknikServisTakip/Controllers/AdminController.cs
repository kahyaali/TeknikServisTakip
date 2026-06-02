using Dapper;
using DataAccess.Context;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Entities.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Hubs;
using TeknikServisTakip.Models.ViewModels;
using TeknikServisTakip.Services;


namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class AdminController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IMailService _mailService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogService _logService;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;

        public AdminController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager,
            IWebHostEnvironment webHostEnvironment, IMailService mailService,
            IHubContext<NotificationHub> hubContext, ILogService logService,
            RoleManager<IdentityRole> roleManager, IConfiguration configuration, AppDbContext context)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _mailService = mailService;
            _hubContext = hubContext;
            _logService = logService;
            _roleManager = roleManager;
            _configuration = configuration;
            _context = context;
        }

        // Ay ismini almak için yardımcı metod
        string GetMonthName(int month)
        {
            return month switch
            {
                1 => "Ocak",
                2 => "Şubat",
                3 => "Mart",
                4 => "Nisan",
                5 => "Mayıs",
                6 => "Haziran",
                7 => "Temmuz",
                8 => "Ağustos",
                9 => "Eylül",
                10 => "Ekim",
                11 => "Kasım",
                12 => "Aralık",
                _ => month.ToString()
            };
        }

        public async Task<IActionResult> Statistics()
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync(r => r.Personel, r => r.AppUser);

            ViewBag.TotalRepairs = repairs.Count();
            ViewBag.PendingRepairs = repairs.Count(r => r.StatusId == 1);
            ViewBag.InProgressRepairs = repairs.Count(r => r.StatusId == 2);
            ViewBag.CompletedRepairs = repairs.Count(r => r.StatusId == 3);
            ViewBag.TotalPersonel = (await _unitOfWork.Personels.GetAllAsync()).Count();
            ViewBag.TotalCustomers = (await _userManager.GetUsersInRoleAsync("Customer")).Count();
            ViewBag.SonTamirler = repairs.OrderByDescending(r => r.ReceivedDate).Take(5).ToList();

            // Aylık veriler
            var monthlyData = repairs
                .Where(r => r.ReceivedDate != null)
                .GroupBy(r => new { r.ReceivedDate.Year, r.ReceivedDate.Month })
                .Select(g => new
                {
                    Ay = $"{g.Key.Month}/{g.Key.Year}",
                    Sayi = g.Count()
                })
                .OrderBy(x => x.Ay)
                .ToList();

            ViewBag.Months = monthlyData.Select(x => x.Ay).ToArray();
            ViewBag.MonthlyCounts = monthlyData.Select(x => x.Sayi).ToArray();

            // Durum dağılımı
            ViewBag.StatusLabels = new[] { "Beklemede", "İşlemde", "Tamamlandı" };
            ViewBag.StatusData = new[] {
                repairs.Count(r => r.StatusId == 1),
                repairs.Count(r => r.StatusId == 2),
                repairs.Count(r => r.StatusId == 3)
            };

            return View();
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync();

            // Toplam Tamir
            ViewBag.TotalRepairs = repairs.Count();

            // Bekleyen: 1,2,3,4,5,6 (Ürün Kaydedildi'den Teklif Onaylandı'ya kadar)
            ViewBag.PendingRepairs = repairs.Count(r =>
                r.StatusId == (int)RepairStatusEnum.UrunKaydedildi ||
                r.StatusId == (int)RepairStatusEnum.ExpertizBekleniyor ||
                r.StatusId == (int)RepairStatusEnum.ExpertizeGonderildi ||
                r.StatusId == (int)RepairStatusEnum.TeklifHazirlaniyor ||
                r.StatusId == (int)RepairStatusEnum.TeklifGonderildi ||
                r.StatusId == (int)RepairStatusEnum.TeklifOnaylandi);

            // İşlemde: 7 (İşleme Alındı)
            ViewBag.InProgressRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.IslemeAlindi);

            // Tamamlanan: 8 (Tamamlandı) veya 9 (Teslim Edildi)
            ViewBag.CompletedRepairs = repairs.Count(r =>
                r.StatusId == (int)RepairStatusEnum.Tamamlandi ||
                r.StatusId == (int)RepairStatusEnum.TeslimEdildi);

            // Toplam Personel
            ViewBag.TotalPersonel = (await _unitOfWork.Personels.GetAllAsync()).Count();

            // Grafik için veriler
            ViewBag.StatusLabels = new[] { "Bekleyen", "İşlemde", "Tamamlanan" };
            ViewBag.StatusData = new[] { ViewBag.PendingRepairs, ViewBag.InProgressRepairs, ViewBag.CompletedRepairs };

            // Aylık trend için son 12 ay
            var months = new List<string>();
            var monthlyCounts = new List<int>();
            for (int i = 11; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                months.Add(date.ToString("MMM yyyy"));
                var count = repairs.Count(r => r.ReceivedDate.Year == date.Year && r.ReceivedDate.Month == date.Month);
                monthlyCounts.Add(count);
            }
            ViewBag.Months = months.ToArray();
            ViewBag.MonthlyCounts = monthlyCounts.ToArray();

            return View();
        }


        // Admin Listesi
        public async Task<IActionResult> AdminList()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AddAdmin()
        {
            var roles = new List<SelectListItem>
    {
        new SelectListItem { Value = "SuperAdmin", Text = "SuperAdmin" },
        new SelectListItem { Value = "Admin", Text = "Admin" },
        new SelectListItem { Value = "Idari", Text = "Idari" },
        new SelectListItem { Value = "Depo", Text = "Depo" },
        new SelectListItem { Value = "Sevkiyat", Text = "Sevkiyat" },
        new SelectListItem { Value = "Personel", Text = "Personel" }
    };

            ViewBag.Roles = roles;
            return View();
        }

        // Admin Ekleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdmin(string fullName, string email, string password, string confirmPassword, string selectedRole = null,
            string phoneNumber = null, string address = null, string city = null, string district = null)
        {
            if (password != confirmPassword)
            {
                TempData["Error"] = "Şifreler eşleşmiyor!";
                return View();
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "E-posta ve şifre zorunludur!";
                return View();
            }

            if (password.Length < 6)
            {
                TempData["Error"] = "Şifre en az 6 karakter olmalıdır!";
                return View();
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                TempData["Error"] = "Bu e-posta ile kayıtlı kullanıcı zaten var!";
                return View();
            }

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                PhoneNumber = phoneNumber ?? "05550000000",
                Address = address ?? "İstanbul",
                City = city ?? "İstanbul",
                District = district ?? "Merkez",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // ========== ROL ATAMA ==========
                // Seçilen rol yoksa default "Admin"
                var roleToAssign = string.IsNullOrEmpty(selectedRole) ? "Admin" : selectedRole;

                // Rolün var olduğundan emin ol
                if (!await _roleManager.RoleExistsAsync(roleToAssign))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleToAssign));
                }

                await _userManager.AddToRoleAsync(user, roleToAssign);
                // ================================


                await _userManager.AddToRoleAsync(user, "Admin");
                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                action: $"{currentUserName} - Kullanıcı Ekleme",
                actionType: "Create",
                entityName: "Admin",
                entityId: null,
                description: $"Yeni admin eklendi: {fullName} - {email}",
                oldValues: null,
                newValues: new { fullName, email }
            );
                TempData["Success"] = "Yeni admin başarıyla eklendi!";
                return RedirectToAction("Index");
            }

            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        // Admin Düzenle
        [HttpGet]
        public async Task<IActionResult> EditAdmin(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var admin = await _userManager.FindByIdAsync(id);
            if (admin == null)
                return NotFound();

            // Kullanıcının mevcut rolünü bul
            var currentRoles = await _userManager.GetRolesAsync(admin);
            var currentRole = currentRoles.FirstOrDefault() ?? "Admin";


            ViewBag.CurrentRole = currentRole;

            return View(admin);
        }

        // Admin Düzenle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdmin(string id, string fullName, string email,
         string phoneNumber = null, string address = null, string city = null, string district = null)
        {
            var admin = await _userManager.FindByIdAsync(id);
            if (admin == null)
            {
                TempData["Error"] = "Admin bulunamadı!";
                return RedirectToAction("AdminList");
            }

            if (string.IsNullOrEmpty(fullName))
            {
                TempData["Error"] = "Ad Soyad boş olamaz!";
                return RedirectToAction("EditAdmin", new { id = id });
            }

            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "E-posta boş olamaz!";
                return RedirectToAction("EditAdmin", new { id = id });
            }

            if (admin.Email != email)
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && existingUser.Id != id)
                {
                    TempData["Error"] = "Bu e-posta adresi başka bir kullanıcı tarafından kullanılıyor!";
                    return RedirectToAction("EditAdmin", new { id = id });
                }
                admin.UserName = email;
                admin.Email = email;
            }

            // ========== TÜM ALANLARI GÜNCELLE (email değişmese de) ==========
            admin.FullName = fullName;
            admin.PhoneNumber = phoneNumber ?? admin.PhoneNumber;
            admin.Address = address ?? admin.Address;
            admin.City = city ?? admin.City;
            admin.District = district ?? admin.District;
            // ================================================================


            var result = await _userManager.UpdateAsync(admin);
            if (result.Succeeded)
            {


                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                var oldAdminValues = new { admin.FullName, admin.Email };
                var newAdminValues = new { fullName, email };

                await _logService.LogAsync(
                    action: $"{currentUserName} - Kullanıcı Güncelleme",
                    actionType: "Update",
                    entityName: "Admin",
                    entityId: null,
                    description: $"Admin bilgileri güncellendi: {fullName} - {email}",
                    oldValues: oldAdminValues,
                    newValues: newAdminValues
                );
                TempData["Success"] = "Admin bilgileri başarıyla güncellendi!";
                return RedirectToAction("AdminList");
            }

            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return RedirectToAction("EditAdmin", new { id = id });
        }

        // Admin Sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "Admin ID boş olamaz!" });
                }

                var admin = await _userManager.FindByIdAsync(id);
                if (admin == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
                }

                // ========== SUPERADMIN SİLİNEMEZ KONTROLÜ ==========
                var roles = await _userManager.GetRolesAsync(admin);
                if (roles.Contains("SuperAdmin"))
                {
                    return Json(new { success = false, message = "Süper Admin kullanıcısı silinemez!" });
                }
                // =================================================

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser.Id == id)
                {
                    return Json(new { success = false, message = "Kendi hesabınızı silemezsiniz!" });
                }

                var adminName = admin.FullName;
                var adminEmail = admin.Email;

                var personel = await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == id);
                if (personel != null && personel.Any())
                {
                    return Json(new { success = false, message = "Bu kullanıcının personel kaydı var! Önce personel kaydını silin." });
                }

                var result = await _userManager.DeleteAsync(admin);
                if (result.Succeeded)
                {

                    //========== İşlem Logu ===========/
                    var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                    await _logService.LogAsync(
                        action: $"{currentUserName} - Kullanıcı Silme",
                        actionType: "Delete",
                        entityName: "Admin",
                        entityId: null,
                        description: $"Admin silindi: {adminName} - {adminEmail}",
                        oldValues: null,
                        newValues: null
                    );
                    return Json(new { success = true, message = "Admin başarıyla silindi!" });
                }

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
        }



        // ========== KULLANICIYA ROL ATAMA  ==========
        [HttpGet]
        public async Task<IActionResult> AssignRole(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // KORUNAN KULLANICI KONTROLÜ
            bool isProtected = user.IsSystemAdmin;
            ViewBag.IsProtected = isProtected;

            // Sabit roller (sistemdeki tüm roller)
            var allRoles = new List<string> { "SuperAdmin", "Admin", "Idari", "Depo", "Sevkiyat", "Personel", "Customer" };
            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new AssignRoleViewModel
            {
                UserId = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                Roles = allRoles.Select(r => new RoleCheckboxViewModel
                {
                    RoleId = r,
                    RoleName = r,
                    IsSelected = userRoles.Contains(r)
                }).ToList()
            };

            return View(model);
        }

        // ========== KULLANICIYA ROL ATAMA  ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(AssignRoleViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound();

            // Mevcut rolleri bul
            var currentRoles = await _userManager.GetRolesAsync(user);

            // Seçili rolleri bul
            var selectedRoles = model.Roles.Where(r => r.IsSelected).Select(r => r.RoleName).ToList();

            // Eklenecek rolleri bul
            var rolesToAdd = selectedRoles.Except(currentRoles).ToList();

            // Silinecek rolleri bul
            var rolesToRemove = currentRoles.Except(selectedRoles).ToList();

            // Rolleri sil
            if (rolesToRemove.Any())
            {
                var result = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Roller kaldırılırken hata oluştu!";
                    return RedirectToAction("UserRoles");
                }
            }

            // Rolleri ekle
            if (rolesToAdd.Any())
            {
                var result = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Roller eklenirken hata oluştu!";
                    return RedirectToAction("UserRoles");
                }
            }

            TempData["Success"] = "Kullanıcı rolleri başarıyla güncellendi!";
            return RedirectToAction("UserRoles");
        }

        // Kullanıcı Rol Listesi
        public async Task<IActionResult> UserRoles()
        {
        
            return View();
        }

        public async Task<IActionResult> CustomerRoles()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetCustomerRolesJson(int draw, int start, int length, string search = null)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

                // Customer rolünün ID'sini al
                var customerRoleId = await connection.ExecuteScalarAsync<string>(
                    "SELECT Id FROM AspNetRoles WHERE Name = 'Customer'"
                );

                if (string.IsNullOrEmpty(customerRoleId))
                {
                    return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
                }

                // 1. TOPLAM SAYI (Sadece Customer)
                var totalCount = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @CustomerRoleId
        ", new { CustomerRoleId = customerRoleId });

                // 2. FİLTRELENMİŞ SAYI
                var filteredCount = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @CustomerRoleId
            AND (@Search IS NULL 
                OR u.FullName LIKE @Search 
                OR u.Email LIKE @Search)
        ", new { Search = searchPattern, CustomerRoleId = customerRoleId });

                // 3. SAYFALANMIŞ KULLANICILAR
                var users = await connection.QueryAsync(@"
            SELECT 
                u.Id,
                u.FullName,
                u.Email
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @CustomerRoleId
            AND (@Search IS NULL 
                OR u.FullName LIKE @Search 
                OR u.Email LIKE @Search)
            ORDER BY u.FullName
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
        ", new
                {
                    Search = searchPattern,
                    CustomerRoleId = customerRoleId,
                    Offset = start,
                    Limit = length <= 0 ? 10 : length
                });

                var userList = users.ToList();
                var userIds = userList.Select(x => x.Id).ToList();

                // 4. KULLANICILARIN ROLLERİ
                var rolesData = await connection.QueryAsync(@"
            SELECT 
                ur.UserId,
                r.Name AS RoleName
            FROM AspNetUserRoles ur
            INNER JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE ur.UserId IN @UserIds
        ", new { UserIds = userIds });

                var roleDict = rolesData
                    .GroupBy(x => x.UserId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.RoleName).ToList()
                    );

                // 5. SONUÇ VERİSİ
                var data = userList.Select(user => new
                {
                    userId = user.Id,
                    fullName = user.FullName ?? "-",
                    email = user.Email ?? "-",
                    roles = roleDict.ContainsKey(user.Id)
                        ? roleDict[user.Id]
                        : new List<string>()
                });

                return Json(new
                {
                    draw,
                    recordsTotal = totalCount,
                    recordsFiltered = filteredCount,
                    data
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        // UserRole pagination

        private string? _cachedCustomerRoleId;
        private readonly object _roleLock = new object();

        private async Task<string> GetCustomerRoleIdAsync(SqlConnection connection)
        {
            if (_cachedCustomerRoleId != null)
                return _cachedCustomerRoleId;

            lock (_roleLock)
            {
                if (_cachedCustomerRoleId != null)
                    return _cachedCustomerRoleId;

                _cachedCustomerRoleId = connection.ExecuteScalarAsync<string>(
                    "SELECT Id FROM AspNetRoles WHERE Name = 'Customer'"
                ).GetAwaiter().GetResult();
            }

            return _cachedCustomerRoleId;
        }


        [HttpPost]
        public async Task<IActionResult> GetUserRolesJson(int draw, int start, int length, string search = null)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

              
                var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

                // Customer rolünün ID'sini alıyoruz
                var customerRoleId = await GetCustomerRoleIdAsync(connection);

                if (string.IsNullOrEmpty(customerRoleId))
                {
                    return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
                }

                // TEK SORGUDA hem count hem veri
                var offset = start;
                var limit = length <= 0 ? 10 : length;

                var sql = @"
            SELECT 
                COUNT(*) OVER() as TotalCount,
                u.Id,
                u.FullName,
                u.Email
            FROM AspNetUsers u
            WHERE NOT EXISTS (
                SELECT 1 FROM AspNetUserRoles ur 
                WHERE ur.UserId = u.Id AND ur.RoleId = @CustomerRoleId
            )
            AND (@Search IS NULL 
                OR u.FullName LIKE @Search 
                OR u.Email LIKE @Search)
            ORDER BY u.FullName
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

                var results = await connection.QueryAsync<dynamic>(sql, new
                {
                    Search = searchPattern,
                    CustomerRoleId = customerRoleId,
                    Offset = offset,
                    Limit = limit
                });

                var resultList = results.ToList();
                var totalCount = resultList.FirstOrDefault()?.TotalCount ?? 0;
                var filteredCount = totalCount;

                var users = resultList.Select(r => new { r.Id, r.FullName, r.Email }).ToList();
                var userIds = users.Select(x => (string)x.Id).ToList();

                // Rolleri alıyoruz
                string roleSql = @"
            SELECT 
                ur.UserId,
                r.Name AS RoleName
            FROM AspNetUserRoles ur
            INNER JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE ur.UserId IN @UserIds";

                var rolesData = await connection.QueryAsync(roleSql, new { UserIds = userIds });

                var roleDict = rolesData
                    .GroupBy(x => (string)x.UserId)
                    .ToDictionary(g => g.Key, g => g.Select(x => (string)x.RoleName).ToList());

                var data = users.Select(user => new
                {
                    userId = user.Id,
                    fullName = user.FullName ?? "-",
                    email = user.Email ?? "-",
                    roles = roleDict.ContainsKey(user.Id) ? roleDict[user.Id] : new List<string>()
                });

                return Json(new { draw, recordsTotal = totalCount, recordsFiltered = filteredCount, data });
            }
            catch (Exception ex)
            {
                return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>(), error = ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> GetCustomersJson(int draw, int start, int length, string search = null)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
     

                var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

                // Customer rolünün ID'sini alıyoruz
                var customerRoleId = await connection.ExecuteScalarAsync<string>(
                    "SELECT Id FROM AspNetRoles WHERE Name = 'Customer'"
                );

                // 1 COUNT (TOTAL) - Sadece Customer
                var totalCount = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @CustomerRoleId
        ", new { CustomerRoleId = customerRoleId });

                // 2 COUNT (FILTERED) - Customer + arama
                var filteredCount = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @CustomerRoleId
            AND (@Search IS NULL 
                OR u.FullName LIKE @Search 
                OR u.Email LIKE @Search)
        ", new { Search = searchPattern, CustomerRoleId = customerRoleId });

                // 3 PAGED USERS - Sadece Customer
                var users = await connection.QueryAsync(@"
            SELECT 
                u.Id,
                u.FullName,
                u.Email,
                u.CustomerNumber,
                u.PhoneNumber,
                u.IsActive,
                u.CreatedAt
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @CustomerRoleId
            AND (@Search IS NULL 
                OR u.FullName LIKE @Search 
                OR u.Email LIKE @Search)
            ORDER BY u.FullName
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
        ", new
                {
                    Search = searchPattern,
                    CustomerRoleId = customerRoleId,
                    Offset = start,
                    Limit = length <= 0 ? 10 : length
                });

                var userList = users.ToList();

                var data = userList.Select(user => new
                {
                    userId = user.Id,
                    fullName = user.FullName ?? "-",
                    email = user.Email ?? "-",
                    customerNumber = user.CustomerNumber ?? "-",
                    phoneNumber = user.PhoneNumber ?? "-",
                    isActive = user.IsActive,
                    createdAt = user.CreatedAt.ToString("dd.MM.yyyy")
                });

                return Json(new
                {
                    draw,
                    recordsTotal = totalCount,
                    recordsFiltered = filteredCount,
                    data
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> GetUsersJson(int draw, int start, int length, string search = null)
        {
            var baseQuery = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrEmpty(search))
            {
                baseQuery = baseQuery.Where(u =>
                    EF.Functions.Like(u.FullName ?? "", $"%{search}%") ||
                    EF.Functions.Like(u.Email ?? "", $"%{search}%")
                    );
            }

            var totalCount = await _userManager.Users.AsNoTracking().CountAsync();   
            var filteredCount = await baseQuery.CountAsync();

            var take = length <= 0 ? 10 : length;
            var skip = start < 0 ? 0 : start;

        
            var users = await baseQuery
                .OrderBy(u => u.FullName)
                .Skip(skip)
                .Take(take)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.CreatedAt
                })
                .ToListAsync();

            var userIds = users.Select(x => x.Id).ToList();


            var rolesData = await (
     from ur in _context.UserRoles
     join r in _context.Roles on ur.RoleId equals r.Id
     where userIds.Contains(ur.UserId)
     select new
     {
         ur.UserId,
         RoleName = r.Name
     }
 ).ToListAsync();

            //  Memory grouping
            var roleDict = rolesData
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.RoleName).ToList()
                );

            //  View model
            var dataList = new List<object>();

            foreach (var user in users)
            {
                roleDict.TryGetValue(user.Id, out var roles);

                var roleBadges = roles != null && roles.Any()
                    ? string.Join("", roles.Select(r => $"<span class='badge bg-primary me-1'>{r}</span>"))
                    : "<span class='badge bg-secondary'>Rol yok</span>";

                var isSuperAdmin = roles != null && roles.Contains("SuperAdmin");

                var actionButtons = isSuperAdmin
                    ? "<span class='btn btn-sm btn-secondary disabled'>Silinemez</span>"
                    : $"<a href='/Admin/EditAdmin/{user.Id}' class='btn btn-sm btn-warning me-1'>Düzenle</a>" +
                      $"<a href='/Password/ResetUserPassword/{user.Id}' class='btn btn-sm btn-info me-1'>Şifre</a>" +
                      $"<button onclick='deleteAdmin(\"{user.Id}\", \"{user.FullName}\")' class='btn btn-sm btn-danger'>Sil</button>";

                dataList.Add(new
                {
                    fullName = user.FullName ?? "-",
                    email = user.Email ?? "-",
                    createdAt = user.CreatedAt.ToString("dd.MM.yyyy"),
                    roleBadges,
                    actionButtons
                });
            }

            return Json(new
            {
                draw,
                recordsTotal = totalCount,
                recordsFiltered = filteredCount,
                data = dataList
            });
        }


        // ================= PERSONEL İŞ TAKİP SAYFASI =================
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> PersonelTakip(int? personelId = null,
            string tab = "bekleyen",
            string customerNo = null,
            string companyName = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                // Aktif personelleri getir
                var personeller = await _unitOfWork.Personels
                    .GetQueryable()
                    .Where(p => p.IsActive)
                    .Select(p => new PersonelTakipViewModel
                    {
                        PersonelId = p.Id,
                        PersonelAdi = p.FullName,
                        Email = p.Email ?? "-",
                        Telefon = p.PhoneNumber ?? "-"
                    })
                    .ToListAsync();

                ViewBag.Personeller = personeller;
                ViewBag.SeciliPersonelId = personelId;
                ViewBag.ActiveTab = tab;
                ViewBag.CustomerNo = customerNo;
                ViewBag.CompanyName = companyName;

                if (!personelId.HasValue)
                {
                    return View(new PersonelIsTakipListViewModel());
                }

                // Seçilen personele ait repair'leri getiriyoruz
                var query = _unitOfWork.RepairItems
                    .GetQueryable()
                    .Include(r => r.AppUser)
                    .Include(r => r.Personel)
                    .Where(r => r.PersonelId == personelId.Value && !r.IsDeleted);

                // Filtreler
                if (!string.IsNullOrEmpty(customerNo))
                    query = query.Where(r => r.CustomerNumber != null && r.CustomerNumber.Contains(customerNo));

                // Firma Adı'na göre filtre (Null kontrolü ile)
                if (!string.IsNullOrEmpty(companyName))
                    query = query.Where(r => r.AppUser != null && r.AppUser.CompanyName != null && r.AppUser.CompanyName.Contains(companyName));

                // StatusId'ye göre filtreleme
                var bekleyenStatusler = new[] {
            (int)RepairStatusEnum.UrunKaydedildi,
            (int)RepairStatusEnum.ExpertizBekleniyor,
            (int)RepairStatusEnum.ExpertizeGonderildi,
            (int)RepairStatusEnum.TeklifHazirlaniyor,
            (int)RepairStatusEnum.TeklifGonderildi,
            (int)RepairStatusEnum.TeklifOnaylandi
        };
                var islemdeStatusler = new[] { (int)RepairStatusEnum.IslemeAlindi };
                var tamamlananStatusler = new[] { (int)RepairStatusEnum.Tamamlandi, (int)RepairStatusEnum.TeslimEdildi };

                // Toplam sayılar
                int totalBekleyen = await query.CountAsync(r => bekleyenStatusler.Contains(r.StatusId ?? 0));
                int totalIslemde = await query.CountAsync(r => islemdeStatusler.Contains(r.StatusId ?? 0));
                int totalTamamlanan = await query.CountAsync(r => tamamlananStatusler.Contains(r.StatusId ?? 0));

                // Seçilen tab'a göre verileri çekiyoruz
                IQueryable<RepairItem> tabQuery = tab switch
                {
                    "islemde" => query.Where(r => islemdeStatusler.Contains(r.StatusId ?? 0)),
                    "tamamlanan" => query.Where(r => tamamlananStatusler.Contains(r.StatusId ?? 0)),
                    _ => query.Where(r => bekleyenStatusler.Contains(r.StatusId ?? 0))
                };

                int totalCount = await tabQuery.CountAsync();
                int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
                int skip = (page - 1) * pageSize;

                var repairs = await tabQuery
                    .OrderByDescending(r => r.ReceivedDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(r => new PersonelIsTakipViewModel
                    {
                        RepairId = r.Id,
                        CustomerNumber = r.CustomerNumber ?? "-",
                        CompanyName = r.AppUser != null ? (r.AppUser.CompanyName ?? "-") : "-",
                        ProductName = r.ProductName ?? "-",
                        ProblemDescription = r.ProblemDescription ?? "-",
                        ReceivedDate = r.ReceivedDate,
                        StatusId = r.StatusId ?? 0,
                        StatusName = GetStatusName(r.StatusId ?? 0),
                        Price = r.Price,
                        Currency = r.Currency ?? "TRY"
                    })
                    .ToListAsync();

                var personel = await _unitOfWork.Personels.GetByIdAsync(personelId.Value);
                var viewModel = new PersonelIsTakipListViewModel
                {
                    PersonelId = personelId.Value,
                    PersonelAdi = personel?.FullName ?? "-",
                    BekleyenCount = totalBekleyen,
                    IslemdeCount = totalIslemde,
                    TamamlananCount = totalTamamlanan,
                    Bekleyenler = tab == "bekleyen" ? repairs : new List<PersonelIsTakipViewModel>(),
                    Islemdekiler = tab == "islemde" ? repairs : new List<PersonelIsTakipViewModel>(),
                    Tamamlananlar = tab == "tamamlanan" ? repairs : new List<PersonelIsTakipViewModel>()
                };

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.PageSize = pageSize;
                ViewBag.PageSizeOptions = new List<int> { 10, 20, 50, 100 };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
                if (ex.InnerException != null)
                    TempData["ErrorMessage"] += $" | Detay: {ex.InnerException.Message}";

                return View(new PersonelIsTakipListViewModel());
            }
        }

        private static string GetStatusName(int statusId)
        {
            return statusId switch
            {
                (int)RepairStatusEnum.UrunKaydedildi => "Ürün Kaydedildi",
                (int)RepairStatusEnum.ExpertizBekleniyor => "Expertiz Bekleniyor",
                (int)RepairStatusEnum.ExpertizeGonderildi => "Expertize Gönderildi",
                (int)RepairStatusEnum.TeklifHazirlaniyor => "Teklif Hazırlanıyor",
                (int)RepairStatusEnum.TeklifGonderildi => "Teklif Gönderildi",
                (int)RepairStatusEnum.TeklifOnaylandi => "Teklif Onaylandı",
                (int)RepairStatusEnum.IslemeAlindi => "İşleme Alındı",
                (int)RepairStatusEnum.Tamamlandi => "Tamamlandı",
                (int)RepairStatusEnum.TeslimEdildi => "Teslim Edildi",
                _ => "Bilinmiyor"
            };
        }

        //private static string GetStatusName(int statusId)
        //{
        //    var status = (RepairStatusEnum)statusId;
        //    var displayAttribute = status.GetType()
        //        .GetMember(status.ToString())
        //        .FirstOrDefault()
        //        ?.GetCustomAttribute<DisplayAttribute>();

        //    return displayAttribute?.Name ?? status.ToString();
        //}

    }
}