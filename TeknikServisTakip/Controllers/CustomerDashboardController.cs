using DataAccess.UnitOfWork;
using Entities.Concrete;
using Entities.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerDashboardController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogService _logService;

        public CustomerDashboardController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, ILogService logService)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _logService = logService;
        }

        // Müşteri Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(
                r => r.AppUserId == user.Id && r.IsDeleted == false
            );

            ViewBag.TotalRepairs = repairs.Count();
            ViewBag.PendingRepairs = repairs.Count(r => r.StatusId == 1);
            ViewBag.InProgressRepairs = repairs.Count(r => r.StatusId == 2);
            ViewBag.CompletedRepairs = repairs.Count(r => r.StatusId == 3);
            ViewBag.DeliveredRepairs = repairs.Count(r => r.StatusId == 4);

            return View(repairs);
        }

        // Profil Düzenleme 
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Sadece Customer rolü kontrolü
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Customer"))
            {
                TempData["Error"] = "Bu sayfaya erişim yetkiniz yok!";
                return RedirectToAction("Index");
            }

            return View(user);
        }


        // Profil Düzenleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string fullName, string email, string phoneNumber,
            string address, string city, string district, string postalCode, string identityNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Validasyonlar
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

            // ========== TELEFON KONTROLÜ  ==========
            if (string.IsNullOrEmpty(phoneNumber))
            {
                TempData["Error"] = "Telefon numarası zorunludur!";
                return RedirectToAction("Edit");
            }

            if (!phoneNumber.IsValidTurkishPhone())
            {
                TempData["Error"] = "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 05XX XXX XX XX)";
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

            // Güncelleme Öncesi Müşteri Bilgileri (LOG için)
            var oldValues = new
            {
                fullName = user.FullName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                address = user.Address,
                city = user.City,
                district = user.District,
                postalCode = user.PostalCode,
                identityNumber = user.IdentityNumber
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

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                //========== İşlem Logu =========//
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Müşteri Profil Güncelleme",
                    actionType: "Update",
                    entityName: "Customer",
                    entityId: null,
                    description: $"Müşteri profili güncellendi: {user.Email}",
                    oldValues: oldValues,
                    newValues: new { fullName, email, phoneNumber, address, city, district }
                );

                TempData["Success"] = "Profil bilgileriniz başarıyla güncellendi!";
                return RedirectToAction("Edit");
            }

            TempData["Error"] = "Güncelleme hatası: " + string.Join(", ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Edit");
        }


        [HttpGet]
        public async Task<IActionResult> GetRepairsData()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { repairs = new List<object>(), stats = new { totalRepairs = 0, inProgressRepairs = 0, completedRepairs = 0 } });
                }

                var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == user.Id && r.IsDeleted == false);

                var data = repairs.Select(item => new
                {
                    id = item.Id,
                    trackingCode = item.TrackingCode ?? "-",
                    productName = item.ProductName ?? "-",
                    problemDescription = string.IsNullOrEmpty(item.ProblemDescription) ? "-" : (item.ProblemDescription.Length > 50 ? item.ProblemDescription.Substring(0, 50) + "..." : item.ProblemDescription),
                    receivedDate = item.ReceivedDate.ToString("dd.MM.yyyy"),
                    statusId = item.StatusId ?? 1,
                    statusName = item.StatusId == 3 ? "Tamamlandı" : (item.StatusId == 2 ? "İşlemde" : "Beklemede"),
                    statusColor = item.StatusId == 3 ? "success" : (item.StatusId == 2 ? "info" : "warning"),
                    price = item.Price.ToString("C2")
                });

                var stats = new
                {
                    totalRepairs = repairs.Count(),
                    inProgressRepairs = repairs.Count(r => r.StatusId == 2),
                    completedRepairs = repairs.Count(r => r.StatusId == 3)
                };

                return Json(new { repairs = data, stats = stats });
            }
            catch (Exception ex)
            {
                return Json(new { repairs = new List<object>(), stats = new { totalRepairs = 0, inProgressRepairs = 0, completedRepairs = 0 } });
            }
        }


        // Tamir Detay
        public async Task<IActionResult> Details(int id)
        {
            var repair = await _unitOfWork.GetRepairWithDetailsAsync(id);
            if (repair == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (repair.AppUserId != user.Id)
            {
                TempData["Error"] = "Bu tamir size ait değil!";
                return RedirectToAction("Index");
            }

            return View(repair);
        }

        // ========== MÜŞTERİ ARŞİVİ (TESLİM EDİLEN TAMİRLER) ==========

        [HttpGet]
        public async Task<IActionResult> MyArchive()
        {
            
            return View();
        }

        // MyArchive server - side pagination

        [HttpPost]
        public async Task<IActionResult> GetMyArchiveJson(int draw, int start, int length, string search = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _unitOfWork.ArchiveRepairs
                .GetWhereAsync(a => a.AppUserId == user.Id, a => a.Personel)
                .Result.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    (a.TrackingCode != null && a.TrackingCode.Contains(search)) ||
                    (a.ProductName != null && a.ProductName.Contains(search)) ||
                    (a.ProductBrand != null && a.ProductBrand.Contains(search)) ||
                    (a.CustomerNumber != null && a.CustomerNumber.Contains(search))
                );
            }

            var totalCount = query.Count();
            var take = length <= 0 ? 10 : length;
            var skip = start < 0 ? 0 : start;

            var archives = query
                .OrderByDescending(a => a.ReceivedDate)
                .Skip(skip)
                .Take(take)
                .ToList();

            var dataList = archives.Select(a => new
            {
                id = a.Id,
                trackingCode = a.TrackingCode ?? "-",
                productName = a.ProductName ?? "-",
                productBrand = a.ProductBrand ?? "",
                productModel = a.ProductModel ?? "",
                receivedDate = a.ReceivedDate.ToString("dd.MM.yyyy"),
                deliveryDate = a.DeliveryDate?.ToString("dd.MM.yyyy") ?? "-",
                personel = a.Personel?.FullName ?? "-",
                price = a.Price.ToString("C2")
            }).ToList();

            return Json(new { draw, recordsTotal = totalCount, recordsFiltered = totalCount, data = dataList });
        }

        [HttpGet]
        public async Task<IActionResult> MyArchiveDetail(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var archive = await _unitOfWork.ArchiveRepairs
                .GetByIdWithIncludeAsync(id, a => a.AppUser, a => a.Personel);

            if (archive == null || archive.AppUserId != user.Id)
                return NotFound();

            return View(archive);
        }

     
        // Müşteri Rapor Sayfası
        public async Task<IActionResult> Reports()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CustomerName = user.FullName;
            ViewBag.CustomerNumber = user.CustomerNumber;
            return View();
        }

        // Müşteriye ait tamir istatistikleri
        [HttpGet]
        public async Task<IActionResult> GetCustomerRepairStats()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(
                r => r.AppUserId == user.Id
            );

            return Json(new
            {
                success = true,
                total = repairs.Count(),
                pending = repairs.Count(r => r.StatusId == 1),
                inProgress = repairs.Count(r => r.StatusId == 2),
                completed = repairs.Count(r => r.StatusId == 3),
                delivered = repairs.Count(r => r.StatusId == 4),
                totalRevenue = repairs.Sum(r => r.Price)
            });
        }

        // Müşteriye ait durum dağılımı
        [HttpGet]
        public async Task<IActionResult> GetCustomerStatusDistribution()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(
                r => r.AppUserId == user.Id
            );

            return Json(new
            {
                labels = new[] { "Beklemede", "İşlemde", "Tamamlandı", "Teslim Edildi" },
                data = new[]
                {
            repairs.Count(r => r.StatusId == 1),
            repairs.Count(r => r.StatusId == 2),
            repairs.Count(r => r.StatusId == 3),
            repairs.Count(r => r.StatusId == 4)
        },
                colors = new[] { "#f59e0b", "#3b82f6", "#10b981", "#6c757d" }
            });
        }

        // Müşteriye ait aylık tamir trendi
        [HttpGet]
        public async Task<IActionResult> GetCustomerMonthlyTrend(int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == user.Id);
            repairs = repairs.Where(r => r.ReceivedDate.Year == year).ToList();

            var monthlyData = new List<object>();
            for (int i = 1; i <= 12; i++)
            {
                var count = repairs.Count(r => r.ReceivedDate.Month == i);
                var revenue = repairs.Where(r => r.ReceivedDate.Month == i).Sum(r => r.Price);
                monthlyData.Add(new { month = i, monthName = GetMonthName(i), count = count, revenue = revenue });
            }

            return Json(new { year = year, data = monthlyData });
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerRepairHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == user.Id, r => r.Personel);

            var result = repairs.Select(r => new
            {
                r.Id,
                r.TrackingCode,
                r.ProductName,
                r.ProductBrand,
                r.ProductModel,
                r.ReceivedDate,
                r.DeliveryDate,
                status = ((RepairStatusEnum)(r.StatusId ?? 1)).GetDisplayName(),
                statusId = r.StatusId ?? 1, 
                price = r.Price,
                personel = r.Personel?.FullName ?? "Atanmamış"
            }).OrderByDescending(r => r.ReceivedDate);

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetCustomerRepairHistoryServerSide()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { draw = draw, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
                }

                var query = _unitOfWork.RepairItems.GetQueryable();
                query = query.Where(r => r.AppUserId == user.Id);
                query = query.Include(r => r.Personel);

                // Arama
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(r => (r.TrackingCode != null && r.TrackingCode.Contains(searchValue)) ||
                                              (r.ProductName != null && r.ProductName.Contains(searchValue)));
                }

                int totalRecords = await query.CountAsync();
                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var data = await query
                    .OrderByDescending(r => r.ReceivedDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(r => new
                    {
                        id = r.Id,
                        trackingCode = r.TrackingCode ?? "-",
                        productName = r.ProductName ?? "-",
                        productBrand = r.ProductBrand ?? "-",
                        productModel = r.ProductModel ?? "-",
                        receivedDate = r.ReceivedDate.ToString("dd.MM.yyyy"),
                        deliveryDate = r.DeliveryDate != null ? r.DeliveryDate.Value.ToString("dd.MM.yyyy") : "-",
                        status = ((RepairStatusEnum)(r.StatusId ?? 1)).GetDisplayName(),
                        price = r.Price.ToString("C2"),
                        personel = r.Personel != null ? r.Personel.FullName : "Atanmamış"
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return Json(new { draw = Request.Form["draw"].FirstOrDefault(), recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
            }
        }


        // Müşteri PDF Raporu 
        [HttpGet]
        public async Task<IActionResult> DownloadRepairPdf(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var repair = await _unitOfWork.GetRepairWithDetailsAsync(id);

            if (repair == null || repair.AppUserId != user.Id)
            {
                return NotFound();
            }

            var html = GenerateCustomerRepairHtml(repair);

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions { Top = "10mm", Bottom = "10mm", Left = "10mm", Right = "10mm" }
            });

            string fileName = $"TamirDetayi_{repair.TrackingCode}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // Müşteri tamir geçmişi Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportCustomerRepairsExcel()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == user.Id, r => r.Personel);

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("TamirGeçmişim");

                worksheet.Cells[1, 1].Value = $"MÜŞTERİ TAMİR GEÇMİŞİ - {user.FullName}";
                worksheet.Cells[1, 1, 1, 8].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;

                worksheet.Cells[3, 1].Value = "Takip Kodu";
                worksheet.Cells[3, 2].Value = "Ürün Adı";
                worksheet.Cells[3, 3].Value = "Marka";
                worksheet.Cells[3, 4].Value = "Model";
                worksheet.Cells[3, 5].Value = "Geliş Tarihi";
                worksheet.Cells[3, 6].Value = "Teslim Tarihi";
                worksheet.Cells[3, 7].Value = "Durum";
                worksheet.Cells[3, 8].Value = "Ücret";

                int row = 4;
                decimal total = 0;
                foreach (var item in repairs)
                {
                    worksheet.Cells[row, 1].Value = item.TrackingCode;
                    worksheet.Cells[row, 2].Value = item.ProductName;
                    worksheet.Cells[row, 3].Value = item.ProductBrand ?? "-";
                    worksheet.Cells[row, 4].Value = item.ProductModel ?? "-";
                    worksheet.Cells[row, 5].Value = item.ReceivedDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 6].Value = item.DeliveryDate?.ToString("dd.MM.yyyy") ?? "-";
                    worksheet.Cells[row, 7].Value = ((RepairStatusEnum)(item.StatusId ?? 1)).GetDisplayName();
                    worksheet.Cells[row, 8].Value = item.Price;
                    total += item.Price;
                    row++;
                }

                worksheet.Cells[row, 7].Value = "TOPLAM:";
                worksheet.Cells[row, 8].Value = total;

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TamirGecmisim_{DateTime.Now:yyyyMMdd}.xlsx");
            }
        }

        private string GetMonthName(int month)
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

        private string GenerateCustomerRepairHtml(RepairItem repair)
        {
            return $@"
                      <!DOCTYPE html>
                      <html>
                      <head>
                          <meta charset='UTF-8'>
                          <title>Tamir Detayı</title>
                          <style>
                              body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }}
                              h1 {{ color: #0d6efd; text-align: center; }}
                              .info {{ background: #f8fafc; padding: 15px; border-radius: 10px; margin-bottom: 20px; }}
                              table {{ width: 100%; border-collapse: collapse; }}
                              th {{ background: #1e293b; color: white; padding: 10px; text-align: left; }}
                              td {{ padding: 8px; border-bottom: 1px solid #ddd; }}
                          </style>
                      </head>
                      <body>
                          <h1>Tamir Detayı</h1>
                          <div class='info'>
                              <p><strong>Takip Kodu:</strong> {repair.TrackingCode}</p>
                              <p><strong>Ürün:</strong> {repair.ProductName}</p>
                              <p><strong>Durum:</strong> {((RepairStatusEnum)(repair.StatusId ?? 1)).GetDisplayName()}</p>
                              <p><strong>Ücret:</strong> {repair.Price:C2}</p>
                          </div>
                          <div class='footer'>Teknik Servis Takip Sistemi</div>
                      </body>
                      </html>";
        }


        // Excel Export ArchiveDetail excel export 1. Tasarım
        //public async Task<IActionResult> ExportArchiveToExcel(int id)
        //{
        //    var archive = await _unitOfWork.ArchiveRepairs
        //        .GetByIdWithIncludeAsync(id, a => a.Personel);

        //    if (archive == null) return NotFound();

        //    using (var package = new ExcelPackage())
        //    {
        //        var worksheet = package.Workbook.Worksheets.Add("Tamir Detay Raporu");

        //        // ========== BAŞLIK BÖLÜMÜ ==========
        //        // Ana Başlık
        //        worksheet.Cells["A1:C1"].Merge = true;
        //        worksheet.Cells["A1"].Value = "TEKNİK SERVİS TAKİP SİSTEMİ";
        //        worksheet.Cells["A1"].Style.Font.Size = 20;
        //        worksheet.Cells["A1"].Style.Font.Bold = true;
        //        worksheet.Cells["A1"].Style.Font.Color.SetColor(Color.White);
        //        worksheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        //        worksheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(13, 110, 253));
        //        worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        //        worksheet.Cells["A1"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        //        worksheet.Row(1).Height = 35;

        //        // Alt Başlık
        //        worksheet.Cells["A2:C2"].Merge = true;
        //        worksheet.Cells["A2"].Value = $"Tamir Detay Raporu - {DateTime.Now:dd.MM.yyyy HH:mm}";
        //        worksheet.Cells["A2"].Style.Font.Size = 11;
        //        worksheet.Cells["A2"].Style.Font.Italic = true;
        //        worksheet.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        //        worksheet.Row(2).Height = 25;

        //        // Boşluk
        //        worksheet.Row(3).Height = 10;

        //        // ========== TAMİR BİLGİLERİ KARTI ==========
        //        int startRow = 4;
        //        int colLabel = 1;
        //        int colValue = 2;
        //        int colIcon = 3;

        //        // Kart başlığı
        //        worksheet.Cells[startRow, colLabel, startRow, colIcon].Merge = true;
        //        worksheet.Cells[startRow, colLabel].Value = "🔧 TAMİR BİLGİLERİ";
        //        worksheet.Cells[startRow, colLabel].Style.Font.Size = 14;
        //        worksheet.Cells[startRow, colLabel].Style.Font.Bold = true;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.PatternType = ExcelFillStyle.Solid;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(240, 248, 255));
        //        worksheet.Row(startRow).Height = 25;
        //        startRow++;

        //        // Veri satırları
        //        var dataRows = new List<(string Label, string Value, string Icon)>
        //{
        //    ("Takip Kodu", archive.TrackingCode, "🏷️"),
        //    ("Durum", "Teslim Edildi", "✅"),
        //    ("Ürün Adı", archive.ProductName, "📦"),
        //    ("Marka / Model", $"{archive.ProductBrand} {archive.ProductModel}".Trim(), "🏭"),
        //    ("Seri No", string.IsNullOrEmpty(archive.SerialNumber) ? "-" : archive.SerialNumber, "🔢"),
        //    ("Geliş Tarihi", archive.ReceivedDate.ToString("dd.MM.yyyy HH:mm"), "📅"),
        //    ("Teslim Tarihi", archive.DeliveryDate?.ToString("dd.MM.yyyy HH:mm") ?? "-", "🚚"),
        //    ("Personel", archive.Personel?.FullName ?? "-", "👤"),
        //    ("Ücret", archive.Price.ToString("C2"), "💰"),
        //    ("Arıza Açıklaması", string.IsNullOrEmpty(archive.ProblemDescription) ? "-" : archive.ProblemDescription, "⚠️"),
        //    ("Personel Notu", string.IsNullOrEmpty(archive.InternalNote) ? "-" : archive.InternalNote, "📝")
        //};

        //        foreach (var row in dataRows)
        //        {
        //            // İkon sütunu (opsiyonel)
        //            worksheet.Cells[startRow, colIcon].Value = row.Icon;
        //            worksheet.Cells[startRow, colIcon].Style.Font.Size = 14;

        //            // Label sütunu
        //            worksheet.Cells[startRow, colLabel].Value = row.Label;
        //            worksheet.Cells[startRow, colLabel].Style.Font.Bold = true;
        //            worksheet.Cells[startRow, colLabel].Style.Font.Size = 11;
        //            worksheet.Cells[startRow, colLabel].Style.Fill.PatternType = ExcelFillStyle.Solid;
        //            worksheet.Cells[startRow, colLabel].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 249, 250));

        //            // Value sütunu
        //            worksheet.Cells[startRow, colValue].Value = row.Value;
        //            worksheet.Cells[startRow, colValue].Style.Font.Size = 11;

        //            // Ücret satırını kalın yap
        //            if (row.Label == "Ücret")
        //            {
        //                worksheet.Cells[startRow, colValue].Style.Font.Bold = true;
        //                worksheet.Cells[startRow, colValue].Style.Font.Color.SetColor(Color.FromArgb(25, 135, 84));
        //            }

        //            startRow++;
        //        }

        //        // Boşluk
        //        startRow++;

        //        // ========== ARIZA AÇIKLAMASI DETAY ==========
        //        worksheet.Cells[startRow, colLabel, startRow, colIcon].Merge = true;
        //        worksheet.Cells[startRow, colLabel].Value = "⚠️ ARIZA AÇIKLAMASI DETAYI";
        //        worksheet.Cells[startRow, colLabel].Style.Font.Size = 13;
        //        worksheet.Cells[startRow, colLabel].Style.Font.Bold = true;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.PatternType = ExcelFillStyle.Solid;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 205));
        //        startRow++;

        //        worksheet.Cells[startRow, colLabel, startRow, colIcon].Merge = true;
        //        worksheet.Cells[startRow, colLabel].Value = string.IsNullOrEmpty(archive.ProblemDescription) ? "-" : archive.ProblemDescription;
        //        worksheet.Cells[startRow, colLabel].Style.WrapText = true;
        //        worksheet.Cells[startRow, colLabel].Style.Font.Size = 11;
        //        worksheet.Row(startRow).Height = 60;
        //        startRow++;

        //        // ========== PERSONEL NOTU DETAY ==========
        //        startRow++;
        //        worksheet.Cells[startRow, colLabel, startRow, colIcon].Merge = true;
        //        worksheet.Cells[startRow, colLabel].Value = "📝 PERSONEL NOTU";
        //        worksheet.Cells[startRow, colLabel].Style.Font.Size = 13;
        //        worksheet.Cells[startRow, colLabel].Style.Font.Bold = true;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.PatternType = ExcelFillStyle.Solid;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(227, 242, 253));
        //        startRow++;

        //        worksheet.Cells[startRow, colLabel, startRow, colIcon].Merge = true;
        //        worksheet.Cells[startRow, colLabel].Value = string.IsNullOrEmpty(archive.InternalNote) ? "-" : archive.InternalNote;
        //        worksheet.Cells[startRow, colLabel].Style.WrapText = true;
        //        worksheet.Cells[startRow, colLabel].Style.Font.Size = 11;
        //        worksheet.Row(startRow).Height = 50;
        //        startRow++;

        //        // ========== FOOTER ==========
        //        startRow++;
        //        worksheet.Cells[startRow, colLabel, startRow, colIcon].Merge = true;
        //        worksheet.Cells[startRow, colLabel].Value = $"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm} | Teknik Servis Takip Sistemi | Tüm Hakları Saklıdır. © {DateTime.Now.Year}";
        //        worksheet.Cells[startRow, colLabel].Style.Font.Size = 9;
        //        worksheet.Cells[startRow, colLabel].Style.Font.Italic = true;
        //        worksheet.Cells[startRow, colLabel].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.PatternType = ExcelFillStyle.Solid;
        //        worksheet.Cells[startRow, colLabel].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(240, 248, 255));
        //        worksheet.Row(startRow).Height = 25;

        //        // ========== KOLON GENİŞLİKLERİ ==========
        //        worksheet.Column(colIcon).Width = 5;
        //        worksheet.Column(colLabel).Width = 25;
        //        worksheet.Column(colValue).Width = 60;

        //        // ========== TÜM TABLOYA BORDER EKLE ==========
        //        var tableRange = worksheet.Cells[4, colLabel, startRow - 1, colValue];
        //        tableRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        //        tableRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        //        tableRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        //        tableRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        //        tableRange.Style.Border.Top.Color.SetColor(Color.LightGray);
        //        tableRange.Style.Border.Bottom.Color.SetColor(Color.LightGray);
        //        tableRange.Style.Border.Left.Color.SetColor(Color.LightGray);
        //        tableRange.Style.Border.Right.Color.SetColor(Color.LightGray);

        //        // ========== YAZDIRMA AYARLARI ==========
        //        worksheet.PrinterSettings.Orientation = eOrientation.Portrait;
        //        worksheet.PrinterSettings.FitToPage = true;
        //        worksheet.PrinterSettings.FitToWidth = 1;
        //        worksheet.PrinterSettings.FitToHeight = 0;
        //        worksheet.PrinterSettings.LeftMargin = 0.5m;
        //        worksheet.PrinterSettings.RightMargin = 0.5m;
        //        worksheet.PrinterSettings.TopMargin = 0.5m;
        //        worksheet.PrinterSettings.BottomMargin = 0.5m;

        //        var fileBytes = package.GetAsByteArray();
        //        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //            $"Tamir_Detay_{archive.TrackingCode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        //    }
        //}

        // Excel Export ArchiveDetail excel export 2. Tasarım
        public async Task<IActionResult> ExportArchiveToExcel(int id)
        {
            var archive = await _unitOfWork.ArchiveRepairs
                .GetByIdWithIncludeAsync(id, a => a.Personel);

            if (archive == null) return NotFound();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Tamir Detay Raporu");

                // ========== GENEL AYARLAR ==========
                worksheet.View.ShowGridLines = false; // Izgara çizgilerini kaldır (Modern görünüm)
                worksheet.Cells.Style.Font.Name = "Segoe UI"; // Daha kurumsal bir font
                worksheet.Cells.Style.Font.Size = 10;

                // Kolon Genişlikleri
                worksheet.Column(1).Width = 25; // Etiket sütunu
                worksheet.Column(2).Width = 45; // Değer sütunu
                worksheet.Column(3).Width = 15; // Ekstra alan / İkon

                // ========== ÜST BANNER (BAŞLIK) ==========
                using (var range = worksheet.Cells["A1:C1"])
                {
                    range.Merge = true;
                    range.Value = "TEKNİK SERVİS TAKİP SİSTEMİ";
                    range.Style.Font.Size = 18;
                    range.Style.Font.Bold = true;
                    range.Style.Font.Color.SetColor(Color.White);
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(33, 37, 41)); // Antrasit Siyah
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                worksheet.Row(1).Height = 45;

                // Alt Başlık (Rapor Tarihi)
                using (var range = worksheet.Cells["A2:C2"])
                {
                    range.Merge = true;
                    range.Value = $"ARŞİV KAYIT RAPORU • Üretim Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    range.Style.Font.Size = 9;
                    range.Style.Font.Italic = true;
                    range.Style.Font.Color.SetColor(Color.Gray);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                worksheet.Row(2).Height = 20;

                int currentRow = 4;

                // ========== YARDIMCI METOD: VERİ SATIRI EKLEME ==========
                void AddInfoRow(string label, string value, bool isPrice = false)
                {
                    // Etiket Hücresi
                    var cellLabel = worksheet.Cells[currentRow, 1];
                    cellLabel.Value = label;
                    cellLabel.Style.Font.Bold = true;
                    cellLabel.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cellLabel.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 249, 250)); // Açık Gri
                    cellLabel.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));

                    // Değer Hücresi
                    var cellValue = worksheet.Cells[currentRow, 2, currentRow, 3];
                    cellValue.Merge = true;
                    cellValue.Value = value;
                    cellValue.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    cellValue.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    cellValue.Style.Indent = 1;

                    if (isPrice)
                    {
                        cellValue.Style.Font.Bold = true;
                        cellValue.Style.Font.Color.SetColor(Color.FromArgb(21, 115, 71)); // Yeşil Tutar
                    }

                    worksheet.Row(currentRow).Height = 22;
                    currentRow++;
                }

                // ========== BİLGİLERİ DOLDUR ==========
                AddInfoRow("Takip Kodu", $"# {archive.TrackingCode}");
                AddInfoRow("Durum", "Teslim Edildi (Arşivlenmiş)");
                AddInfoRow("Ürün Bilgisi", $"{archive.ProductName} {archive.ProductBrand} {archive.ProductModel}");
                AddInfoRow("Seri Numarası", string.IsNullOrEmpty(archive.SerialNumber) ? "-" : archive.SerialNumber);
                AddInfoRow("Geliş Tarihi", archive.ReceivedDate.ToString("dd.MM.yyyy HH:mm"));
                AddInfoRow("Teslim Tarihi", archive.DeliveryDate?.ToString("dd.MM.yyyy HH:mm") ?? "-");
                AddInfoRow("Sorumlu Personel", archive.Personel?.FullName ?? "-");
                AddInfoRow("Toplam Servis Ücreti", archive.Price.ToString("C2"), true);

                // ========== GENİŞ METİN ALANLARI (ARIZA & NOT) ==========
                currentRow++;

                void AddLargeBox(string title, string content, Color themeColor)
                {
                    // Başlık
                    var titleRange = worksheet.Cells[currentRow, 1, currentRow, 3];
                    titleRange.Merge = true;
                    titleRange.Value = title;
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Color.SetColor(Color.FromArgb(68, 68, 68));
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(themeColor);
                    titleRange.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    currentRow++;

                    // İçerik
                    var contentRange = worksheet.Cells[currentRow, 1, currentRow, 3];
                    contentRange.Merge = true;
                    contentRange.Value = content;
                    contentRange.Style.WrapText = true;
                    contentRange.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    contentRange.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    worksheet.Row(currentRow).Height = 60; // Metne göre yükseklik
                    currentRow += 2;
                }

                AddLargeBox("⚠️ Müşteri Arıza Açıklaması", string.IsNullOrEmpty(archive.ProblemDescription) ? "-" : archive.ProblemDescription, Color.FromArgb(255, 243, 205));
                AddLargeBox("📝 Teknik Servis İşlem Notları", string.IsNullOrEmpty(archive.InternalNote) ? "-" : archive.InternalNote, Color.FromArgb(227, 242, 253));

                // ========== FOOTER ==========
                var footerRange = worksheet.Cells[currentRow, 1, currentRow, 3];
                footerRange.Merge = true;
                footerRange.Value = "Bu rapor Teknik Servis Takip Sistemi üzerinden dijital olarak oluşturulmuştur.";
                footerRange.Style.Font.Size = 8;
                footerRange.Style.Font.Italic = true;
                footerRange.Style.Font.Color.SetColor(Color.Gray);
                footerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // ========== YAZDIRMA AYARLARI ==========
                worksheet.PrinterSettings.Orientation = eOrientation.Portrait;
                worksheet.PrinterSettings.FitToPage = true;
                worksheet.PrinterSettings.FitToWidth = 1;

                var fileBytes = package.GetAsByteArray();
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Servis_Raporu_{archive.TrackingCode}.xlsx");
            }
        }

        // Yazdır için ArchiveDetail yazdır metodu
        public async Task<IActionResult> PrintArchiveDetail(int id)
        {
            var archive = await _unitOfWork.ArchiveRepairs
                .GetByIdWithIncludeAsync(id, a => a.Personel);

            if (archive == null) return NotFound();
            return View("PrintArchiveDetail", archive);
        }

    }
}