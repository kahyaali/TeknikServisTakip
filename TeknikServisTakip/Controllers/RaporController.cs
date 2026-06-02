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
using System.Drawing;
using System.Text;
using TeknikServisTakip.Helpers;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class RaporController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public RaporController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        // ==================== LİSTE API'LERİ ====================

        [HttpGet]
        public async Task<IActionResult> GetPersonelList()
        {
            var personels = await _userManager.GetUsersInRoleAsync("Personel");
            var result = personels
                .Where(p => p.IsActive == true)
                .Select(p => new { id = p.Id, name = p.FullName })
                .ToList();
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerList()
        {
            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            var result = customers
                .Where(c => c.IsActive == true)
                .Select(c => new { id = c.Id, name = c.FullName, customerNumber = c.CustomerNumber })
                .ToList();
            return Json(result);
        }

        // ==================== FİRMA RAPORLARI ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetFirmaStats(string startDate, string endDate, int? statusId, int? personelId)
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync(r => r.Personel);

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }
            if (statusId.HasValue && statusId.Value > 0)
            {
                repairs = repairs.Where(r => r.StatusId == statusId.Value).ToList();
            }
            if (personelId.HasValue && personelId.Value > 0)
            {
                repairs = repairs.Where(r => r.PersonelId == personelId.Value).ToList();
            }

            // Durum bazlı sayılar (Doğru eşleştirme)
            int bekleyenCount = repairs.Count(r =>
                r.StatusId == (int)RepairStatusEnum.UrunKaydedildi ||
                r.StatusId == (int)RepairStatusEnum.ExpertizBekleniyor ||
                r.StatusId == (int)RepairStatusEnum.ExpertizeGonderildi ||
                r.StatusId == (int)RepairStatusEnum.TeklifHazirlaniyor ||
                r.StatusId == (int)RepairStatusEnum.TeklifGonderildi ||
                r.StatusId == (int)RepairStatusEnum.TeklifOnaylandi);

            int islemdeCount = repairs.Count(r =>
                r.StatusId == (int)RepairStatusEnum.IslemeAlindi);

            int tamamlananCount = repairs.Count(r =>
                r.StatusId == (int)RepairStatusEnum.Tamamlandi);

            int teslimCount = repairs.Count(r =>
                r.StatusId == (int)RepairStatusEnum.TeslimEdildi);

            return Json(new
            {
                success = true,
                total = repairs.Count(),
                bekleyen = bekleyenCount,
                islemde = islemdeCount,
                tamamlanan = tamamlananCount,
                teslim = teslimCount,
                totalRevenue = repairs.Sum(r => r.Price),
                averageRevenue = repairs.Any() ? repairs.Average(r => r.Price) : 0
            });
        }

        // Durum Dağılımı
        [HttpGet]
        public async Task<IActionResult> GetStatusDistribution(string startDate, string endDate)
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync();

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }

            // Sadece veri olan durumları göster
            var statusData = new List<object>();
            var colors = new List<string>();

            var statusList = new[]
            {
        new { Id = (int)RepairStatusEnum.UrunKaydedildi, Name = "Ürün Kaydedildi", Color = "#6c757d" },
        new { Id = (int)RepairStatusEnum.ExpertizBekleniyor, Name = "Expertiz Bekleniyor", Color = "#ffc107" },
        new { Id = (int)RepairStatusEnum.ExpertizeGonderildi, Name = "Expertize Gönderildi", Color = "#20c997" },
        new { Id = (int)RepairStatusEnum.TeklifHazirlaniyor, Name = "Teklif Hazırlanıyor", Color = "#0d6efd" },
        new { Id = (int)RepairStatusEnum.TeklifGonderildi, Name = "Teklif Gönderildi", Color = "#6f42c1" },
        new { Id = (int)RepairStatusEnum.TeklifOnaylandi, Name = "Teklif Onaylandı", Color = "#fd7e14" },
        new { Id = (int)RepairStatusEnum.IslemeAlindi, Name = "İşleme Alındı", Color = "#0dcaf0" },
        new { Id = (int)RepairStatusEnum.Tamamlandi, Name = "Tamamlandı", Color = "#198754" },
        new { Id = (int)RepairStatusEnum.TeslimEdildi, Name = "Teslim Edildi", Color = "#343a40" }
    };

            foreach (var status in statusList)
            {
                int count = repairs.Count(r => r.StatusId == status.Id);
                if (count > 0)
                {
                    statusData.Add(new { label = status.Name, value = count, color = status.Color });
                    colors.Add(status.Color);
                }
            }

            return Json(new
            {
                labels = statusData.Select(x => ((dynamic)x).label).ToArray(),
                data = statusData.Select(x => ((dynamic)x).value).ToArray(),
                colors = statusData.Select(x => ((dynamic)x).color).ToArray()
            });
        }

        // Aylık Tamir Trendi
        [HttpGet]
        public async Task<IActionResult> GetMonthlyTrend(int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var repairs = await _unitOfWork.RepairItems.GetAllAsync();
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

        // Personel Performansı
        [HttpGet]
        public async Task<IActionResult> GetPersonelPerformance(string startDate, string endDate)
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync(r => r.Personel);

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }

            var performance = repairs
                .Where(r => r.Personel != null)
                .GroupBy(r => r.PersonelId)
                .Select(g => new
                {
                    personelId = g.Key,
                    personelName = g.First().Personel?.FullName ?? "Bilinmiyor",
                    totalRepairs = g.Count(),
                    completedRepairs = g.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi || r.StatusId == (int)RepairStatusEnum.TeslimEdildi),
                    totalRevenue = g.Sum(r => r.Price),
                    avgRepairTime = g.Where(r => r.DeliveryDate.HasValue)
                        .Select(r => (r.DeliveryDate.Value - r.ReceivedDate).TotalDays)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .OrderByDescending(x => x.totalRepairs)
                .ToList();

            return Json(performance);
        }

        // En Çok Tamir Yapılan Ürünler
        [HttpGet]
        public async Task<IActionResult> GetTopProducts(int top = 10, string startDate = null, string endDate = null)
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync();

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }

            var topProducts = repairs
                .Where(r => !string.IsNullOrEmpty(r.ProductName))
                .GroupBy(r => new { r.ProductName, r.ProductBrand, r.ProductModel })
                .Select(g => new
                {
                    productName = g.Key.ProductName,
                    brand = g.Key.ProductBrand ?? "-",
                    model = g.Key.ProductModel ?? "-",
                    count = g.Count(),
                    totalRevenue = g.Sum(r => r.Price),
                    avgPrice = g.Average(r => r.Price)
                })
                .OrderByDescending(x => x.count)
                .Take(top)
                .ToList();

            return Json(topProducts);
        }

        // Aylık Gelir Tablosu
        [HttpGet]
        public async Task<IActionResult> GetMonthlyRevenue(int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var repairs = await _unitOfWork.RepairItems.GetAllAsync();
            repairs = repairs.Where(r => r.ReceivedDate.Year == year).ToList();

            var monthlyRevenue = new List<object>();
            for (int i = 1; i <= 12; i++)
            {
                var monthRepairs = repairs.Where(r => r.ReceivedDate.Month == i).ToList();
                monthlyRevenue.Add(new
                {
                    month = i,
                    monthName = GetMonthName(i),
                    count = monthRepairs.Count,
                    revenue = monthRepairs.Sum(r => r.Price),
                    avgRevenue = monthRepairs.Any() ? monthRepairs.Average(r => r.Price) : 0
                });
            }

            return Json(new { year = year, data = monthlyRevenue });
        }

        // ==================== MÜŞTERİ RAPORLARI ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetMusteriStats(string customerId, string startDate, string endDate, int? statusId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return Json(new { success = false });
            }

            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == customerId);

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }
            if (statusId.HasValue && statusId.Value > 0)
            {
                repairs = repairs.Where(r => r.StatusId == statusId.Value).ToList();
            }

            var customer = await _userManager.FindByIdAsync(customerId);

            return Json(new
            {
                success = true,
                customerName = customer?.FullName,
                customerNumber = customer?.CustomerNumber,
                total = repairs.Count(),
                pending = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.UrunKaydedildi ||
                                      r.StatusId == (int)RepairStatusEnum.ExpertizBekleniyor ||
                                      r.StatusId == (int)RepairStatusEnum.ExpertizeGonderildi),
                progress = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.IslemeAlindi),
                completed = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi ||
                                                r.StatusId == (int)RepairStatusEnum.TeslimEdildi),
                totalRevenue = repairs.Sum(r => r.Price)
            });
        }

        // Müşteri Tamir Geçmişi
        [HttpGet]
        public async Task<IActionResult> GetCustomerRepairHistory(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return Json(new List<object>());
            }

            var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == customerId, r => r.Personel);

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
                r.Price,
                personel = r.Personel?.FullName ?? "Atanmamış"
            }).OrderByDescending(r => r.ReceivedDate);

            return Json(result);
        }

        // ==================== RAPOR OLUŞTURMA ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport(string type, string format, string startDate, string endDate,
            int? statusId, int? personelId, string customerId)
        {
            IEnumerable<RepairItem> repairs;
            string title = "";
            var fileName = "";
            if (type == "firma")
            {
                repairs = await _unitOfWork.RepairItems.GetAllAsync(r => r.Personel, r => r.AppUser);

                if (!string.IsNullOrEmpty(startDate))
                {
                    var start = DateTime.Parse(startDate);
                    repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
                }
                if (!string.IsNullOrEmpty(endDate))
                {
                    var end = DateTime.Parse(endDate).AddDays(1);
                    repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
                }
                if (statusId.HasValue && statusId.Value > 0)
                {
                    repairs = repairs.Where(r => r.StatusId == statusId.Value).ToList();
                }
                if (personelId.HasValue && personelId.Value > 0)
                {
                    repairs = repairs.Where(r => r.PersonelId == personelId.Value).ToList();
                }

                title = "Firma Raporu";
               
            }
            else
            {
                if (string.IsNullOrEmpty(customerId))
                    return BadRequest();

                repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == customerId, r => r.Personel, r => r.AppUser);

                if (!string.IsNullOrEmpty(startDate))
                {
                    var start = DateTime.Parse(startDate);
                    repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
                }
                if (!string.IsNullOrEmpty(endDate))
                {
                    var end = DateTime.Parse(endDate).AddDays(1);
                    repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
                }
                if (statusId.HasValue && statusId.Value > 0)
                {
                    repairs = repairs.Where(r => r.StatusId == statusId.Value).ToList();
                }

                var customer = await _userManager.FindByIdAsync(customerId);
           
                title = "Müşteri Raporu";

            }


            if (format == "excel")
            {
                return ExportToExcel(repairs, title);
            }
            else if (format == "pdf")
            {
                return await ExportToPdf(repairs, title);
            }

            else
            {
                var html = GenerateHtmlReport(repairs, title);
                return Content(html, "text/html");
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

        // ==================== EXCEL EXPORT  ====================

        private IActionResult ExportToExcel(IEnumerable<RepairItem> repairs, string title)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Rapor");

                // Başlık
                worksheet.Cells[1, 1].Value = title;
                worksheet.Cells[1, 1, 1, 12].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Tarih
                worksheet.Cells[2, 1].Value = $"Oluşturulma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";
                worksheet.Cells[2, 1, 2, 12].Merge = true;
                worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Başlıklar (Model eklendi)
                worksheet.Cells[4, 1].Value = "Takip Kodu";
                worksheet.Cells[4, 2].Value = "Müşteri";
                worksheet.Cells[4, 3].Value = "Ürün";
                worksheet.Cells[4, 4].Value = "Marka";
                worksheet.Cells[4, 5].Value = "Model";
                worksheet.Cells[4, 6].Value = "Arıza Açıklaması";
                worksheet.Cells[4, 7].Value = "Geliş Tarihi";
                worksheet.Cells[4, 8].Value = "Teslim Tarihi";
                worksheet.Cells[4, 9].Value = "Personel";
                worksheet.Cells[4, 10].Value = "Durum";
                worksheet.Cells[4, 11].Value = "Ücret";

                using (var range = worksheet.Cells[4, 1, 4, 11])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                int row = 5;
                decimal totalRevenue = 0;
                foreach (var item in repairs)
                {
                    worksheet.Cells[row, 1].Value = item.TrackingCode;
                    worksheet.Cells[row, 2].Value = item.AppUser?.FullName ?? "-";
                    worksheet.Cells[row, 3].Value = item.ProductName;
                    worksheet.Cells[row, 4].Value = item.ProductBrand ?? "-";
                    worksheet.Cells[row, 5].Value = item.ProductModel ?? "-";
                    worksheet.Cells[row, 6].Value = item.ProblemDescription;
                    worksheet.Cells[row, 7].Value = item.ReceivedDate.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 8].Value = item.DeliveryDate?.ToString("dd.MM.yyyy") ?? "-";
                    worksheet.Cells[row, 9].Value = item.Personel?.FullName ?? "Atanmamış";
                    worksheet.Cells[row, 10].Value = ((RepairStatusEnum)(item.StatusId ?? 1)).GetDisplayName();
                    worksheet.Cells[row, 11].Value = item.Price;
                    worksheet.Cells[row, 11].Style.Numberformat.Format = "#,##0.00 ₺";
                    totalRevenue += item.Price;
                    row++;
                }

                // Toplam satırı
                worksheet.Cells[row, 9].Value = "TOPLAM:";
                worksheet.Cells[row, 9].Style.Font.Bold = true;
                worksheet.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                worksheet.Cells[row, 10].Value = totalRevenue;
                worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0.00 ₺";
                worksheet.Cells[row, 10].Style.Font.Bold = true;

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"{title.Replace(" ", "_").Replace("/", "-")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";


                fileName = RemoveTurkishChars(fileName) + ".xlsx";

   
                var cd = new System.Net.Mime.ContentDisposition
                {
                    FileName = fileName,
                    Inline = false
                };
                Response.Headers.Add("Content-Disposition", cd.ToString());

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
        }

        // ==================== PDF EXPORT  ====================

        private async Task<IActionResult> ExportToPdf(IEnumerable<RepairItem> repairs, string title)
        {
            var html = GenerateHtmlReport(repairs, title);

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                Landscape = true,
                PrintBackground = true,
                MarginOptions = new MarginOptions { Top = "10mm", Bottom = "10mm", Left = "10mm", Right = "10mm" }
            });

            string fileName = $"{title.Replace(" ", "_").Replace("/", "-")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            fileName = RemoveTurkishChars(fileName) + ".pdf";
            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = fileName,
                Inline = false
            };

            Response.Headers.Add("Content-Disposition", cd.ToString());

            return File(pdfBytes, "application/pdf");


        }

        // ==================== HTML RAPOR ====================

        private string GenerateHtmlReport(IEnumerable<RepairItem> repairs, string title)
        {
            var sb = new StringBuilder();
            decimal totalRevenue = 0;

            sb.Append($@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>{title}</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }}
        h1 {{ color: #0d6efd; text-align: center; font-size: 18px; margin-bottom: 10px; }}
        .tarih {{ text-align: right; margin-bottom: 20px; font-size: 11px; color: #666; }}
        table {{ width: 100%; border-collapse: collapse; font-size: 11px; }}
        th {{ background-color: #0d6efd; color: white; padding: 8px; border: 1px solid #0d5efd; }}
        td {{ padding: 6px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 10px; color: #666; }}
        .total {{ background-color: #f8fafc; font-weight: bold; }}
        .text-center {{ text-align: center; }}
        .text-right {{ text-align: right; }}
    </style>
</head>
<body>
    <h1>{title}</h1>
    <div class='tarih'>Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}</div>
    
    <table>
        <thead>
            <tr>
                <th>Takip Kodu</th>
                <th>Müşteri</th>
                <th>Ürün</th>
                <th>Marka</th>
                <th>Model</th>
                <th>Geliş Tarihi</th>
                <th>Teslim Tarihi</th>
                <th>Durum</th>
                <th>Ücret</th>
            </tr>
        </thead>
        <tbody>");

            foreach (var item in repairs)
            {
                sb.Append($@"
            <tr>
                <td class='text-center'>{item.TrackingCode}</td>
                <td>{item.AppUser?.FullName ?? "-"}</td>
                <td>{item.ProductName}</td>
                <td>{item.ProductBrand ?? "-"}</td>
                <td>{item.ProductModel ?? "-"}</td>
                <td class='text-center'>{item.ReceivedDate:dd.MM.yyyy}</td>
                <td class='text-center'>{item.DeliveryDate?.ToString("dd.MM.yyyy") ?? "-"}</td>
                <td class='text-center'>{(item.StatusId.HasValue ? ((RepairStatusEnum)item.StatusId.Value).GetDisplayName() : "Beklemede")}</td>
                <td class='text-right'>{item.Price:C2}</td>
            </tr>");
                totalRevenue += item.Price;
            }

            sb.Append($@"
            <tr class='total'>
                <td colspan='8' class='text-right'><strong>TOPLAM:</strong></td>
                <td class='text-right'><strong>{totalRevenue:C2}</strong></td>
            </tr>
        </tbody>
    </table>
    <div class='footer'>Teknik Servis Takip Sistemi</div>
</body>
</html>");

            return sb.ToString();
        }

        private string RemoveTurkishChars(string text)
        {
            return text.Replace("İ", "I").Replace("ı", "i")
                      .Replace("Ğ", "G").Replace("ğ", "g")
                      .Replace("Ü", "U").Replace("ü", "u")
                      .Replace("Ş", "S").Replace("ş", "s")
                      .Replace("Ö", "O").Replace("ö", "o")
                      .Replace("Ç", "C").Replace("ç", "c");
        }






        // ==================== RAPOR SAYFALARI ====================

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Özet istatistikler
            var repairs = await _unitOfWork.RepairItems.GetAllAsync();
            var offers = await _unitOfWork.Offers.GetAllAsync();

            ViewBag.TotalRepairs = repairs.Count();
            ViewBag.ActiveRepairs = repairs.Count(r => r.StatusId != (int)RepairStatusEnum.TeslimEdildi);
            ViewBag.CompletedRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi || r.StatusId == (int)RepairStatusEnum.TeslimEdildi);
            ViewBag.TotalRevenue = repairs.Sum(r => r.Price);
            ViewBag.TotalOffers = offers.Count();
            ViewBag.ApprovedOffers = offers.Count(o => !o.IsActive);
            ViewBag.TotalOfferAmount = offers.Sum(o => o.GrandTotal);

            return View();
        }

        [HttpGet]
        public IActionResult GelirRaporu()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PersonelPerformans()
        {
            return View();
        }

        [HttpGet]
        public IActionResult UrunRaporu()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TamirSureRaporu()
        {
            return View();
        }

        [HttpGet]
        public IActionResult MusteriRaporu()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TeklifRaporu()
        {
            return View();
        }


        // ==================== DASHBOARD API'LERİ ====================

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync();
            var offers = await _unitOfWork.Offers.GetAllAsync();

            // Para birimlerine göre gelir gruplaması
            var revenueByCurrency = repairs
                .GroupBy(r => r.Currency ?? "TRY")
                .Select(g => new
                {
                    currency = g.Key,
                    symbol = CurrencyHelper.GetSymbol(g.Key),
                    total = g.Sum(r => r.Price)
                })
                .ToList();

            // Aylık gelir trendi (son 12 ay) - para birimi bazlı
            var monthlyRevenue = new List<object>();
            for (int i = 0; i < 12; i++)
            {
                var date = DateTime.Now.AddMonths(-i);
                var monthRepairs = repairs.Where(r => r.ReceivedDate.Year == date.Year && r.ReceivedDate.Month == date.Month);

                var monthlyByCurrency = monthRepairs
                    .GroupBy(r => r.Currency ?? "TRY")
                    .Select(g => new
                    {
                        currency = g.Key,
                        symbol = CurrencyHelper.GetSymbol(g.Key),
                        revenue = g.Sum(r => r.Price)
                    })
                    .ToList();

                monthlyRevenue.Insert(0, new
                {
                    month = date.ToString("MMM yyyy"),
                    currencies = monthlyByCurrency,
                    total = monthRepairs.Sum(r => r.Price) 
                });
            }

            // Durum dağılımı
            var statusDistribution = new[]
            {
        new { name = "Bekleyen", value = repairs.Count(r => r.StatusId != (int)RepairStatusEnum.IslemeAlindi && r.StatusId != (int)RepairStatusEnum.Tamamlandi && r.StatusId != (int)RepairStatusEnum.TeslimEdildi), color = "#f59e0b" },
        new { name = "İşlemde", value = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.IslemeAlindi), color = "#3b82f6" },
        new { name = "Tamamlanan", value = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi), color = "#10b981" },
        new { name = "Teslim", value = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.TeslimEdildi), color = "#6c757d" }
    };

            return Json(new
            {
                totalRepairs = repairs.Count(),
                activeRepairs = repairs.Count(r => r.StatusId != (int)RepairStatusEnum.TeslimEdildi),
                completedRepairs = repairs.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi || r.StatusId == (int)RepairStatusEnum.TeslimEdildi),
                revenueByCurrency = revenueByCurrency,
                monthlyRevenue = monthlyRevenue,
                statusDistribution = statusDistribution
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentRepairs(int count = 10) // son 10 tamir kaydı
        {
            var repairs = await _unitOfWork.RepairItems
                .GetQueryable()
                .Include(r => r.Personel)
                .Include(r => r.AppUser)
                .OrderByDescending(r => r.ReceivedDate)
                .Take(count)
                .Select(r => new
                {
                    r.Id,
                    r.TrackingCode,
                    r.ProductName,
                    customerName = r.AppUser.FullName,
                    personelName = r.Personel.FullName,
                    r.ReceivedDate,
                    status = ((RepairStatusEnum)(r.StatusId ?? 1)).GetDisplayName()
                })
                .ToListAsync();

            return Json(repairs);
        }

        // ==================== GELİR RAPORU API'LERİ ====================

        [HttpGet]
        public async Task<IActionResult> GetGelirRaporuStats(int year = 0)
        {
            if (year == 0) year = DateTime.Now.Year;

            var repairs = await _unitOfWork.RepairItems.GetAllAsync();
            repairs = repairs.Where(r => r.ReceivedDate.Year == year).ToList();

            // Para birimlerine göre toplam gelir
            var revenueByCurrency = repairs
                .GroupBy(r => r.Currency ?? "TRY")
                .Select(g => new
                {
                    currency = g.Key,
                    symbol = CurrencyHelper.GetSymbol(g.Key),
                    total = g.Sum(r => r.Price),
                    count = g.Count()
                })
                .ToList();

            // Aylık gelir (para birimi bazlı)
            var monthlyIncome = new List<object>();
            for (int i = 1; i <= 12; i++)
            {
                var monthRepairs = repairs.Where(r => r.ReceivedDate.Month == i).ToList();

                var monthlyByCurrency = monthRepairs
                    .GroupBy(r => r.Currency ?? "TRY")
                    .Select(g => new
                    {
                        currency = g.Key,
                        symbol = CurrencyHelper.GetSymbol(g.Key),
                        revenue = g.Sum(r => r.Price),
                        count = g.Count()
                    })
                    .ToList();

                monthlyIncome.Add(new
                {
                    month = i,
                    monthName = GetMonthName(i),
                    currencies = monthlyByCurrency,
                    totalRevenue = monthRepairs.Sum(r => r.Price),
                    totalCount = monthRepairs.Count(),
                    avgRevenue = monthRepairs.Any() ? monthRepairs.Average(r => r.Price) : 0
                });
            }

            // Yıllara göre karşılaştırma (son 5 yıl)
            var yearlyComparison = new List<object>();
            for (int i = year - 4; i <= year; i++)
            {
                var yearRepairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.ReceivedDate.Year == i);
                yearlyComparison.Add(new
                {
                    year = i,
                    revenue = yearRepairs.Sum(r => r.Price),
                    count = yearRepairs.Count()
                });
            }

            return Json(new
            {
                currentYear = year,
                revenueByCurrency = revenueByCurrency,
                totalRevenue = repairs.Sum(r => r.Price),
                totalRepairs = repairs.Count(),
                averageRevenue = repairs.Any() ? repairs.Average(r => r.Price) : 0,
                monthlyIncome = monthlyIncome,
                yearlyComparison = yearlyComparison
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetGelirRaporuYearly()
        {
            var currentYear = DateTime.Now.Year;
            var yearlyData = new List<object>();

            for (int i = currentYear - 4; i <= currentYear; i++)
            {
                var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.ReceivedDate.Year == i);
                yearlyData.Add(new
                {
                    year = i,
                    totalRevenue = repairs.Sum(r => r.Price),
                    totalRepairs = repairs.Count()
                });
            }

            return Json(yearlyData);
        }

        // ==================== PERSONEL PERFORMANS RAPORU API'LERİ ====================


        [HttpGet]
        public async Task<IActionResult> GetPersonelDropdownList()
        {
            var personels = await _unitOfWork.Personels
                .GetQueryable()
                .Where(p => p.IsActive)
                .Select(p => new { id = p.Id, name = p.FullName })
                .ToListAsync();
            return Json(personels);
        }

        [HttpGet]
        public async Task<IActionResult> GetPersonelPerformansRaporu(string startDate, string endDate, int? personelId = null)
        {
            IQueryable<RepairItem> query = _unitOfWork.RepairItems
                .GetQueryable()
                .Include(r => r.Personel)
                .Where(r => r.StatusId != (int)RepairStatusEnum.TeslimEdildi && r.StatusId != (int)RepairStatusEnum.Tamamlandi);

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                query = query.Where(r => r.ReceivedDate >= start);
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                query = query.Where(r => r.ReceivedDate <= end);
            }
            if (personelId.HasValue && personelId.Value > 0)
            {
                query = query.Where(r => r.PersonelId == personelId.Value);
            }

            var repairs = await query.ToListAsync();

            var personelList = await _unitOfWork.Personels
                .GetQueryable()
                .Where(p => p.IsActive)
                .Select(p => new { p.Id, p.FullName })
                .ToListAsync();

            var personelPerformance = repairs
                .Where(r => r.Personel != null)
                .GroupBy(r => new { r.PersonelId, r.Personel.FullName })
                .Select(g => new
                {
                    personelId = g.Key.PersonelId,
                    personelName = g.Key.FullName,
                    totalRepairs = g.Count(),
                    completedRepairs = g.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi),
                    inProgressRepairs = g.Count(r => r.StatusId == (int)RepairStatusEnum.IslemeAlindi),
                    pendingRepairs = g.Count(r => r.StatusId != (int)RepairStatusEnum.IslemeAlindi && r.StatusId != (int)RepairStatusEnum.Tamamlandi),
                    avgRepairTime = g.Where(r => r.DeliveryDate.HasValue)
                        .Select(r => (r.DeliveryDate.Value - r.ReceivedDate).TotalDays)
                        .DefaultIfEmpty(0)
                        .Average()
                })
                .OrderByDescending(x => x.totalRepairs)
                .ToList();

            return Json(new
            {
                personelList = personelList,
                personelPerformance = personelPerformance,
                totalRepairs = repairs.Count(),
                avgRepairTime = repairs.Where(r => r.DeliveryDate.HasValue)
                    .Select(r => (r.DeliveryDate.Value - r.ReceivedDate).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average()
            });
        }


        // ==================== ÜRÜN BAZLI RAPOR API'LERİ ====================

        [HttpGet]
        public async Task<IActionResult> GetUrunRaporu(string startDate, string endDate, string productName = null, string brand = null)
        {
            var repairs = await _unitOfWork.RepairItems
                .GetQueryable()
                .AsNoTracking()
                .Where(r => !string.IsNullOrEmpty(r.ProductName))
                .ToListAsync();

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }
            if (!string.IsNullOrEmpty(productName))
            {
                repairs = repairs.Where(r => r.ProductName.Contains(productName)).ToList();
            }
            if (!string.IsNullOrEmpty(brand))
            {
                repairs = repairs.Where(r => r.ProductBrand != null && r.ProductBrand.Contains(brand)).ToList();
            }

            // Top 10 ürün - para birimi bazlı gelir
            var topProducts = repairs
                .GroupBy(r => new { r.ProductName, r.ProductBrand, r.ProductModel })
                .Select(g => new
                {
                    productName = g.Key.ProductName,
                    brand = g.Key.ProductBrand ?? "-",
                    model = g.Key.ProductModel ?? "-",
                    count = g.Count(),
                    revenueByCurrency = g.GroupBy(r => r.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            total = cg.Sum(r => r.Price)
                        }).ToList(),
                    avgPriceByCurrency = g.GroupBy(r => r.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            avg = cg.Average(r => r.Price)
                        }).ToList(),
                    lastRepair = g.Max(r => r.ReceivedDate)
                })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToList();

            // Marka bazlı istatistikler - para birimi bazlı
            var brandStats = repairs
                .Where(r => !string.IsNullOrEmpty(r.ProductBrand))
                .GroupBy(r => r.ProductBrand)
                .Select(g => new
                {
                    brand = g.Key,
                    count = g.Count(),
                    revenueByCurrency = g.GroupBy(r => r.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            total = cg.Sum(r => r.Price)
                        }).ToList()
                })
                .OrderByDescending(x => x.count)
                .Take(5)
                .ToList();

            // Aylık ürün tamir trendi (son 12 ay) - para birimi bazlı
            var monthlyTrend = new List<object>();
            for (int i = 0; i < 12; i++)
            {
                var date = DateTime.Now.AddMonths(-i);
                var monthRepairs = repairs.Where(r => r.ReceivedDate.Year == date.Year && r.ReceivedDate.Month == date.Month);

                var monthlyByCurrency = monthRepairs
                    .GroupBy(r => r.Currency ?? "TRY")
                    .Select(cg => new
                    {
                        currency = cg.Key,
                        symbol = CurrencyHelper.GetSymbol(cg.Key),
                        revenue = cg.Sum(r => r.Price)
                    }).ToList();

                monthlyTrend.Insert(0, new
                {
                    month = date.ToString("MMM yyyy"),
                    count = monthRepairs.Count(),
                    currencies = monthlyByCurrency
                });
            }

            // Toplam istatistikler - para birimi bazlı
            var totalRevenueByCurrency = repairs
                .GroupBy(r => r.Currency ?? "TRY")
                .Select(g => new
                {
                    currency = g.Key,
                    symbol = CurrencyHelper.GetSymbol(g.Key),
                    total = g.Sum(r => r.Price)
                })
                .ToList();

            return Json(new
            {
                topProducts = topProducts,
                brandStats = brandStats,
                monthlyTrend = monthlyTrend,
                totalProducts = repairs.Select(r => r.ProductName).Distinct().Count(),
                totalRepairs = repairs.Count(),
                totalRevenueByCurrency = totalRevenueByCurrency
            });
        }


        // ==================== TAMİR SÜRE RAPORU API'LERİ ====================
        [HttpGet]
        public async Task<IActionResult> GetTamirSureRaporu(string startDate, string endDate, int? personelId = null)
        {
            // Tamamlanan (StatusId = 8) VEYA Teslim Edilen (DeliveryDate dolu) kayıtları al
            var repairs = await _unitOfWork.RepairItems
                .GetQueryable()
                .Include(r => r.Personel)
                .Where(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi || r.DeliveryDate.HasValue)
                .ToListAsync();

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }
            if (personelId.HasValue && personelId.Value > 0)
            {
                repairs = repairs.Where(r => r.PersonelId == personelId.Value).ToList();
            }

            if (!repairs.Any())
            {
                return Json(new
                {
                    avgRepairTime = 0,
                    minRepairTime = 0,
                    maxRepairTime = 0,
                    totalCompletedRepairs = 0,
                    personelAvgTime = new List<object>(),
                    fastestRepairs = new List<object>(),
                    slowestRepairs = new List<object>(),
                    monthlyTrend = new List<object>(),
                    personelList = await _unitOfWork.Personels.GetQueryable().Where(p => p.IsActive).Select(p => new { p.Id, p.FullName }).ToListAsync()
                });
            }

            // Süre hesaplama (DeliveryDate yoksa bugünün tarihini kullan)
            var today = DateTime.Today;
            var repairsWithTime = repairs.Select(r => new
            {
                r.Id,
                r.TrackingCode,
                r.ProductName,
                r.ProductBrand,
                r.Personel,
                r.ReceivedDate,
                r.DeliveryDate,
                RepairDays = r.DeliveryDate.HasValue
                    ? (r.DeliveryDate.Value - r.ReceivedDate).TotalDays
                    : (today - r.ReceivedDate).TotalDays  // Teslim edilmemişse bugüne kadar geçen süre
            }).ToList();

            // Sadece süresi 0'dan büyük olanları al (hatalı hesaplamayı engelle)
            var validRepairs = repairsWithTime.Where(r => r.RepairDays > 0).ToList();

            if (!validRepairs.Any())
            {
                return Json(new
                {
                    avgRepairTime = 0,
                    minRepairTime = 0,
                    maxRepairTime = 0,
                    totalCompletedRepairs = repairs.Count,
                    personelAvgTime = new List<object>(),
                    fastestRepairs = new List<object>(),
                    slowestRepairs = new List<object>(),
                    monthlyTrend = new List<object>(),
                    personelList = await _unitOfWork.Personels.GetQueryable().Where(p => p.IsActive).Select(p => new { p.Id, p.FullName }).ToListAsync()
                });
            }

            // Ortalama süreler
            var avgRepairTime = validRepairs.Average(r => r.RepairDays);
            var minRepairTime = validRepairs.Min(r => r.RepairDays);
            var maxRepairTime = validRepairs.Max(r => r.RepairDays);

            // Personel bazında ortalama süre
            var personelAvgTime = validRepairs
                .Where(r => r.Personel != null)
                .GroupBy(r => new { r.Personel.Id, r.Personel.FullName })
                .Select(g => new
                {
                    personelId = g.Key.Id,
                    personelName = g.Key.FullName,
                    avgDays = g.Average(r => r.RepairDays),
                    totalRepairs = g.Count(),
                    minDays = g.Min(r => r.RepairDays),
                    maxDays = g.Max(r => r.RepairDays)
                })
                .OrderBy(x => x.avgDays)
                .ToList();

            // En hızlı / en yavaş tamirler
            var fastestRepairs = validRepairs.OrderBy(r => r.RepairDays).Take(5).ToList();
            var slowestRepairs = validRepairs.OrderByDescending(r => r.RepairDays).Take(5).ToList();

            // Aylık ortalama süre trendi
            var monthlyTrend = new List<object>();
            for (int i = 0; i < 12; i++)
            {
                var date = DateTime.Now.AddMonths(-i);
                var monthRepairs = validRepairs.Where(r => r.ReceivedDate.Year == date.Year && r.ReceivedDate.Month == date.Month);
                var avgDays = monthRepairs.Any() ? monthRepairs.Average(r => r.RepairDays) : 0;
                monthlyTrend.Insert(0, new
                {
                    month = date.ToString("MMM yyyy"),
                    avgDays = Math.Round(avgDays, 1),
                    count = monthRepairs.Count()
                });
            }

            return Json(new
            {
                avgRepairTime = Math.Round(avgRepairTime, 1),
                minRepairTime = Math.Round(minRepairTime, 1),
                maxRepairTime = Math.Round(maxRepairTime, 1),
                totalCompletedRepairs = repairs.Count,
                personelAvgTime = personelAvgTime,
                fastestRepairs = fastestRepairs,
                slowestRepairs = slowestRepairs,
                monthlyTrend = monthlyTrend,
                personelList = await _unitOfWork.Personels.GetQueryable().Where(p => p.IsActive).Select(p => new { p.Id, p.FullName }).ToListAsync()
            });
        }



        // ==================== MÜŞTERİ BAZLI RAPOR API'LERİ ====================
        [HttpGet]
        public async Task<IActionResult> GetMusteriBazliRaporu(string startDate, string endDate,
     string customerName = null, string customerNumber = null,
     int page = 1, int pageSize = 20)
        {
            var repairs = await _unitOfWork.RepairItems
                .GetQueryable()
                .Include(r => r.AppUser)
                .Where(r => r.AppUser != null)
                .ToListAsync();

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                repairs = repairs.Where(r => r.ReceivedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                repairs = repairs.Where(r => r.ReceivedDate <= end).ToList();
            }
            if (!string.IsNullOrEmpty(customerName))
            {
                repairs = repairs.Where(r => r.AppUser.FullName.Contains(customerName)).ToList();
            }
            if (!string.IsNullOrEmpty(customerNumber))
            {
                repairs = repairs.Where(r => r.CustomerNumber != null && r.CustomerNumber.Contains(customerNumber)).ToList();
            }

            // Müşteri bazlı istatistikler - PARA BİRİMİ BAZLI
            var allCustomerStats = repairs
                .GroupBy(r => new { r.AppUserId, r.AppUser.FullName, r.AppUser.CustomerNumber })
                .Select(g => new
                {
                    customerId = g.Key.AppUserId,
                    customerName = g.Key.FullName,
                    customerNumber = g.Key.CustomerNumber ?? "-",
                    totalRepairs = g.Count(),
                    completedRepairs = g.Count(r => r.StatusId == (int)RepairStatusEnum.Tamamlandi || r.StatusId == (int)RepairStatusEnum.TeslimEdildi),
                    inProgressRepairs = g.Count(r => r.StatusId == (int)RepairStatusEnum.IslemeAlindi),
                    // PARA BİRİMİ BAZLI GELİR
                    revenueByCurrency = g.GroupBy(r => r.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            total = cg.Sum(r => r.Price)
                        }).ToList(),
                    // PARA BİRİMİ BAZLI ORTALAMA
                    avgRevenueByCurrency = g.GroupBy(r => r.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            avg = cg.Average(r => r.Price)
                        }).ToList(),
                    lastRepair = g.Max(r => r.ReceivedDate)
                })
                .OrderByDescending(x => x.totalRepairs)  // Toplam tamir sayısına göre sırala (para birimi sorunu yok)
                .ToList();

            // Toplam istatistikler - PARA BİRİMİ BAZLI
            var totalRevenueByCurrency = repairs
                .GroupBy(r => r.Currency ?? "TRY")
                .Select(g => new
                {
                    currency = g.Key,
                    symbol = CurrencyHelper.GetSymbol(g.Key),
                    total = g.Sum(r => r.Price)
                })
                .ToList();

            var totalStats = new
            {
                totalCustomers = allCustomerStats.Count,
                totalRepairs = allCustomerStats.Sum(x => x.totalRepairs),
                totalRevenueByCurrency = totalRevenueByCurrency,
                avgRepairsPerCustomer = allCustomerStats.Any() ? allCustomerStats.Average(x => x.totalRepairs) : 0
            };

            // En çok tamir yapan müşteriler (Top 5) - tamir sayısına göre
            var topCustomers = allCustomerStats.Take(5).ToList();

            // Pagination
            var totalCount = allCustomerStats.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
            var skip = (page - 1) * pageSize;
            var pagedCustomers = allCustomerStats.Skip(skip).Take(pageSize).ToList();

            return Json(new
            {
                customerStats = pagedCustomers,
                totalStats = totalStats,
                topCustomers = topCustomers,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = totalPages,
                    totalCount = totalCount
                }
            });
        }


        // ==================== TEKLİF RAPORU API'LERİ ====================
        [HttpGet]
        public async Task<IActionResult> GetTeklifRaporu(string startDate, string endDate, string customerName = null, string customerNumber = null)
        {
            var offers = await _unitOfWork.Offers
                .GetQueryable()
                .Include(o => o.OfferLines)
                .ToListAsync();

            if (!string.IsNullOrEmpty(startDate))
            {
                var start = DateTime.Parse(startDate);
                offers = offers.Where(o => o.CreatedDate >= start).ToList();
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                var end = DateTime.Parse(endDate).AddDays(1);
                offers = offers.Where(o => o.CreatedDate <= end).ToList();
            }
            if (!string.IsNullOrEmpty(customerName))
            {
                offers = offers.Where(o => o.CustomerName.Contains(customerName)).ToList();
            }

            if (!string.IsNullOrEmpty(customerNumber))
            {
                offers = offers.Where(o => o.CustomerNumber != null && o.CustomerNumber.Contains(customerNumber)).ToList();
            }

            // Durum bazlı istatistikler
            var activeOffers = offers.Count(o => o.IsActive);
            var approvedOffers = offers.Count(o => !o.IsActive);
            var totalOffers = offers.Count;

            // Versiyon bazlı istatistikler
            var versionStats = offers
                .GroupBy(o => o.Version)
                .Select(g => new
                {
                    version = g.Key,
                    count = g.Count(),
                    totalAmount = g.Sum(o => o.GrandTotal)
                })
                .OrderBy(x => x.version)
                .ToList();

            // Aylık teklif trendi (son 12 ay)
            var monthlyTrend = new List<object>();
            for (int i = 0; i < 12; i++)
            {
                var date = DateTime.Now.AddMonths(-i);
                var monthOffers = offers.Where(o => o.CreatedDate.Year == date.Year && o.CreatedDate.Month == date.Month);
                monthlyTrend.Insert(0, new
                {
                    month = date.ToString("MMM yyyy"),
                    total = monthOffers.Count(),
                    approved = monthOffers.Count(o => !o.IsActive),
                    amount = monthOffers.Sum(o => o.GrandTotal)
                });
            }

            // Müşteri bazlı teklif istatistikleri (Top 10) - PARA BİRİMİ BAZLI
            var customerStats = offers
                .GroupBy(o => new { o.CustomerNumber, o.CustomerName })
                .Select(g => new
                {
                    customerName = g.Key.CustomerName,
                    customerNumber = g.Key.CustomerNumber,
                    totalOffers = g.Count(),
                    approvedOffers = g.Count(o => !o.IsActive),
                    activeOffers = g.Count(o => o.IsActive),
                    // PARA BİRİMİ BAZLI TOPLAM TUTAR
                    amountByCurrency = g.GroupBy(o => o.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            total = cg.Sum(o => o.GrandTotal)
                        }).ToList(),
                    // PARA BİRİMİ BAZLI ORTALAMA
                    avgByCurrency = g.GroupBy(o => o.Currency ?? "TRY")
                        .Select(cg => new
                        {
                            currency = cg.Key,
                            symbol = CurrencyHelper.GetSymbol(cg.Key),
                            avg = cg.Average(o => o.GrandTotal)
                        }).ToList(),
                    lastOffer = g.Max(o => o.CreatedDate)
                })
                .OrderByDescending(x => x.amountByCurrency.Sum(a => a.total))
                .Take(10)
                .ToList();

            // Toplam gelir - PARA BİRİMİ BAZLI
            var totalRevenueByCurrency = offers
                .GroupBy(o => o.Currency ?? "TRY")
                .Select(g => new
                {
                    currency = g.Key,
                    symbol = CurrencyHelper.GetSymbol(g.Key),
                    total = g.Sum(o => o.GrandTotal)
                })
                .ToList();

            return Json(new
            {
                totalOffers = totalOffers,
                activeOffers = activeOffers,
                approvedOffers = approvedOffers,
                totalRevenueByCurrency = totalRevenueByCurrency,
                approvalRate = totalOffers > 0 ? (approvedOffers / (double)totalOffers) * 100 : 0,
                versionStats = versionStats,
                monthlyTrend = monthlyTrend,
                customerStats = customerStats
            });
        }
    }
}