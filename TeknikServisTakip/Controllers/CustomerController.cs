using Dapper;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari")]
    public class CustomerController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMailService _mailService;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;

        public CustomerController(IUnitOfWork unitOfWork, ILogService logService, UserManager<AppUser> userManager, IMailService mailService, RoleManager<IdentityRole> roleManager, SignInManager<AppUser> signInManager, IConfiguration configuration)
        { _unitOfWork = unitOfWork; _logService = logService; _userManager = userManager; _mailService = mailService; _roleManager = roleManager; _signInManager = signInManager; _configuration = configuration; }

        // Müşteri Listesi
        public async Task<IActionResult> Index()
        {         
            return View();
        }

        // ========== MÜŞTERİ EKLE ==========
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string fullName, string email, string phoneNumber, string address,
          string city, string district, string postalCode, string identityNumber, string password, string confirmPassword, string companyName = null, string cariNo = null)
        {
            // Validasyonlar
            if (password != confirmPassword)
            {
                ViewBag.Error = "Şifreler eşleşmiyor!";
                return View();
            }

            if (string.IsNullOrEmpty(fullName) || fullName.Length < 3)
            {
                ViewBag.Error = "Ad Soyad en az 3 karakter olmalıdır!";
                return View();
            }

            if (string.IsNullOrEmpty(email) || !new EmailAddressAttribute().IsValid(email))
            {
                ViewBag.Error = "Geçerli bir e-posta adresi giriniz!";
                return View();
            }
     

            // Telefon kontrolü
            if (string.IsNullOrEmpty(phoneNumber))
            {
                ViewBag.Error = "Telefon numarası zorunludur!";
                return View();
            }

            // Geçerli mi?
            if (!phoneNumber.IsValidTurkishPhone())
            {
                ViewBag.Error = "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 05XX XXX XX XX)";
                return View();
            }

         
            phoneNumber = phoneNumber.NormalizePhone(); 

            if (string.IsNullOrEmpty(address) || address.Length < 10)
            {
                ViewBag.Error = "Adres en az 10 karakter olmalıdır!";
                return View();
            }

            if (string.IsNullOrEmpty(city) || city.Length < 2)
            {
                ViewBag.Error = "Şehir adı en az 2 karakter olmalıdır!";
                return View();
            }

            if (string.IsNullOrEmpty(district) || district.Length < 2)
            {
                ViewBag.Error = "İlçe adı en az 2 karakter olmalıdır!";
                return View();
            }

            if (!string.IsNullOrEmpty(postalCode) && !System.Text.RegularExpressions.Regex.IsMatch(postalCode, @"^\d{5}$"))
            {
                ViewBag.Error = "Posta kodu 5 haneli sayı olmalıdır!";
                return View();
            }

            if (!string.IsNullOrEmpty(identityNumber) && !System.Text.RegularExpressions.Regex.IsMatch(identityNumber, @"^\d{11}$"))
            {
                ViewBag.Error = "TC Kimlik No 11 haneli sayı olmalıdır!";
                return View();
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ViewBag.Error = "Şifre en az 6 karakter olmalıdır!";
                return View();
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ViewBag.Error = "Bu e-posta adresi zaten kullanılıyor!";
                return View();
            }

            //  CariNo benzersizlik kontrolü (eğer girilmişse)
            if (!string.IsNullOrEmpty(cariNo))
            {
                var existingCariNo = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.CariNo == cariNo);
                if (existingCariNo != null)
                {
                    ViewBag.Error = "Bu Cari No zaten kullanılıyor!";
                    return View();
                }
            }

            var customerNumber = await GenerateUniqueCustomerNumber();

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                CustomerNumber = customerNumber,
                CompanyName = companyName,  
                CariNo = string.IsNullOrEmpty(cariNo) ? null : cariNo,  
                PhoneNumber = phoneNumber,
                Address = address,
                City = city,
                District = district,
                PostalCode = postalCode,
                IdentityNumber = identityNumber,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Customer"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Customer"));
                }
                await _userManager.AddToRoleAsync(user, "Customer");


                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                            action: $"{currentUserName} - Müşteri Ekleme",
                            actionType: "Create", 
                            entityName: "Customer",
                            entityId: null,     
                            description: $"{currentUserName} tarafından yeni müşteri eklendi: {fullName} - {email}",
                            oldValues: null,       
                            newValues: new { fullName, email, phoneNumber, address, city, district, customerNumber }
                            );


                // ========== MAİL GÖNDER  ==========
                try
                {
                    string body = $@"
                                   <!DOCTYPE html>
                                   <html>
                                   <head><meta charset='utf-8'></head>
                                   <body style='font-family:Arial; text-align:center; padding:20px;'>
                                       <h2 style='color:#0d6efd;'>Teknik Servis Takip Sistemi</h2>
                                       <p>Sayın <b>{fullName}</b>,</p>
                                       <p>Sisteme başarıyla kayıt oldunuz.</p>
                                       <p><strong>Müşteri Numaranız:</strong> <span style='background:#f8fafc; padding:8px 16px; border-radius:8px; font-size:18px;'>{customerNumber}</span></p>
                                       <p>Kayıt bilgileriniz:</p>
                                       <div style='background:#f8fafc; padding:15px; border-radius:8px; margin:15px 0; text-align:left;'>
                                           <p><strong>👤 Ad Soyad:</strong> {fullName}</p>
                                           <p><strong>📧 E-posta:</strong> {email}</p>
                                           <p><strong>📱 Telefon:</strong> {phoneNumber}</p>
                                           <p><strong>📍 Adres:</strong> {address}, {district}/{city}</p>
                                           <p><strong>🆔 Müşteri No:</strong> {customerNumber}</p>
                                       </div>
                                       <p>Aşağıdaki butona tıklayarak sisteme giriş yapabilirsiniz:</p>
                                       <div style='margin:20px 0;'>
                                           <a href='{Url.Action("Login", "Account", null, Request.Scheme)}' style='display:inline-block; padding:12px 24px; background:#0d6efd; color:white; text-decoration:none; border-radius:8px;'>Giriş Yap</a>
                                       </div>
                                       <hr/>
                                       <small>Bu e-posta otomatik olarak gönderilmiştir. Lütfen cevaplamayınız.</small>
                                   </body>
                                   </html>";

                    await _mailService.SendMailAsync(email, "Teknik Servis Takip - Kayıt Başarılı", body, true);
                    Console.WriteLine($"Kayıt maili gönderildi: {email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Kayıt maili gönderilemedi: {ex.Message}");
                }
                // ===============================================

                return RedirectToAction("Index");
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        // Benzersiz Müşteri Numarası üret
        private async Task<string> GenerateUniqueCustomerNumber()
        {
            var year = DateTime.Now.Year;
            var prefix = $"MUS-{year}";

            // SADECE EN BÜYÜK NUMARAYI AL
            var lastCustomer = await _userManager.Users
                .Where(u => u.CustomerNumber != null && u.CustomerNumber.StartsWith(prefix))
                .OrderByDescending(u => u.CustomerNumber)
                .Select(u => u.CustomerNumber)
                .FirstOrDefaultAsync();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastCustomer))
            {
                var numberPart = lastCustomer.Substring(prefix.Length);
                int.TryParse(numberPart, out lastNumber);
            }

            var newNumber = lastNumber + 1;
            return $"{prefix}{newNumber:D5}";
        }

        // Müşteri Düzenleme
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Customer"))
            {
                TempData["Error"] = "Bu kullanıcı müşteri değil!";
                return RedirectToAction("Index");
            }

            return View(user);
        }

        // Müşteri Düzenleme 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string fullName, string email, string phoneNumber,
      string address, string city, string district, string postalCode, string identityNumber, string companyName = null, string cariNo = null)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

         
            if (string.IsNullOrEmpty(fullName) || fullName.Length < 3)
            {
                TempData["Error"] = "Ad Soyad en az 3 karakter olmalıdır!";
                return RedirectToAction("Edit");
            }

            if (string.IsNullOrEmpty(email) || !new EmailAddressAttribute().IsValid(email))
            {
                TempData["Error"] = "Geçerli bir e-posta adresi giriniz!";
                return RedirectToAction("Edit");
            }

            // Telefon kontrolü
            if (string.IsNullOrEmpty(phoneNumber))
            {
                TempData["Error"] = "Telefon numarası zorunludur!";
                return RedirectToAction("Edit");
            }

            // Geçerli mi? 
            if (!phoneNumber.IsValidTurkishPhone())
            {
                TempData["Error"] = "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 0500 000 00 00)";
                return RedirectToAction("Edit");
            }

         
            phoneNumber = phoneNumber.NormalizePhone(); 

            if (string.IsNullOrEmpty(address) || address.Length < 10)
            {
                TempData["Error"] = "Adres en az 10 karakter olmalıdır!";
                return RedirectToAction("Edit");
            }

            if (string.IsNullOrEmpty(city) || city.Length < 2)
            {
                TempData["Error"] = "Şehir adı en az 2 karakter olmalıdır!";
                return RedirectToAction("Edit");
            }

            if (string.IsNullOrEmpty(district) || district.Length < 2)
            {
                TempData["Error"] = "İlçe adı en az 2 karakter olmalıdır!";
                return RedirectToAction("Edit");
            }

            if (!string.IsNullOrEmpty(postalCode) && !System.Text.RegularExpressions.Regex.IsMatch(postalCode, @"^\d{5}$"))
            {
                TempData["Error"] = "Posta kodu 5 haneli sayı olmalıdır!";
                return RedirectToAction("Edit");
            }

            if (!string.IsNullOrEmpty(identityNumber) && !System.Text.RegularExpressions.Regex.IsMatch(identityNumber, @"^\d{11}$"))
            {
                TempData["Error"] = "TC Kimlik No 11 haneli sayı olmalıdır!";
                return RedirectToAction("Edit");
            }

            //  CariNo benzersizlik kontrolü (eğer değiştiyse ve boş değilse)
            if (user.CariNo != cariNo && !string.IsNullOrEmpty(cariNo))
            {
                var existingCariNo = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.CariNo == cariNo && u.Id != id);
                if (existingCariNo != null)
                {
                    TempData["Error"] = "Bu Cari No zaten başka bir müşteri tarafından kullanılıyor!";
                    return RedirectToAction("Edit", new { id = id });
                }
            }

            // Güncelleme Öncesi Müşteri Bilgileri
            var oldValues = new
            {
                fullName = user.FullName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                address = user.Address,
                city = user.City,
                district = user.District,
                postalCode = user.PostalCode,
                identityNumber = user.IdentityNumber,
                companyName = user.CompanyName,  
                cariNo = user.CariNo
            };


            // Email değiştiyse kontrol et
            if (user.Email != email)
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    TempData["Error"] = "Bu e-posta adresi başka bir kullanıcı tarafından kullanılıyor!";
                    return RedirectToAction("Edit");
                }
                user.UserName = email;
                user.Email = email;
            }


            // Kullanıcı bilgilerini güncelle
            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.Address = address;
            user.City = city;
            user.District = district;
            user.PostalCode = postalCode;
            user.IdentityNumber = identityNumber;
            user.CompanyName = companyName;  
            user.CariNo = string.IsNullOrEmpty(cariNo) ? null : cariNo;  

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {

                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Müşteri Güncelleme",
                    actionType: "Update",
                    entityName: "Customer",
                    entityId: null,
                    description: $"Müşteri profili güncellendi",
                    oldValues: oldValues,
                    newValues: new
                    {
                        fullName,
                        email,
                        phoneNumber,
                        address,
                        city,
                        district,
                        companyName,
                        cariNo
                    }
                );
                TempData["Success"] = "Profil bilgileri başarıyla güncellendi!";
                return RedirectToAction("Edit");
            }

            TempData["Error"] = "Güncelleme hatası: " + string.Join(", ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Edit");
        }

        // Müşteri Sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
                }

                var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == id);
                if (repairs != null && repairs.Any())
                {
                    return Json(new { success = false, message = "Bu müşteriye ait tamir kayıtları var! Önce tamir kayıtlarını silin." });
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    //============ İşlem Logu ============//
                    var currentUser = await _userManager.GetUserAsync(User);
                    var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                    await _logService.LogAsync(
                        action: $"{currentUserName} - Müşteri Silme",
                        actionType: "Delete",
                        entityName: "Customer",
                        entityId: null,
                        description: $"Müşteri silindi: {currentUserName}",
                        oldValues: null,
                        newValues: null
                    );

                    return Json(new { success = true, message = "Müşteri başarıyla silindi!" });
                }

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
        }

        // Müşteri Aktif pasif yapma
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCustomerStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
            }

            user.IsActive = !user.IsActive;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                //=========== İşlem Logu ============
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Müşteri Durumu Değiştirme",
                    actionType: "Update",
                    entityName: "Customer",
                    entityId: null,
                    description: $"Müşteri durumu değiştirildi: {user.Email} - {(user.IsActive ? "Aktif" : "Pasif")}",
                    oldValues: null,
                    newValues: null
                );

                return Json(new { success = true, message = $"Müşteri {(user.IsActive ? "aktif" : "pasif")} yapıldı!" });
            }

            return Json(new { success = false, message = "Güncelleme başarısız!" });
        }

        // Müşteri Detay Sayfası
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Customer"))
            {
                TempData["Error"] = "Bu kullanıcı müşteri değil!";
                return RedirectToAction("Index");
            }

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> GetCustomersJson(int draw, int start, int length, string search = null)
        {
            try
            {
                var customerRole = await _roleManager.FindByNameAsync("Customer");
                if (customerRole == null)
                {
                    return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
                }

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                // Toplam sayı sorgusu
                var countSql = @"
            SELECT COUNT(*)
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @RoleId";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { RoleId = customerRole.Id });

                // Arama filtresi varsa
                var hasSearch = !string.IsNullOrEmpty(search);
                var filterSql = hasSearch ? @"
            AND (u.CustomerNumber LIKE @Search 
                OR u.FullName LIKE @Search 
                OR u.Email LIKE @Search 
                OR u.PhoneNumber LIKE @Search)" : "";

                // Veri sorgusu
                var dataSql = $@"
            SELECT 
                u.Id,
                u.CustomerNumber,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.Address,
                u.IsActive,
                u.CreatedAt,
                u.City,
                u.District,
                u.PostalCode,
                u.IdentityNumber
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @RoleId
            {filterSql}
            ORDER BY u.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var searchPattern = hasSearch ? $"%{search}%" : null;

                var users = await connection.QueryAsync(dataSql, new
                {
                    RoleId = customerRole.Id,
                    Search = searchPattern,
                    Offset = start,
                    PageSize = length <= 0 ? 10 : length
                });

                var filteredCount = hasSearch ? users.Count() : totalCount;

                var data = users.Select(u => new
                {
                    u.Id,
                    customerNumber = u.CustomerNumber ?? "",
                    fullName = u.FullName ?? "",
                    email = u.Email ?? "",
                    phoneNumber = u.PhoneNumber ?? "",
                    addressShort = u.Address != null && u.Address.Length > 30 ? u.Address.Substring(0, 30) + "..." : (u.Address ?? ""),
                    isActive = u.IsActive,
                    createdAt = u.CreatedAt.ToString("dd.MM.yyyy"),
                    u.City,
                    u.District,
                    address = u.Address ?? "",
                    u.PostalCode,
                    u.IdentityNumber
                }).ToList();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalCount,
                    recordsFiltered = hasSearch ? filteredCount : totalCount,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
            }
        }


        // Dapper ile bilgileri alma
        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                // Customer rolünün ID'sini al
                var customerRole = await _roleManager.FindByNameAsync("Customer");
                if (customerRole == null)
                {
                    return Json(new { success = true, totalCount = 0, last30DaysCount = 0, activeCount = 0, growthRate = 0 });
                }

                var sql = @"
            SELECT 
                COUNT(*) AS TotalCount,
                SUM(CASE WHEN u.CreatedAt > @Date THEN 1 ELSE 0 END) AS Last30DaysCount,
                SUM(CASE WHEN u.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCount
            FROM AspNetUsers u
            INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
            WHERE ur.RoleId = @RoleId";

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var result = await connection.QueryFirstOrDefaultAsync(sql, new
                {
                    Date = DateTime.Now.AddDays(-30),
                    RoleId = customerRole.Id
                });

                var totalCount = result?.TotalCount ?? 0;
                var last30DaysCount = result?.Last30DaysCount ?? 0;
                var activeCount = result?.ActiveCount ?? 0;
                var growthRate = totalCount > 0 ? (last30DaysCount * 100 / totalCount) : 0;

                return Json(new
                {
                    success = true,
                    totalCount = totalCount,
                    last30DaysCount = last30DaysCount,
                    activeCount = activeCount,
                    growthRate = growthRate
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    totalCount = 0,
                    last30DaysCount = 0,
                    activeCount = 0,
                    growthRate = 0,
                    error = ex.Message
                });
            }
        }

    }
}
