using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Hubs;
using TeknikServisTakip.Services;
using WebDriverBiDi.Protocol;

namespace TeknikServisTakip.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMailService _mailService;
        private readonly ILogService _logService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IMailService mailService,
            ILogService logService,
            IHubContext<NotificationHub> hubContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _mailService = mailService;
            _logService = logService;
            _hubContext = hubContext;
        }

        // GET: Giriş Sayfası
        [HttpGet]
        public IActionResult Login(string returnUrl = null, string message = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (message == "password_changed")
            {
                ViewBag.Success = "Şifreniz başarıyla değiştirildi! Lütfen tekrar giriş yapın.";
            }
            else if (message == "password_reset")
            {
                ViewBag.Success = "Şifreniz sıfırlandı! Lütfen yeni şifrenizle giriş yapın.";
            }

            return View();
        }

        // POST: Giriş Yap
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string emailOrCustomerNo, string password, bool rememberMe = false, string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (string.IsNullOrEmpty(emailOrCustomerNo) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Müşteri No/E-posta ve şifre alanları zorunludur.";
                return View();
            }

            // Önce email ile ara, bulamazsa müşteri no ile ara
            var user = await _userManager.FindByEmailAsync(emailOrCustomerNo);

            if (user == null)
            {
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.CustomerNumber == emailOrCustomerNo);
            }

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName, password, rememberMe, false);

                if (result.Succeeded)
                {
            

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    var userRoles = await _userManager.GetRolesAsync(user);

                 
                    var adminPanelRoles = new[] { "SuperAdmin", "Admin", "Idari", "Depo", "Sevkiyat" };

                    if (userRoles.Any(r => adminPanelRoles.Contains(r)))
                    {
                        return RedirectToAction("Index", "Admin");
                    }

                    // Personel
                    if (userRoles.Contains("Personel"))
                    {
                        return RedirectToAction("Index", "PersonelDashboard");
                    }

                    // Customer 
                    if (userRoles.Contains("Customer"))
                    {
                        return RedirectToAction("Index", "CustomerDashboard");
                    }
                    
                    // Rol yoksa Home index sayfasında kal
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Müşteri No/E-posta veya şifre hatalı!";
            return View();
        }

        // GET: Kayıt Sayfası
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Kayıt Ol
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string phoneNumber, string address,
         string city, string district, string postalCode, string identityNumber, string password, string confirmPassword, string companyName = null)
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

            var customerNumber = await GenerateUniqueCustomerNumber();

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                CustomerNumber = customerNumber,
                CompanyName = companyName,
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

                // ========== MAİL GÖNDER ==========
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

                await _signInManager.SignInAsync(user, false);
                return RedirectToAction("Index", "CustomerDashboard");
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        //Benzersiz Müşteri Numarası üret 
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

        // POST: Çıkış Yap
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET: Şifremi Unuttum
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Şifre Sıfırlama Linki Gönder
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Lütfen e-posta adresinizi girin!";
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ViewBag.Error = "Bu e-posta adresine kayıtlı kullanıcı bulunamadı!";
                return View();
            }

            // Şifre sıfırlama tokenı oluştur
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Şifre sıfırlama linki oluştur
            var resetLink = Url.Action("ResetPassword", "Account",
                new { email = email, token = token }, Request.Scheme);

            // Mail içeriği
            string body = $@"
                        <!DOCTYPE html>
                        <html>
                        <head><meta charset='utf-8'></head>
                        <body style='font-family:Arial; text-align:center; padding:20px;'>
                            <h2 style='color:#0d6efd;'>Teknik Servis Takip Sistemi</h2>
                            <p>Şifre sıfırlama talebinde bulundunuz.</p>
                            <p>Aşağıdaki butona tıklayarak yeni şifre belirleyebilirsiniz:</p>
                            <div style='margin:20px 0;'>
                                <a href='{resetLink}' style='display:inline-block; padding:12px 24px; background:#0d6efd; color:white; text-decoration:none; border-radius:8px;'>Şifremi Sıfırla</a>
                            </div>
                            <p>Bu link <b>1 saat</b> geçerlidir.</p>
                            <hr/>
                            <small>Bu e-posta otomatik olarak gönderilmiştir.</small>
                        </body>
                        </html>";
                        
            // Mail gönder
            var result = await _mailService.SendMailAsync(email, "Şifre Sıfırlama Talebi", body, true);

            if (result)
            {
                ViewBag.Success = "Şifre sıfırlama linki e-posta adresinize gönderildi!";
            }
            else
            {
                ViewBag.Error = "Mail gönderilemedi! Lütfen daha sonra tekrar deneyin.";
            }

            return View();
        }

        // GET: Şifre Sıfırlama
        [HttpGet]
        public IActionResult ResetPassword(string email, string token, string message = null)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Login");
            }
            if (message == "expired")
            {
                ViewBag.Error = "Şifre sıfırlama bağlantısının süresi dolmuş! Lütfen yeni bir talep oluşturun.";
                return View("Login");
            }

            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        // POST: Şifre Sıfırlama
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string token, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "Şifreler eşleşmiyor!";
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var result = await _userManager.ResetPasswordAsync(user, token, password);
            if (result.Succeeded)
            {
                // SecurityStamp güncelle (eski tokenları geçersiz kılıyoruz)
                await _userManager.UpdateSecurityStampAsync(user);

                // Query string ile mesaj taşıyoruz
                return RedirectToAction("Login", new { message = "password_reset" });
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        // GET: Yetkisiz Erişim
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return RedirectToAction("Forbidden403", "Home");
        }



        // Excel Şablonu İndir
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExportCustomerTemplate()
        {
            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("MusteriSablonu");

                // Başlık satırı (Zorunlu alanlar kırmızı renkte)
                worksheet.Cells[1, 1].Value = "Ad Soyad *";
                worksheet.Cells[1, 2].Value = "E-posta *";
                worksheet.Cells[1, 3].Value = "Telefon *";
                worksheet.Cells[1, 4].Value = "Şifre *";
                worksheet.Cells[1, 5].Value = "Adres *";
                worksheet.Cells[1, 6].Value = "Şehir *";
                worksheet.Cells[1, 7].Value = "İlçe *";
                worksheet.Cells[1, 8].Value = "Firma Adı";
                worksheet.Cells[1, 9].Value = "Cari No";
                worksheet.Cells[1, 10].Value = "Posta Kodu";
                worksheet.Cells[1, 11].Value = "TC Kimlik No";

                // Başlık stili
                using (var range = worksheet.Cells[1, 1, 1, 11])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }

                // Zorunlu alan başlıklarını kırmızı yap
                using (var range = worksheet.Cells[1, 1, 1, 7])
                {
                    range.Style.Font.Color.SetColor(System.Drawing.Color.Red);
                }

                // Örnek veri satırı
                worksheet.Cells[2, 1].Value = "Ahmet Yılmaz";
                worksheet.Cells[2, 2].Value = "ahmet@example.com";
                worksheet.Cells[2, 3].Value = "0532 123 45 67";
                worksheet.Cells[2, 4].Value = "Abc123.";
                worksheet.Cells[2, 5].Value = "Örnek Mahallesi Atatürk Cad. No:123";
                worksheet.Cells[2, 6].Value = "İstanbul";
                worksheet.Cells[2, 7].Value = "Kadıköy";
                worksheet.Cells[2, 8].Value = "XYZ Teknoloji A.Ş.";
                worksheet.Cells[2, 9].Value = "CR-2024-001";
                worksheet.Cells[2, 10].Value = "34700";
                worksheet.Cells[2, 11].Value = "12345678901";

                // Açıklama satırları
                int descRow = 5;
                worksheet.Cells[descRow, 1].Value = "AÇIKLAMALAR:";
                worksheet.Cells[descRow, 1].Style.Font.Bold = true;

                worksheet.Cells[descRow + 1, 1].Value = "1. (*) ile işaretli alanlar ZORUNLUDUR.";
                worksheet.Cells[descRow + 2, 1].Value = "2. Telefon formatı: 05XX XXX XX XX veya 532 XXX XX XX şeklinde olmalıdır.";
                worksheet.Cells[descRow + 3, 1].Value = "3. Şifre en az 6 karakter olmalıdır. Boş bırakılırsa varsayılan 'Abc123.' kullanılır.";
                worksheet.Cells[descRow + 4, 1].Value = "4. Cari No benzersiz olmalıdır ve boşluk içeremez. Boş bırakılabilir.";
                worksheet.Cells[descRow + 5, 1].Value = "5. Firma Adı opsiyoneldir.";
                worksheet.Cells[descRow + 6, 1].Value = "6. Posta Kodu 5 haneli sayı olmalıdır (opsiyonel).";
                worksheet.Cells[descRow + 7, 1].Value = "7. TC Kimlik No 11 haneli sayı olmalıdır (opsiyonel).";
                worksheet.Cells[descRow + 8, 1].Value = "8. E-posta ve Telefon numarası daha önce kayıtlı olmamalıdır.";
                worksheet.Cells[descRow + 9, 1].Value = "9. Cari No daha önce kayıtlı olmamalıdır (eğer girilmişse).";

                // Açıklama satırı stili
                using (var range = worksheet.Cells[descRow, 1, descRow + 9, 1])
                {
                    range.Style.Font.Size = 9;
                    range.Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue);
                }

                // Sütun genişliklerini otomatik ayarla
                worksheet.Cells.AutoFitColumns();

                // İlk satırı dondur
                worksheet.View.FreezePanes(2, 1);

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"MusteriExcelSablonu_{DateTime.Now:yyyyMMdd}.xlsx";

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ImportCustomers(IFormFile excelFile)
        //{
        //    if (excelFile == null || excelFile.Length == 0)
        //    {
        //        return Json(new { success = false, message = "Lütfen bir Excel dosyası seçin!" });
        //    }

        //    var importedCount = 0;
        //    var errorList = new List<string>();
        //    var successList = new List<string>();
        //    var totalRows = 0;
        //    var usedCariNos = new HashSet<string>(); // Aynı Excel içinde CariNo tekrarı kontrolü

        //    // BAŞLANGIÇTA SON NUMARAYI BİR KERE AL
        //    var year = DateTime.Now.Year;
        //    var prefix = $"MUS-{year}";
        //    var lastCustomer = await _userManager.Users
        //        .Where(u => u.CustomerNumber != null && u.CustomerNumber.StartsWith(prefix))
        //        .OrderByDescending(u => u.CustomerNumber)
        //        .FirstOrDefaultAsync();

        //    int currentNumber = 0;
        //    if (lastCustomer != null && !string.IsNullOrEmpty(lastCustomer.CustomerNumber))
        //    {
        //        var numberPart = lastCustomer.CustomerNumber.Substring(prefix.Length);
        //        int.TryParse(numberPart, out currentNumber);
        //    }

        //    // ========== CariNo BENZERSİZLİK KONTROLÜ İÇİN VERİLERİ AL ==========
        //    // ToListAsync() kullan, sonra HashSet'e çevir
        //    var existingCariNosList = await _userManager.Users
        //        .Where(u => !string.IsNullOrEmpty(u.CariNo))
        //        .Select(u => u.CariNo)
        //        .ToListAsync();

        //    var existingCariNosSet = new HashSet<string>(existingCariNosList);

        //    using (var stream = new MemoryStream())
        //    {
        //        await excelFile.CopyToAsync(stream);
        //        using (var package = new OfficeOpenXml.ExcelPackage(stream))
        //        {
        //            var worksheet = package.Workbook.Worksheets[0];
        //            totalRows = worksheet.Dimension.Rows - 1;
        //            var rowCount = worksheet.Dimension.Rows;

        //            for (int row = 2; row <= rowCount; row++)
        //            {
        //                try
        //                {
        //                    var fullName = worksheet.Cells[row, 1]?.Text?.Trim();
        //                    var email = worksheet.Cells[row, 2]?.Text?.Trim();
        //                    var phoneNumber = worksheet.Cells[row, 3]?.Text?.Trim();
        //                    var password = worksheet.Cells[row, 4]?.Text?.Trim();
        //                    var address = worksheet.Cells[row, 5]?.Text?.Trim();
        //                    var city = worksheet.Cells[row, 6]?.Text?.Trim();
        //                    var district = worksheet.Cells[row, 7]?.Text?.Trim();
        //                    var companyName = worksheet.Cells[row, 8]?.Text?.Trim();
        //                    var cariNo = worksheet.Cells[row, 9]?.Text?.Trim();
        //                    var postalCode = worksheet.Cells[row, 10]?.Text?.Trim();
        //                    var identityNumber = worksheet.Cells[row, 11]?.Text?.Trim();

        //                    // Boş alanları null yap
        //                    companyName = string.IsNullOrEmpty(companyName) ? null : companyName;
        //                    cariNo = string.IsNullOrEmpty(cariNo) ? null : cariNo;
        //                    postalCode = string.IsNullOrEmpty(postalCode) ? null : postalCode;
        //                    identityNumber = string.IsNullOrEmpty(identityNumber) ? null : identityNumber;

        //                    // Varsayılan şifre
        //                    if (string.IsNullOrEmpty(password))
        //                    {
        //                        password = "Abc123.";
        //                    }

        //                    // ========== VALİDASYONLAR ==========
        //                    if (string.IsNullOrEmpty(fullName) || fullName.Length < 3)
        //                    {
        //                        errorList.Add($"Satır {row}: Ad Soyad en az 3 karakter olmalı!");
        //                        continue;
        //                    }

        //                    if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
        //                    {
        //                        errorList.Add($"Satır {row}: Geçerli bir email adresi girin!");
        //                        continue;
        //                    }

        //                    if (string.IsNullOrEmpty(phoneNumber))
        //                    {
        //                        errorList.Add($"Satır {row}: Telefon numarası zorunlu!");
        //                        continue;
        //                    }

        //                    phoneNumber = FormatPhoneNumber(phoneNumber);

        //                    var phoneRegex = new System.Text.RegularExpressions.Regex(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$");
        //                    if (!phoneRegex.IsMatch(phoneNumber))
        //                    {
        //                        errorList.Add($"Satır {row}: Geçerli bir telefon numarası girin! (Örn: 0532 123 4567)");
        //                        continue;
        //                    }

        //                    if (string.IsNullOrEmpty(address) || address.Length < 10)
        //                    {
        //                        errorList.Add($"Satır {row}: Adres en az 10 karakter olmalı!");
        //                        continue;
        //                    }

        //                    if (string.IsNullOrEmpty(city) || city.Length < 2)
        //                    {
        //                        errorList.Add($"Satır {row}: Şehir en az 2 karakter olmalı!");
        //                        continue;
        //                    }

        //                    if (string.IsNullOrEmpty(district) || district.Length < 2)
        //                    {
        //                        errorList.Add($"Satır {row}: İlçe en az 2 karakter olmalı!");
        //                        continue;
        //                    }

        //                    if (!string.IsNullOrEmpty(postalCode) && !System.Text.RegularExpressions.Regex.IsMatch(postalCode, @"^\d{5}$"))
        //                    {
        //                        errorList.Add($"Satır {row}: Posta kodu 5 haneli sayı olmalı!");
        //                        continue;
        //                    }

        //                    if (!string.IsNullOrEmpty(identityNumber) && !System.Text.RegularExpressions.Regex.IsMatch(identityNumber, @"^\d{11}$"))
        //                    {
        //                        errorList.Add($"Satır {row}: TC Kimlik No 11 haneli sayı olmalı!");
        //                        continue;
        //                    }

        //                    // CariNo validasyonu (eğer girilmişse)
        //                    if (!string.IsNullOrEmpty(cariNo))
        //                    {
        //                        // Boşluk kontrolü
        //                        if (cariNo.Contains(" "))
        //                        {
        //                            errorList.Add($"Satır {row}: Cari No boşluk içeremez!");
        //                            continue;
        //                        }

        //                        // Aynı Excel içinde tekrar kontrolü
        //                        if (usedCariNos.Contains(cariNo))
        //                        {
        //                            errorList.Add($"Satır {row}: '{cariNo}' Cari No aynı Excel dosyasında birden fazla kez kullanılıyor!");
        //                            continue;
        //                        }

        //                        // Database'de benzersizlik kontrolü (HashSet kullan)
        //                        if (existingCariNosSet.Contains(cariNo))
        //                        {
        //                            errorList.Add($"Satır {row}: '{cariNo}' Cari No zaten kayıtlı!");
        //                            continue;
        //                        }

        //                        usedCariNos.Add(cariNo);
        //                    }

        //                    // Email benzersizlik kontrolü
        //                    var existingUser = await _userManager.FindByEmailAsync(email);
        //                    if (existingUser != null)
        //                    {
        //                        errorList.Add($"Satır {row}: '{email}' e-posta adresi zaten kayıtlı!");
        //                        continue;
        //                    }

        //                    // Telefon benzersizlik kontrolü
        //                    var existingPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        //                    if (existingPhone != null)
        //                    {
        //                        errorList.Add($"Satır {row}: '{phoneNumber}' telefon numarası zaten kayıtlı!");
        //                        continue;
        //                    }

        //                    // Benzersiz Müşteri Numarası oluştur
        //                    currentNumber++;
        //                    var customerNumber = $"{prefix}{currentNumber:D5}";

        //                    var user = new AppUser
        //                    {
        //                        UserName = email,
        //                        Email = email,
        //                        FullName = fullName,
        //                        CustomerNumber = customerNumber,
        //                        CompanyName = companyName,
        //                        CariNo = cariNo,
        //                        PhoneNumber = phoneNumber,
        //                        Address = address,
        //                        City = city,
        //                        District = district,
        //                        PostalCode = postalCode,
        //                        IdentityNumber = identityNumber,
        //                        IsActive = true,
        //                        CreatedAt = DateTime.Now
        //                    };

        //                    var result = await _userManager.CreateAsync(user, password);

        //                    if (result.Succeeded)
        //                    {
        //                        if (!await _roleManager.RoleExistsAsync("Customer"))
        //                        {
        //                            await _roleManager.CreateAsync(new IdentityRole("Customer"));
        //                        }
        //                        await _userManager.AddToRoleAsync(user, "Customer");

        //                        importedCount++;
        //                        var infoMsg = $"{fullName} - Müşteri No: {customerNumber}";
        //                        if (!string.IsNullOrEmpty(cariNo))
        //                        {
        //                            infoMsg += $" - Cari No: {cariNo}";
        //                        }
        //                        if (!string.IsNullOrEmpty(companyName))
        //                        {
        //                            infoMsg += $" - Firma: {companyName}";
        //                        }
        //                        successList.Add(infoMsg);

        //                        // Yeni eklenen CariNo'yu hashset'e ekle (sonraki satırlarda kontrol için)
        //                        if (!string.IsNullOrEmpty(cariNo))
        //                        {
        //                            existingCariNosSet.Add(cariNo);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        //                        errorList.Add($"Satır {row}: {errors}");
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    errorList.Add($"Satır {row}: {ex.Message}");
        //                }
        //            }
        //        }
        //    }

        //    // ========== İŞLEM LOGU ==========
        //    var currentUser = await _userManager.GetUserAsync(User);
        //    var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
        //    await _logService.LogAsync(
        //        action: $"{currentUserName} - Toplu Müşteri Yükleme",
        //        actionType: "BulkCreate",
        //        entityName: "Customer",
        //        entityId: null,
        //        description: $"{importedCount} başarılı, {errorList.Count} hatalı kayıt. CariNo'lar: {string.Join(", ", usedCariNos)}",
        //        oldValues: null,
        //        newValues: new { SuccessCount = importedCount, ErrorCount = errorList.Count }
        //    );

        //    // AJAX için JSON döndür
        //    return Json(new
        //    {
        //        success = true,
        //        totalRows = totalRows,
        //        importedCount = importedCount,
        //        errorCount = errorList.Count,
        //        errors = errorList.Take(50),
        //        successList = successList.Take(20),
        //        message = $"{importedCount} / {totalRows} müşteri başarıyla eklendi!",
        //        hasErrors = errorList.Any()
        //    });
        //}


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCustomers(IFormFile excelFile, string signalRConnectionId) // signalRConnectionId parametresi eklendi
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                return Json(new { success = false, message = "Lütfen bir Excel dosyası seçin!" });
            }

            var importedCount = 0;
            var errorList = new List<string>();
            var successList = new List<string>();
            var totalRows = 0;
            var usedCariNos = new HashSet<string>(); // Aynı Excel içinde CariNo tekrarı kontrolü
            var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Aynı Excel içinde Email tekrarı kontrolü
            var usedPhones = new HashSet<string>(); // Aynı Excel içinde Telefon tekrarı kontrolü

            // BAŞLANGIÇTA SON NUMARAYI BİR KERE AL
            var year = DateTime.Now.Year;
            var prefix = $"MUS-{year}";
            var lastCustomer = await _userManager.Users
                .Where(u => u.CustomerNumber != null && u.CustomerNumber.StartsWith(prefix))
                .OrderByDescending(u => u.CustomerNumber)
                .FirstOrDefaultAsync();

            int currentNumber = 0;
            if (lastCustomer != null && !string.IsNullOrEmpty(lastCustomer.CustomerNumber))
            {
                var numberPart = lastCustomer.CustomerNumber.Substring(prefix.Length);
                int.TryParse(numberPart, out currentNumber);
            }

            // ==========================================
            // PERFORMANS OPTİMİZASYONU: DATABASE YÜKÜNÜ AZALTMA
            // Döngü içinde sürekli DB'ye gitmemek için mevcut verileri bir kere çekip belleğe (HashSet) alıyoruz.
            // ==========================================

            // 1. Mevcut Cari No'lar
            var existingCariNosList = await _userManager.Users
                .Where(u => !string.IsNullOrEmpty(u.CariNo))
                .Select(u => u.CariNo)
                .ToListAsync();
            var existingCariNosSet = new HashSet<string>(existingCariNosList);

            // 2. Mevcut E-postalar
            var existingEmailsList = await _userManager.Users
                .Where(u => !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email)
                .ToListAsync();
            var existingEmailsSet = new HashSet<string>(existingEmailsList, StringComparer.OrdinalIgnoreCase);

            // 3. Mevcut Telefonlar
            var existingPhonesList = await _userManager.Users
                .Where(u => !string.IsNullOrEmpty(u.PhoneNumber))
                .Select(u => u.PhoneNumber)
                .ToListAsync();
            var existingPhonesSet = new HashSet<string>(existingPhonesList);

            // Müşteri rolünün varlığını döngüden önce bir kez kontrol et ve yoksa oluştur
            if (!await _roleManager.RoleExistsAsync("Customer"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Customer"));
            }

            using (var stream = new MemoryStream())
            {
                await excelFile.CopyToAsync(stream);
                using (var package = new OfficeOpenXml.ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    totalRows = worksheet.Dimension.Rows - 1;
                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        // SignalR Progress Hesaplama ve Gönderme
                        if (!string.IsNullOrEmpty(signalRConnectionId) && totalRows > 0)
                        {
                            int progressPercent = (int)((row - 1) * 100 / totalRows);
                            // %100 aşamasını en son veritabanı logu bittiğinde göndermek için burada max %90'da tutuyoruz.
                            progressPercent = progressPercent > 90 ? 90 : progressPercent;
                            string progressMsg = $"Müşteriler işleniyor: {row - 1} / {totalRows}";

                            await _hubContext.Clients.Client(signalRConnectionId).SendAsync("ReceiveProgress", progressPercent, progressMsg);
                        }

                        try
                        {
                            var fullName = worksheet.Cells[row, 1]?.Text?.Trim();
                            var email = worksheet.Cells[row, 2]?.Text?.Trim();
                            var phoneNumber = worksheet.Cells[row, 3]?.Text?.Trim();
                            var password = worksheet.Cells[row, 4]?.Text?.Trim();
                            var address = worksheet.Cells[row, 5]?.Text?.Trim();
                            var city = worksheet.Cells[row, 6]?.Text?.Trim();
                            var district = worksheet.Cells[row, 7]?.Text?.Trim();
                            var companyName = worksheet.Cells[row, 8]?.Text?.Trim();
                            var cariNo = worksheet.Cells[row, 9]?.Text?.Trim();
                            var postalCode = worksheet.Cells[row, 10]?.Text?.Trim();
                            var identityNumber = worksheet.Cells[row, 11]?.Text?.Trim();

                            // Boş alanları null yap
                            companyName = string.IsNullOrEmpty(companyName) ? null : companyName;
                            cariNo = string.IsNullOrEmpty(cariNo) ? null : cariNo;
                            postalCode = string.IsNullOrEmpty(postalCode) ? null : postalCode;
                            identityNumber = string.IsNullOrEmpty(identityNumber) ? null : identityNumber;

                            // Varsayılan şifre
                            if (string.IsNullOrEmpty(password))
                            {
                                password = "Abc123.";
                            }

                            // ========== VALİDASYONLAR ==========
                            if (string.IsNullOrEmpty(fullName) || fullName.Length < 3)
                            {
                                errorList.Add($"Satır {row}: Ad Soyad en az 3 karakter olmalı!");
                                continue;
                            }

                            if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
                            {
                                errorList.Add($"Satır {row}: Geçerli bir email adresi girin!");
                                continue;
                            }

                            if (string.IsNullOrEmpty(phoneNumber))
                            {
                                errorList.Add($"Satır {row}: Telefon numarası zorunlu!");
                                continue;
                            }

                            phoneNumber = FormatPhoneNumber(phoneNumber);

                            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$");
                            if (!phoneRegex.IsMatch(phoneNumber))
                            {
                                errorList.Add($"Satır {row}: Geçerli bir telefon numarası girin! (Örn: 0532 123 4567)");
                                continue;
                            }

                            if (string.IsNullOrEmpty(address) || address.Length < 10)
                            {
                                errorList.Add($"Satır {row}: Adres en az 10 karakter olmalı!");
                                continue;
                            }

                            if (string.IsNullOrEmpty(city) || city.Length < 2)
                            {
                                errorList.Add($"Satır {row}: Şehir en az 2 karakter olmalı!");
                                continue;
                            }

                            if (string.IsNullOrEmpty(district) || district.Length < 2)
                            {
                                errorList.Add($"Satır {row}: İlçe en az 2 karakter olmalı!");
                                continue;
                            }

                            if (!string.IsNullOrEmpty(postalCode) && !System.Text.RegularExpressions.Regex.IsMatch(postalCode, @"^\d{5}$"))
                            {
                                errorList.Add($"Satır {row}: Posta kodu 5 haneli sayı olmalı!");
                                continue;
                            }

                            if (!string.IsNullOrEmpty(identityNumber) && !System.Text.RegularExpressions.Regex.IsMatch(identityNumber, @"^\d{11}$"))
                            {
                                errorList.Add($"Satır {row}: TC Kimlik No 11 haneli sayı olmalı!");
                                continue;
                            }

                            // CariNo benzersizlik kontrolü (eğer girilmişse)
                            if (!string.IsNullOrEmpty(cariNo))
                            {
                                if (cariNo.Contains(" "))
                                {
                                    errorList.Add($"Satır {row}: Cari No boşluk içeremez!");
                                    continue;
                                }

                                if (usedCariNos.Contains(cariNo))
                                {
                                    errorList.Add($"Satır {row}: '{cariNo}' Cari No aynı Excel dosyasında birden fazla kez kullanılıyor!");
                                    continue;
                                }

                                if (existingCariNosSet.Contains(cariNo))
                                {
                                    errorList.Add($"Satır {row}: '{cariNo}' Cari No zaten kayıtlı!");
                                    continue;
                                }

                                usedCariNos.Add(cariNo);
                            }

                            // Email benzersizlik kontrolü (Bellekten hızlı kontrol)
                            if (usedEmails.Contains(email))
                            {
                                errorList.Add($"Satır {row}: '{email}' e-posta adresi aynı Excel dosyasında birden fazla kez kullanılıyor!");
                                continue;
                            }
                            if (existingEmailsSet.Contains(email))
                            {
                                errorList.Add($"Satır {row}: '{email}' e-posta adresi zaten kayıtlı!");
                                continue;
                            }
                            usedEmails.Add(email);

                            // Telefon benzersizlik kontrolü (Bellekten hızlı kontrol)
                            if (usedPhones.Contains(phoneNumber))
                            {
                                errorList.Add($"Satır {row}: '{phoneNumber}' telefon numarası aynı Excel dosyasında birden fazla kez kullanılıyor!");
                                continue;
                            }
                            if (existingPhonesSet.Contains(phoneNumber))
                            {
                                errorList.Add($"Satır {row}: '{phoneNumber}' telefon numarası zaten kayıtlı!");
                                continue;
                            }
                            usedPhones.Add(phoneNumber);

                            // Benzersiz Müşteri Numarası oluştur
                            currentNumber++;
                            var customerNumber = $"{prefix}{currentNumber:D5}";

                            var user = new AppUser
                            {
                                UserName = email,
                                Email = email,
                                FullName = fullName,
                                CustomerNumber = customerNumber,
                                CompanyName = companyName,
                                CariNo = cariNo,
                                PhoneNumber = phoneNumber,
                                Address = address,
                                City = city,
                                District = district,
                                PostalCode = postalCode,
                                IdentityNumber = identityNumber,
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };

                            // Kullanıcıyı oluştur
                            var result = await _userManager.CreateAsync(user, password);

                            if (result.Succeeded)
                            {
                                // Rolü ata
                                await _userManager.AddToRoleAsync(user, "Customer");

                                importedCount++;
                                var infoMsg = $"{fullName} - Müşteri No: {customerNumber}";
                                if (!string.IsNullOrEmpty(cariNo)) infoMsg += $" - Cari No: {cariNo}";
                                if (!string.IsNullOrEmpty(companyName)) infoMsg += $" - Firma: {companyName}";

                                successList.Add(infoMsg);

                                // Döngü devam ederken yeni eklenen kayıtları belleğe hemen yazıyoruz (sonraki satırlar için eşleşmesin)
                                existingEmailsSet.Add(email);
                                existingPhonesSet.Add(phoneNumber);
                                if (!string.IsNullOrEmpty(cariNo))
                                {
                                    existingCariNosSet.Add(cariNo);
                                }
                            }
                            else
                            {
                                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                                errorList.Add($"Satır {row}: {errors}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorList.Add($"Satır {row}: {ex.Message}");
                        }
                    }
                }
            }

            // ========== İŞLEM LOGU ==========
            if (!string.IsNullOrEmpty(signalRConnectionId))
            {
                await _hubContext.Clients.Client(signalRConnectionId).SendAsync("ReceiveProgress", 95, "İşlem logları yazılıyor...");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Toplu Müşteri Yükleme",
                actionType: "BulkCreate",
                entityName: "Customer",
                entityId: null,
                description: $"{importedCount} başarılı, {errorList.Count} hatalı kayıt. CariNo'lar: {string.Join(", ", usedCariNos)}",
                oldValues: null,
                newValues: new { SuccessCount = importedCount, ErrorCount = errorList.Count }
            );

            // Son aşama: İşlem bitti!
            if (!string.IsNullOrEmpty(signalRConnectionId))
            {
                await _hubContext.Clients.Client(signalRConnectionId).SendAsync("ReceiveProgress", 100, "Tamamlandı!");
            }

            // AJAX için JSON döndür
            return Json(new
            {
                success = true,
                totalRows = totalRows,
                importedCount = importedCount,
                errorCount = errorList.Count,
                errors = errorList.Take(50),
                successList = successList.Take(20),
                message = $"{importedCount} / {totalRows} müşteri başarıyla eklendi!",
                hasErrors = errorList.Any()
            });
        }


        // Email validasyonu
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // Telefon numarasını formatla
        private string FormatPhoneNumber(string phone)
        {
            // Sadece rakamları al
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (digits.Length == 10)
            {
                return $"0{digits[0]}{digits[1]}{digits[2]} {digits[3]}{digits[4]}{digits[5]} {digits[6]}{digits[7]} {digits[8]}{digits[9]}";
            }
            if (digits.Length == 11 && digits[0] == '0')
            {
                return $"{digits[0]}{digits[1]}{digits[2]} {digits[3]}{digits[4]}{digits[5]} {digits[6]}{digits[7]} {digits[8]}{digits[9]}";
            }
            if (digits.Length == 12 && digits.StartsWith("90"))
            {
                return $"0{digits[2]}{digits[3]}{digits[4]} {digits[5]}{digits[6]}{digits[7]} {digits[8]}{digits[9]} {digits[10]}{digits[11]}";
            }

            return phone;
        }
    }
}