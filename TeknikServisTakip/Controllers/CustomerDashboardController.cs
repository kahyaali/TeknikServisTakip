using DataAccess.Context;
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

        private readonly AppDbContext _context;

        public CustomerDashboardController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, ILogService logService, AppDbContext context)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _logService = logService;
            _context = context;
        }

        // Müşteri Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(
                r => r.AppUserId == user.Id && r.IsDeleted == false
            );

            ViewBag.TotalRepairs = repairs.Count();

            // 1 veya 2: Bekleyen Aşamalar
            ViewBag.PendingRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.UrunKaydedildi ||
                                                        r.StatusId == (int)RepairStatusEnum.ExpertizBekleniyor);

            // 3, 4, 5, 6 veya 7: Süreçteki Aşamalar
            ViewBag.InProgressRepairs = repairs.Count(r => r.StatusId >= (int)RepairStatusEnum.ExpertizeGonderildi &&
                                                          r.StatusId <= (int)RepairStatusEnum.IslemeAlindi);

            // 8: Tamamlanan Aşama
            ViewBag.CompletedRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi);

            // 9: Teslim Edilen Aşama
            ViewBag.DeliveredRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.TeslimEdildi);

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

                var data = repairs.Select(item => {
                    int currentStatusId = item.StatusId ?? 1;

                    // Enum adını ve Display Name özelliğini güvenle alıyoruz 
                    var enumStatus = (RepairStatusEnum)currentStatusId;
                    string displayName = enumStatus.ToString();

                 
                    var fieldInfo = enumStatus.GetType().GetField(enumStatus.ToString());
                    if (fieldInfo != null)
                    {
                        var displayAttr = fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false).FirstOrDefault() as DisplayAttribute;
                        if (displayAttr != null) displayName = displayAttr.Name;
                    }

                    // Duruma göre Bootstrap renk kodlarını dinamikleştiriyoruz 
                    string color = "warning"; // Varsayılan: Beklemede (Sarı)
                    if (currentStatusId == (int)RepairStatusEnum.Tamamlandi)
                    {
                        color = "success"; // Yeşil
                    }
                    else if (currentStatusId == (int)RepairStatusEnum.TeslimEdildi)
                    {
                        color = "secondary"; // Gri
                    }
                    else if (currentStatusId >= (int)RepairStatusEnum.ExpertizeGonderildi && currentStatusId <= (int)RepairStatusEnum.IslemeAlindi)
                    {
                        color = "info"; // Mavi (İşlemde)
                    }

                    var itemCurrency = item.Currency ?? "TRY";
                    var itemCurrencySymbol = TeknikServisTakip.Helpers.CurrencyHelper.GetSymbol(itemCurrency);

                    return new
                    {
                        id = item.Id,
                        trackingCode = item.TrackingCode ?? "-",
                        productName = item.ProductName ?? "-",
                        problemDescription = string.IsNullOrEmpty(item.ProblemDescription) ? "-" : (item.ProblemDescription.Length > 50 ? item.ProblemDescription.Substring(0, 50) + "..." : item.ProblemDescription),
                        receivedDate = item.ReceivedDate.ToString("dd.MM.yyyy"),
                        statusId = currentStatusId,
                        statusName = displayName,
                        statusColor = color,
                        price = $"{itemCurrencySymbol} {item.Price:N2} ({itemCurrency})"
                    };
                });

                var stats = new
                {
                    totalRepairs = repairs.Count(),
                    // 3 ile 7 arasındaki tüm ara süreçleri "İşlemde" sayıyoruz
                    inProgressRepairs = repairs.Count(r => r.StatusId >= (int)RepairStatusEnum.ExpertizeGonderildi && r.StatusId <= (int)RepairStatusEnum.IslemeAlindi),
                    // 8: Tamamlandı
                    completedRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi)
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
                price = a.Price.ToString("N2"),
                currency = a.Currency ?? "TRY",
                currencySymbol = CurrencyHelper.GetSymbol(a.Currency ?? "TRY")
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

            // Kullanılan malzemeleri getir (OriginalRepairId ile)
            var materials = await _unitOfWork.RepairMaterials
                .GetWhereAsync(m => m.RepairId == archive.OriginalRepairId, m => m.Product);

            ViewBag.Materials = materials.OrderByDescending(m => m.UsedAt).ToList();

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
                r => r.AppUserId == user.Id && r.IsDeleted == false
            );

   
            int total = repairs.Count();

            // UrunKaydedildi (1) veya ExpertizBekleniyor (2)
            int pending = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.UrunKaydedildi ||
                                             r.StatusId == (int)RepairStatusEnum.ExpertizBekleniyor);

            // ExpertizeGonderildi (3) ile IslemeAlindi (7) arasındaki tüm ara süreçler
            int inProgress = repairs.Count(r => r.StatusId >= (int)RepairStatusEnum.ExpertizeGonderildi &&
                                               r.StatusId <= (int)RepairStatusEnum.IslemeAlindi);

            // Tamamlandi (8)
            int completed = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi);

            // TeslimEdildi (9)
            int delivered = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.TeslimEdildi);

            // Para birimlerine göre harcamaları grupla
            var revenueByCurrency = repairs
                .GroupBy(r => r.Currency ?? "TRY")
                .Select(g => new {
                    Currency = g.Key,
                    Total = g.Sum(r => r.Price),
                    Avg = g.Count() > 0 ? g.Sum(r => r.Price) / g.Count() : 0
                }).ToList();

            return Json(new
            {
                success = true,
                total = total,
                pending = pending,
                inProgress = inProgress,
                completed = completed,
                delivered = delivered,
                revenueData = revenueByCurrency
            });
        }

        // Müşteriye ait durum dağılımı
        [HttpGet]
        public async Task<IActionResult> GetCustomerStatusDistribution()
        {
            var user = await _userManager.GetUserAsync(User);
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(
                r => r.AppUserId == user.Id && r.IsDeleted == false
            );

            return Json(new
            {
                labels = new[] { "Beklemede", "İşlemde", "Tamamlandı", "Teslim Edildi" },
                data = new[]
                {
            // Beklemede (1 ve 2)
            repairs.Count(r => r.StatusId == (int)RepairStatusEnum.UrunKaydedildi || r.StatusId == (int)RepairStatusEnum.ExpertizBekleniyor),
            
            // İşlemde (3 ile 7 arası)
            repairs.Count(r => r.StatusId >= (int)RepairStatusEnum.ExpertizeGonderildi && r.StatusId <= (int)RepairStatusEnum.IslemeAlindi),
            
            // Tamamlandı (8)
            repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi),
            
            // Teslim Edildi (9)
            repairs.Count(r => r.StatusId == (int)RepairStatusEnum.TeslimEdildi)
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
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == user.Id && r.IsDeleted == false);
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
            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == user.Id && r.IsDeleted == false, r => r.Personel);

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
                currency = r.Currency ?? "TRY",
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
                query = query.Where(r => r.AppUserId == user.Id && r.IsDeleted == false);
                query = query.Include(r => r.Personel);

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(r => (r.TrackingCode != null && r.TrackingCode.Contains(searchValue)) ||
                                              (r.ProductName != null && r.ProductName.Contains(searchValue)));
                }

                int totalRecords = await query.CountAsync();
                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var rawData = await query
                    .OrderByDescending(r => r.ReceivedDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                var data = rawData.Select(r => {
                    int currentStatusId = r.StatusId ?? 1;

                    // Tabloda "Beklemede", "İşlemde" gibi üst grupları göstermek istersen JS'deki badge sınıfları için eşliyoruz
                    string displayGroupName = "Beklemede";
                    if (currentStatusId == (int)RepairStatusEnum.Tamamlandi)
                    {
                        displayGroupName = "Tamamlandı";
                    }
                    else if (currentStatusId == (int)RepairStatusEnum.TeslimEdildi)
                    {
                        displayGroupName = "Teslim Edildi";
                    }
                    else if (currentStatusId >= (int)RepairStatusEnum.ExpertizeGonderildi && currentStatusId <= (int)RepairStatusEnum.IslemeAlindi)
                    {
                        displayGroupName = "İşlemde";
                    }

                    return new
                    {
                        id = r.Id,
                        trackingCode = r.TrackingCode ?? "-",
                        productName = r.ProductName ?? "-",
                        productBrand = r.ProductBrand ?? "-",
                        productModel = r.ProductModel ?? "-",
                        receivedDate = r.ReceivedDate.ToString("dd.MM.yyyy"),
                        deliveryDate = r.DeliveryDate != null ? r.DeliveryDate.Value.ToString("dd.MM.yyyy") : "-",
                        status = displayGroupName, // JS tarafındaki badge-pending, badge-progress şartlarına uysun diye bunu gönderiyoruz
                        price = r.Price,
                        currency = r.Currency ?? "TRY",
                        personel = r.Personel != null ? r.Personel.FullName : "Atanmamış"
                    };
                }).ToList();

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

                // Başlık Alanı
                worksheet.Cells[1, 1].Value = $"MÜŞTERİ TAMİR GEÇMİŞİ - {user.FullName}";
                worksheet.Cells[1, 1, 1, 8].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;

                // Tablo Sütun Başlıkları
                worksheet.Cells[3, 1].Value = "Takip Kodu";
                worksheet.Cells[3, 2].Value = "Ürün Adı";
                worksheet.Cells[3, 3].Value = "Marka";
                worksheet.Cells[3, 4].Value = "Model";
                worksheet.Cells[3, 5].Value = "Geliş Tarihi";
                worksheet.Cells[3, 6].Value = "Teslim Tarihi";
                worksheet.Cells[3, 7].Value = "Durum";
                worksheet.Cells[3, 8].Value = "Ücret";

                // Tablo Başlık Stil Giydirme
                for (int col = 1; col <= 8; col++)
                {
                    worksheet.Cells[3, col].Style.Font.Bold = true;
                    worksheet.Cells[3, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[3, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                int row = 4;
                // Para birimlerinin toplamlarını ayrı ayrı hafızada tutmak için bir sözlük (Dictionary) oluşturuyoruz 
                var currencyTotals = new Dictionary<string, decimal>();

                foreach (var item in repairs)
                {
                    string currency = item.Currency ?? "TRY";
                    string currencySymbol = TeknikServisTakip.Helpers.CurrencyHelper.GetSymbol(currency);

                    worksheet.Cells[row, 1].Value = item.TrackingCode;
                    worksheet.Cells[row, 2].Value = item.ProductName;
                    worksheet.Cells[row, 3].Value = item.ProductBrand ?? "-";
                    worksheet.Cells[row, 4].Value = item.ProductModel ?? "-";
                    worksheet.Cells[row, 5].Value = item.ReceivedDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 6].Value = item.DeliveryDate?.ToString("dd.MM.yyyy") ?? "-";
                    worksheet.Cells[row, 7].Value = ((RepairStatusEnum)(item.StatusId ?? 1)).GetDisplayName();

                    // Hücre değerini para birimi sembolü ve koduyla formatlayıp basıyoruz
                    worksheet.Cells[row, 8].Value = $"{currencySymbol} {item.Price:N2} ({currency})";
                    worksheet.Cells[row, 8].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                    // Her para birimini kendi havuzuna ekliyoruz 
                    if (currencyTotals.ContainsKey(currency))
                    {
                        currencyTotals[currency] += item.Price;
                    }
                    else
                    {
                        currencyTotals.Add(currency, item.Price);
                    }

                    row++;
                }

        
                row++;

                // Toplamları para birimi kırılımına göre alt alta listeliyoruz
                if (currencyTotals.Any())
                {
                    worksheet.Cells[row, 7].Value = "GENEL HARCAMA DETAYI";
                    worksheet.Cells[row, 7].Style.Font.Bold = true;
                    worksheet.Cells[row, 7, row, 8].Merge = true;
                    worksheet.Cells[row, 7].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.WhiteSmoke);
                    row++;

                    foreach (var totalItem in currencyTotals)
                    {
                        string sym = TeknikServisTakip.Helpers.CurrencyHelper.GetSymbol(totalItem.Key);

                        worksheet.Cells[row, 7].Value = $"Toplam ({totalItem.Key}):";
                        worksheet.Cells[row, 7].Style.Font.Bold = true;

                        worksheet.Cells[row, 8].Value = $"{sym} {totalItem.Value:N2}";
                        worksheet.Cells[row, 8].Style.Font.Bold = true;
                        worksheet.Cells[row, 8].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        row++;
                    }
                }
                else
                {
                    worksheet.Cells[row, 7].Value = "TOPLAM:";
                    worksheet.Cells[row, 8].Value = "₺ 0.00";
                    worksheet.Cells[row, 7].Style.Font.Bold = true;
                    worksheet.Cells[row, 8].Style.Font.Bold = true;
                }

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


        // Excel Export MyArchiveDetail 
        public async Task<IActionResult> ExportArchiveToExcel(int id)
        {
           
            var archive = await _unitOfWork.GetArchiveByIdWithIncludeAsync(id, a => a.Personel);

            if (archive == null) return NotFound();

            // 2. Malzemeleri repository'deki GetAllAsync metoduyla çekip Product (Ürün) tablosunu bağlıyoruz
            var allMaterials = await _unitOfWork.RepairMaterials.GetAllAsync(m => m.Product);

            // Hafızada filtreleme yaparak sadece bu tamire ait olanları ayıklıyoruz
            var materials = allMaterials.Where(m => m.RepairId == archive.OriginalRepairId).ToList();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Tamir Detay Raporu");

                // ========== GENEL AYARLAR ==========
                worksheet.View.ShowGridLines = false;
                worksheet.Cells.Style.Font.Name = "Segoe UI";
                worksheet.Cells.Style.Font.Size = 10;

           
                worksheet.Column(1).Width = 25; // Ürün Adı / Etiket
                worksheet.Column(2).Width = 45; // Açıklama / Değer
                worksheet.Column(3).Width = 15; // Miktar
                worksheet.Column(4).Width = 20; // Kullanım Tarihi

                // ========== ÜST BANNER (BAŞLIK) ==========
                using (var range = worksheet.Cells["A1:D1"])
                {
                    range.Merge = true;
                    range.Value = "TEKNİK SERVİS TAKİP SİSTEMİ";
                    range.Style.Font.Size = 18;
                    range.Style.Font.Bold = true;
                    range.Style.Font.Color.SetColor(Color.White);
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(33, 37, 41));
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
                worksheet.Row(1).Height = 45;

                using (var range = worksheet.Cells["A2:D2"])
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
                    var cellLabel = worksheet.Cells[currentRow, 1];
                    cellLabel.Value = label;
                    cellLabel.Style.Font.Bold = true;
                    cellLabel.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cellLabel.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 249, 250));
                    cellLabel.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));

                    var cellValue = worksheet.Cells[currentRow, 2, currentRow, 4];
                    cellValue.Merge = true;
                    cellValue.Value = value;
                    cellValue.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    cellValue.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    cellValue.Style.Indent = 1;

                    if (isPrice)
                    {
                        cellValue.Style.Font.Bold = true;
                        cellValue.Style.Font.Color.SetColor(Color.FromArgb(21, 115, 71));
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

                string currency = archive.Currency ?? "TRY";
                AddInfoRow("Toplam Servis Ücreti", $"{archive.Price:N2} {currency}", true);

                currentRow++;

                // ========== GENİŞ METİN ALANLARI ==========
                void AddLargeBox(string title, string content, Color themeColor)
                {
                    var titleRange = worksheet.Cells[currentRow, 1, currentRow, 4];
                    titleRange.Merge = true;
                    titleRange.Value = title;
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Font.Color.SetColor(Color.FromArgb(68, 68, 68));
                    titleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    titleRange.Style.Fill.BackgroundColor.SetColor(themeColor);
                    titleRange.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    currentRow++;

                    var contentRange = worksheet.Cells[currentRow, 1, currentRow, 4];
                    contentRange.Merge = true;
                    contentRange.Value = content;
                    contentRange.Style.WrapText = true;
                    contentRange.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    contentRange.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    worksheet.Row(currentRow).Height = 60;
                    currentRow += 2;
                }

                AddLargeBox(" Müşteri Arıza Açıklaması", string.IsNullOrEmpty(archive.ProblemDescription) ? "-" : archive.ProblemDescription, Color.FromArgb(255, 243, 205));
                AddLargeBox(" Teknik Servis İşlem Notları", string.IsNullOrEmpty(archive.InternalNote) ? "-" : archive.InternalNote, Color.FromArgb(227, 242, 253));

                // ========== KULLANILAN MALZEMELER TABLOSU ==========
                var matTitleRange = worksheet.Cells[currentRow, 1, currentRow, 4];
                matTitleRange.Merge = true;
                matTitleRange.Value = " KULLANILAN MALZEMELER";
                matTitleRange.Style.Font.Bold = true;
                matTitleRange.Style.Font.Size = 11;
                matTitleRange.Style.Font.Color.SetColor(Color.White);
                matTitleRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                matTitleRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(25, 135, 84)); // Yeşil Tema
                worksheet.Row(currentRow).Height = 25;
                currentRow++;

                string[] headers = { "Ürün Adı", "Açıklama", "Miktar", "Kullanım Tarihi" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var headerCell = worksheet.Cells[currentRow, i + 1];
                    headerCell.Value = headers[i];
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(233, 236, 239));
                    headerCell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    if (i == 2) headerCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                worksheet.Row(currentRow).Height = 22;
                currentRow++;

                if (materials.Any())
                {
                    foreach (var material in materials)
                    {
                        string productName = material.MaterialType == "External"
                            ? $"{material.ExternalProductName} (Dışarıdan)"
                            : (material.Product?.ProductName ?? "-");

                        worksheet.Cells[currentRow, 1].Value = productName;
                        worksheet.Cells[currentRow, 2].Value = material.Description ?? "-";

                        var qtyCell = worksheet.Cells[currentRow, 3];
                        qtyCell.Value = material.Quantity;
                        qtyCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        worksheet.Cells[currentRow, 4].Value = material.UsedAt.ToString("dd.MM.yyyy HH:mm");

                        for (int i = 1; i <= 4; i++)
                        {
                            worksheet.Cells[currentRow, i].Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                        }
                        worksheet.Row(currentRow).Height = 20;
                        currentRow++;
                    }
                }
                else
                {
                    var noMatRange = worksheet.Cells[currentRow, 1, currentRow, 4];
                    noMatRange.Merge = true;
                    noMatRange.Value = "Bu tamir işleminde kullanılan herhangi bir malzeme bulunmamaktadır.";
                    noMatRange.Style.Font.Italic = true;
                    noMatRange.Style.Font.Color.SetColor(Color.Gray);
                    noMatRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    for (int i = 1; i <= 4; i++)
                    {
                        worksheet.Cells[currentRow, i].Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(222, 226, 230));
                    }
                    worksheet.Row(currentRow).Height = 25;
                    currentRow++;
                }

                currentRow++;

                // ========== FOOTER ==========
                var footerRange = worksheet.Cells[currentRow, 1, currentRow, 4];
                footerRange.Merge = true;
                footerRange.Value = "Bu rapor Teknik Servis Takip Sistemi üzerinden dijital olarak oluşturulmuştur.";
                footerRange.Style.Font.Size = 8;
                footerRange.Style.Font.Italic = true;
                footerRange.Style.Font.Color.SetColor(Color.Gray);
                footerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                worksheet.PrinterSettings.Orientation = eOrientation.Portrait;
                worksheet.PrinterSettings.FitToPage = true;
                worksheet.PrinterSettings.FitToWidth = 1;

                var fileBytes = package.GetAsByteArray();
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Servis_Raporu_{archive.TrackingCode}.xlsx");
            }
        }

      // Print ArchiveDetail
        public async Task<IActionResult> PrintArchiveDetail(int id)
        {
            // 1. Arşiv kaydını UnitOfWork içindeki doğru metodunla çekiyoruz kanka
            var archive = await _unitOfWork.GetArchiveByIdWithIncludeAsync(id, a => a.Personel);

            if (archive == null) return NotFound();

            // 2. Malzemeleri GetAllAsync ile çekip Product tablosunu dahil ediyoruz
            var allMaterials = await _unitOfWork.RepairMaterials.GetAllAsync(m => m.Product);

            // Sadece bu tamire ait olan malzemeleri filtreleyip listeliyoruz
            var materials = allMaterials.Where(m => m.RepairId == archive.OriginalRepairId).ToList();

            // 3. View katmanındaki döngünün (ViewBag.Materials) beslenmesi için veriyi atıyoruz
            ViewBag.Materials = materials;

            // 4. Modeli sayfaya gönderiyoruz
            return View("PrintArchiveDetail", archive);
        }


        // MyArchiveDetail pdf download

        [HttpGet]
        public async Task<IActionResult> ExportArchiveToPdf(int id)
        {
            try
            {
                // 1. Verileri Çekiyoruz
                var archive = await _unitOfWork.GetArchiveByIdWithIncludeAsync(id, a => a.Personel);
                if (archive == null) return NotFound("Arşiv kaydı bulunamadı.");

                var allMaterials = await _unitOfWork.RepairMaterials.GetAllAsync(m => m.Product);
                var materials = allMaterials.Where(m => m.RepairId == archive.OriginalRepairId).ToList();

                var currency = archive.Currency ?? "TRY";
                var currencySymbol = currency == "USD" ? "$" : (currency == "EUR" ? "€" : (currency == "GBP" ? "£" : "₺"));

                string trackingCode = archive.TrackingCode ?? "-";
                string productName = archive.ProductName ?? "-";
                string productBrand = archive.ProductBrand ?? "-";
                string productModel = archive.ProductModel ?? "-";
                string serialNumber = string.IsNullOrEmpty(archive.SerialNumber) ? "-" : archive.SerialNumber;
                string receivedDateStr = archive.ReceivedDate.ToString("dd.MM.yyyy HH:mm");
                string deliveryDateStr = archive.DeliveryDate?.ToString("dd.MM.yyyy HH:mm") ?? "-";
                string personnelName = archive.Personel?.FullName ?? "-";
                string totalPriceStr = $"{currencySymbol} {archive.Price:N2} ({currency})";
                string problemDesc = string.IsNullOrEmpty(archive.ProblemDescription) ? "-" : archive.ProblemDescription;
                string internalNote = string.IsNullOrEmpty(archive.InternalNote) ? "-" : archive.InternalNote;

                // 2. HTML String Tasarımı
                var htmlBuilder = new System.Text.StringBuilder();
                htmlBuilder.Append($@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8' />
            <title>Servis Raporu - {trackingCode}</title>
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>
            <link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css'>
            <style>
                body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8f9fa; color: #333; padding: 15px; width: 100%; max-width: 100%; }}
                .detail-header {{ background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); border-radius: 12px; padding: 20px; color: white; margin-bottom: 20px; }}
                
                /* Kartların ve içindeki elementlerin sayfa geçişlerinde bölünmesini engelleme */
                .info-card {{ 
                    background: #fff; 
                    border-radius: 12px; 
                    padding: 20px; 
                    border: 1px solid #e9ecef; 
                    box-shadow: 0 2px 4px rgba(0,0,0,0.02); 
                    height: 100%; 
                    page-break-inside: avoid; /* Eski tarayıcı desteği */
                    break-inside: avoid;      /* Modern PDF motoru desteği */
                }}
                
                /* Tablo satırlarının ve başlığının bölünmesini engelleme */
                table, tr, td, th {{
                    page-break-inside: avoid !important;
                    break-inside: avoid !important;
                }}

                .info-label {{ font-weight: 600; color: #6c757d; }}
                .info-value {{ font-weight: 500; color: #212529; }}
                .badge-delivered {{ background: #10b981; color: white; padding: 6px 14px; border-radius: 20px; font-size: 12px; font-weight: bold; }}
                .section-title {{ font-size: 16px; font-weight: bold; border-bottom: 2px solid #e9ecef; padding-bottom: 8px; margin-bottom: 15px; }}
                .bg-box {{ background-color: #fff; border-left: 4px solid; padding: 15px; border-radius: 4px; margin-bottom: 15px; page-break-inside: avoid; break-inside: avoid; }}
                .box-warning {{ border-left-color: #ffc107; background-color: #fffde7; }}
                .box-info {{ border-left-color: #0dcaf0; background-color: #f0faff; }}
                .footer {{ text-align: center; margin-top: 25px; font-size: 11px; color: #888; border-top: 1px solid #e9ecef; padding-top: 15px; page-break-inside: avoid; break-inside: avoid; }}
            </style>
        </head>
        <body>
            <div class='container-fluid'>
                
                <div class='detail-header d-flex justify-content-between align-items-center'>
                    <div>
                        <h4 class='mb-1'><i class='fas fa-file-invoice me-2'></i> TEKNİK SERVİS DETAYLI ARŞİV RAPORU</h4>
                        <p class='mb-0 opacity-75'>Cihaz ve servis teslim süreç dökümü</p>
                    </div>
                    <div>
                        <span class='badge-delivered'><i class='fas fa-check-circle me-1'></i> Teslim Edildi (Arşiv Kaydı)</span>
                    </div>
                </div>

                <div class='row mb-4'>
                    <div class='col-6'>
                        <div class='info-card'>
                            <div class='section-title text-primary'><i class='fas fa-laptop-medical me-2'></i>Cihaz & Ürün Bilgileri</div>
                            <div class='row mb-2'><div class='col-4 info-label'>Takip Kodu:</div><div class='col-8 info-value'><strong>{trackingCode}</strong></div></div>
                            <div class='row mb-2'><div class='col-4 info-label'>Ürün Adı:</div><div class='col-8 info-value'>{productName}</div></div>
                            <div class='row mb-2'><div class='col-4 info-label'>Marka / Model:</div><div class='col-8 info-value'>{productBrand} {productModel}</div></div>
                            <div class='row mb-2'><div class='col-4 info-label'>Seri Numarası:</div><div class='col-8 info-value'>{serialNumber}</div></div>
                        </div>
                    </div>
                    <div class='col-6'>
                        <div class='info-card'>
                            <div class='section-title text-success'><i class='fas fa-business-time me-2'></i>Servis & Zamanlama Bilgileri</div>
                            <div class='row mb-2'><div class='col-4 info-label'>Geliş Tarihi:</div><div class='col-8 info-value'>{receivedDateStr}</div></div>
                            <div class='row mb-2'><div class='col-4 info-label'>Teslim Tarihi:</div><div class='col-8 info-value'>{deliveryDateStr}</div></div>
                            <div class='row mb-2'><div class='col-4 info-label'>Sorumlu Uzman:</div><div class='col-8 info-value'>{personnelName}</div></div>
                            <div class='row mb-2'><div class='col-4 info-label'>Toplam Tutar:</div><div class='col-8 info-value text-success fw-bold' style='font-size:16px;'>{totalPriceStr}</div></div>
                        </div>
                    </div>
                </div>

                <div class='info-card mb-4'>
                    <div class='section-title text-secondary'><i class='fas fa-comment-alt me-2'></i>Servis Geçmiş Notları</div>
                    <div class='row'>
                        <div class='col-6'>
                            <div class='bg-box box-warning h-100'>
                                <div class='fw-bold mb-2 text-dark'><i class='fas fa-exclamation-triangle text-warning me-2'></i>Müşteri Arıza Bildirimi</div>
                                <div>{problemDesc}</div>
                            </div>
                        </div>
                        <div class='col-6'>
                            <div class='bg-box box-info h-100'>
                                <div class='fw-bold mb-2 text-dark'><i class='fas fa-tools text-info me-2'></i>Teknik Servis İşlem Detayı</div>
                                <div>{internalNote}</div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class='info-card'>
                    <div class='section-title text-dark'><i class='fas fa-boxes-stacked text-success me-2'></i>Servis Esnasında Kullanılan Yedek Parça / Malzemeler</div>");

                if (materials != null && materials.Any())
                {
                    htmlBuilder.Append(@"
                    <div class='table-responsive'>
                        <table class='table table-bordered table-striped align-middle m-0'>
                            <thead>
                                <tr class='table-dark'>
                                    <th>Parça / Ürün Adı</th>
                                    <th style='width: 45%;'>Teknisyen Açıklaması</th>
                                    <th style='width: 10%; text-align:center;'>Adet/Miktar</th>
                                    <th style='width: 20%; text-align:center;'>Eklenme Tarihi</th>
                                </tr>
                            </thead>
                            <tbody>");

                    foreach (var mat in materials)
                    {
                        string pName = mat.MaterialType == "External"
                            ? $"<span class='badge bg-info'>{mat.ExternalProductName}</span> <span class='badge bg-secondary ms-1'>Dış Tedarik</span>"
                            : (mat.Product?.ProductName ?? "-");

                        string matDesc = mat.Description ?? "-";
                        string matQty = mat.Quantity.ToString();
                        string matDate = mat.UsedAt.ToString("dd.MM.yyyy HH:mm");

                        htmlBuilder.Append("<tr>");
                        htmlBuilder.Append("<td><strong>" + pName + "</strong></td>");
                        htmlBuilder.Append("<td>" + matDesc + "</td>");
                        htmlBuilder.Append("<td style='text-align:center;' class='fw-bold'>" + matQty + "</td>");
                        htmlBuilder.Append("<td style='text-align:center;'>" + matDate + "</td>");
                        htmlBuilder.Append("</tr>");
                    }

                    htmlBuilder.Append(@"
                            </tbody>
                        </table>
                    </div>");
                }
                else
                {
                    htmlBuilder.Append(@"
                    <div class='text-center text-muted py-4'>
                        <i class='fas fa-box-open fa-2x mb-2 d-block text-secondary'></i>
                        <span>Bu servis kaydında parça değişimi veya malzeme kullanımı yapılmamıştır.</span>
                    </div>");
                }

                string nowStr = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                htmlBuilder.Append($@"
                </div>

                <div class='footer'>
                    Bu belge sistem tarafından otomatik üretilmiştir. • Doğrulama Kodu: {trackingCode} • Raporlama Zamanı: {nowStr}
                </div>
            </div>
        </body>
        </html>");

                // 3. Puppeteer Sharp ile Yatay PDF Çıktısı Alma Alanı
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();

                var launchOptions = new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-web-security" }
                };

                using var browser = await Puppeteer.LaunchAsync(launchOptions);
                using var page = await browser.NewPageAsync();
                await page.SetContentAsync(htmlBuilder.ToString());
                await page.EvaluateExpressionHandleAsync("document.fonts.ready");

                var pdfOptions = new PdfOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true,
                    Landscape = true,
                    MarginOptions = new MarginOptions
                    {
                        Top = "10mm",
                        Bottom = "10mm",
                        Left = "10mm",
                        Right = "10mm"
                    }
                };

                byte[] pdfBytes = await page.PdfDataAsync(pdfOptions);
                return File(pdfBytes, "application/pdf", $"Servis_Raporu_{trackingCode}.pdf");
            }
            catch (Exception ex)
            {
                return Content($"PDF Oluşturulurken Hata Oluştu: {ex.Message}");
            }
        }
    }
}