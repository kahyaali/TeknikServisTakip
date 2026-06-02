using Business.Abstract;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PuppeteerSharp; // pdf kütüphanesi
using PuppeteerSharp.Media; // pdf kütüphanesi
using System.ComponentModel;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using TeknikServisTakip.Helpers;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari,Depo")]
    public class StockController : Controller
    {
        private readonly IProductService _productService;
        private readonly IUnitOfWork _unitOfWork;

        public StockController(IProductService productService, IUnitOfWork unitOfWork)
        {
            _productService = productService;
            _unitOfWork = unitOfWork;
        }

        // GET: /Stock/Index
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string search = "")
        {
            ViewBag.SearchTerm = search;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            var result = await _productService.GetPagedAsync(page, pageSize, search);
            ViewBag.TotalCount = result.TotalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);

            return View(result.Items);
        }

        // POST: /Stock/GetProducts (Server-side DataTable için)
        [HttpPost]
        public async Task<IActionResult> GetProducts(int draw, int start, int length, string search = "")
        {
            var page = (start / length) + 1;
            var pageSize = length;

            var result = await _productService.GetPagedAsync(page, pageSize, search);

            var data = result.Items.Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.ProductName,
                category = p.Category?.Name ?? "-",
                p.Brand,
                p.Quantity,
                p.MinStockLevel,
                p.MaxStockLevel,
                p.Location,
                StockStatus = GetStockStatus(p.Quantity, p.MinStockLevel, p.MaxStockLevel),
                StatusClass = _productService.GetStockStatusClass(p.Quantity, p.MinStockLevel, p.MaxStockLevel),
                p.IsActive
            });

            return Json(new
            {
                draw = draw,
                recordsTotal = result.TotalCount,
                recordsFiltered = result.TotalCount,
                data = data
            });
        }

        private async Task LoadCategoriesToViewBag()
        {
            var categories = await _unitOfWork.Categories.GetWhereAsync(c => c.IsActive);
            ViewBag.Categories = categories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToList();
        }

        private void LoadCurrenciesToViewBag()
        {
            ViewBag.CurrencyList = CurrencyHelper.GetCurrencyList();
        }

        // GET: /Stock/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
           
            await LoadCategoriesToViewBag();
            LoadCurrenciesToViewBag();
            return View();
        }

       

        // POST: /Stock/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (string.IsNullOrEmpty(product.ProductCode))
            {
                ModelState.AddModelError("ProductCode", "Ürün kodu zorunludur.");
            }

            if (string.IsNullOrEmpty(product.Currency))
            {
                product.Currency = "TRY"; 
            }

            ModelState.Remove("Currency");

            if (!ModelState.IsValid)
            {
                var hatalar = new List<string>();
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        hatalar.Add($"{key}: {error.ErrorMessage}");
                        System.Diagnostics.Debug.WriteLine($"*** HATA - {key}: {error.ErrorMessage} ***");
                    }
                }

                // Hataları TempData'ya da yaz
                TempData["Error"] = string.Join(" | ", hatalar);

                await LoadCategoriesToViewBag();
                LoadCurrenciesToViewBag();
                return View(product);
            }

            try
                {
                    var userId = User.Identity?.Name ?? "System";
                    var created = await _productService.AddAsync(product, userId);
                    TempData["Success"] = $"Ürün başarıyla eklendi. Ürün Kodu: {created.ProductCode}";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            

            await LoadCategoriesToViewBag();
            LoadCurrenciesToViewBag();
            return View(product);
        }

        // GET: /Stock/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
       
            await LoadCategoriesToViewBag();
            LoadCurrenciesToViewBag();
            return View(product);
        }

       
        // POST: /Stock/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            ModelState.Remove("ProductCode");
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = string.Join(", ", errors);

                await LoadCategoriesToViewBag();
                LoadCurrenciesToViewBag();
                return View(product);
            }

            try
            {
              
                var existProduct = await _productService.GetByIdAsync(id);
                if (existProduct == null)
                {
                    return NotFound();
                }

                existProduct.ProductName = product.ProductName;
                existProduct.CategoryId = product.CategoryId;
                existProduct.Unit = product.Unit;
                existProduct.Brand = product.Brand;
                existProduct.Model = product.Model;
                existProduct.SerialNo = product.SerialNo;
                existProduct.IMEINo = product.IMEINo;
                existProduct.Quantity = product.Quantity;
                existProduct.MinStockLevel = product.MinStockLevel;
                existProduct.MaxStockLevel = product.MaxStockLevel;
                existProduct.Location = product.Location;
                existProduct.PurchasePrice = product.PurchasePrice;
                existProduct.SalePrice = product.SalePrice;
                existProduct.Supplier = product.Supplier;
                existProduct.Description = product.Description;
                existProduct.Notes = product.Notes;

           
                existProduct.Currency = product.Currency;

                var userId = User.Identity?.Name ?? "System";

          
                await _productService.UpdateAsync(existProduct, userId);

                TempData["Success"] = "Ürün başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            await LoadCategoriesToViewBag();
            LoadCurrenciesToViewBag();
            return View(product);
        }


        // GET: /Stock/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _unitOfWork.Products
        .GetByIdWithIncludeAsync(id, p => p.Category);
            if (product == null)
            {
                return NotFound();
            }

            var movements = await _unitOfWork.StockMovements
                .GetWhereAsync(m => m.ProductId == id, m => m.Product);

            ViewBag.Movements = movements.OrderByDescending(m => m.CreatedAt).Take(20);
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var allProducts = await _productService.GetAllAsync();
            var activeProducts = allProducts.Where(p => p.IsActive);

            var lowStock = await _productService.GetLowStockProductsAsync();
            var criticalStock = await _productService.GetCriticalStockProductsAsync();

            return Json(new
            {
                lowStockCount = lowStock.Count,
                criticalCount = criticalStock.Count,
                totalProducts = activeProducts.Count(),
                totalStockQuantity = activeProducts.Sum(p => p.Quantity)
            });
        }

        // POST: /Stock/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";
                var result = await _productService.SoftDeleteAsync(id, userId);
                if (result)
                {
                    return Json(new { success = true, message = "Ürün başarıyla silindi." });
                }
                return Json(new { success = false, message = "Ürün bulunamadı." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Stock/HardDelete (Tamamen sil)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HardDelete(int id)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";

                // Ürünü bul
                var product = await _productService.GetByIdAsync(id);
                if (product == null)
                {
                    return Json(new { success = false, message = "Ürün bulunamadı." });
                }

                // İlişkili kayıtları kontrol et
                var hasStockMovements = await _unitOfWork.StockMovements.GetWhereAsync(m => m.ProductId == id);
                if (hasStockMovements.Any())
                {
                    return Json(new { success = false, message = "Bu ürüne ait stok hareketleri var. Önce onları silmelisiniz!" });
                }

                var hasStockAlerts = await _unitOfWork.StockAlerts.GetWhereAsync(a => a.ProductId == id);
                if (hasStockAlerts.Any())
                {
                    return Json(new { success = false, message = "Bu ürüne ait stok uyarıları var. Önce onları silmelisiniz!" });
                }

                // Hard delete - tamamen sil
                _unitOfWork.Products.Delete(product);
                await _unitOfWork.CompleteAsync();

                return Json(new { success = true, message = "Ürün tamamen silindi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Stock/StockIn (Modal ile stok giriş)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(int productId, int quantity, string referenceNo, string description)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Miktar 0'dan büyük olmalıdır." });
            }

            try
            {
                var userId = User.Identity?.Name ?? "System";
                var result = await _productService.StockInAsync(productId, quantity, referenceNo, description, userId);

                if (result.Success)
                {
                    return Json(new { success = true, message = result.Message, newStock = result.NewStock });
                }
                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Stock/StockOut (Modal ile stok çıkış)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(int productId, int quantity, string referenceNo, string description)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Miktar 0'dan büyük olmalıdır." });
            }

            try
            {
                var userId = User.Identity?.Name ?? "System";
                var result = await _productService.StockOutAsync(productId, quantity, referenceNo, description, userId);

                if (result.Success)
                {
                    return Json(new { success = true, message = result.Message, newStock = result.NewStock });
                }
                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Stock/ExportExcel
        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            var products = await _unitOfWork.Products.GetAllWithIncludeAsync(p => p.Category);
            var activeProducts = products.Where(p => p.IsActive).ToList();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("StokListesi");

                // Başlıklar (Genişletildi)
                worksheet.Cells[1, 1].Value = "Ürün Kodu";
                worksheet.Cells[1, 2].Value = "Ürün Adı";
                worksheet.Cells[1, 3].Value = "Kategori";
                worksheet.Cells[1, 4].Value = "Marka";
                worksheet.Cells[1, 5].Value = "Model";
                worksheet.Cells[1, 6].Value = "Seri No";
                worksheet.Cells[1, 7].Value = "IMEI No";
                worksheet.Cells[1, 8].Value = "Miktar";
                worksheet.Cells[1, 9].Value = "Lokasyon";
                worksheet.Cells[1, 10].Value = "Tedarikçi";
                worksheet.Cells[1, 11].Value = "Alış Fiyatı";
                worksheet.Cells[1, 12].Value = "Satış Fiyatı";

                using (var range = worksheet.Cells[1, 1, 1, 12])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Veriler
                int row = 2;
                foreach (var product in activeProducts)
                {
                    worksheet.Cells[row, 1].Value = product.ProductCode;
                    worksheet.Cells[row, 2].Value = product.ProductName;
                    worksheet.Cells[row, 3].Value = product.Category?.Name ?? "-";
                    worksheet.Cells[row, 4].Value = product.Brand;
                    worksheet.Cells[row, 5].Value = product.Model;
                    worksheet.Cells[row, 6].Value = product.SerialNo ?? "-";
                    worksheet.Cells[row, 7].Value = product.IMEINo ?? "-";
                    worksheet.Cells[row, 8].Value = product.Quantity;
                    worksheet.Cells[row, 9].Value = product.Location;
                    worksheet.Cells[row, 10].Value = product.Supplier;
                    worksheet.Cells[row, 11].Value = product.PurchasePrice;
                    worksheet.Cells[row, 12].Value = product.SalePrice;
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"StokListesi_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }


        // GET: /Stock/StockMovements/{productId}
        [HttpGet]
        public async Task<IActionResult> StockMovements(int id, int take = 50)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var movements = await _unitOfWork.StockMovements
                .GetWhereAsync(m => m.ProductId == id);

            ViewBag.Product = product;
            return View(movements.OrderByDescending(m => m.CreatedAt).Take(take));
        }

        // GET: /Stock/Alerts
        [HttpGet]
        public async Task<IActionResult> Alerts()
        {
            var lowStock = await _productService.GetLowStockProductsAsync();
            var criticalStock = await _productService.GetCriticalStockProductsAsync();

            ViewBag.LowStock = lowStock;
            ViewBag.CriticalStock = criticalStock;
            return View();
        }

        // POST: /Stock/SendAlerts (Manuel uyarı gönder)
        [HttpPost]
        public async Task<IActionResult> SendAlerts()
        {
            try
            {
                await _productService.SendStockAlertsAsync();
                TempData["Success"] = "Stok uyarı maili gönderildi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Mail gönderilirken hata: {ex.Message}";
            }
            return RedirectToAction(nameof(Alerts));
        }

        // Helper metod
        private string GetStockStatus(int quantity, int minStock, int maxStock)
        {
            if (quantity == 0) return "Kritik";
            if (quantity <= minStock) return "Düşük Stok";
            if (quantity >= maxStock) return "Stok Fazlası";
            return "Normal";
        }


        // GET: /Stock/ExportTemplate (Şablon indir)
        [HttpGet]
        public IActionResult ExportTemplate()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("UrunSablonu");

                // 🔥 BAŞLIKLAR - Seri No ve IMEI No eklendi
                worksheet.Cells[1, 1].Value = "Ürün Kodu*";
                worksheet.Cells[1, 2].Value = "Ürün Adı*";
                worksheet.Cells[1, 3].Value = "Kategori";
                worksheet.Cells[1, 4].Value = "Marka";
                worksheet.Cells[1, 5].Value = "Model";
                worksheet.Cells[1, 6].Value = "Seri No";
                worksheet.Cells[1, 7].Value = "IMEI No";
                worksheet.Cells[1, 8].Value = "Miktar*";
                worksheet.Cells[1, 9].Value = "Min Stok";
                worksheet.Cells[1, 10].Value = "Max Stok";
                worksheet.Cells[1, 11].Value = "Lokasyon";
                worksheet.Cells[1, 12].Value = "Tedarikçi";
                worksheet.Cells[1, 13].Value = "Alış Fiyatı";
                worksheet.Cells[1, 14].Value = "Satış Fiyatı";
                worksheet.Cells[1, 15].Value = "Açıklama";

                // Başlık stili
                using (var range = worksheet.Cells[1, 1, 1, 15])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Örnek veri
                worksheet.Cells[2, 1].Value = "TEL-001";           // Ürün Kodu
                worksheet.Cells[2, 2].Value = "iPhone 14";         // Ürün Adı
                worksheet.Cells[2, 3].Value = "Elektronik";        // Kategori
                worksheet.Cells[2, 4].Value = "Apple";             // Marka
                worksheet.Cells[2, 5].Value = "iPhone 14";         // Model
                worksheet.Cells[2, 6].Value = "SN123456789";       // Seri No
                worksheet.Cells[2, 7].Value = "123456789012345";   // IMEI No
                worksheet.Cells[2, 8].Value = 10;                  // Miktar
                worksheet.Cells[2, 9].Value = 5;                   // Min Stok
                worksheet.Cells[2, 10].Value = 100;                // Max Stok
                worksheet.Cells[2, 11].Value = "A-01";             // Lokasyon
                worksheet.Cells[2, 12].Value = "Tedarikçi A.Ş.";   // Tedarikçi
                worksheet.Cells[2, 13].Value = 5000;               // Alış Fiyatı
                worksheet.Cells[2, 14].Value = 7500;               // Satış Fiyatı
                worksheet.Cells[2, 15].Value = "Örnek açıklama";   // Açıklama

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UrunExcelSablonu.xlsx");
            }
        }

        // GET: /Stock/BulkImport
        [HttpGet]
        public IActionResult BulkImport()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImport(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                return Json(new { success = false, message = "Lütfen bir Excel dosyası seçin!" });
            }


            var products = new List<Product>();
            var errors = new List<string>();
            var successCount = 0;

            // Excel içi benzersizlik kontrolü için listeler
            var excelProductCodes = new HashSet<string>();
            var excelSerialNos = new HashSet<string>();
            var excelImeiNos = new HashSet<string>();

            using (var stream = new MemoryStream())
            {
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        try
                        {
                            // Sütunları oku
                            var productCode = worksheet.Cells[row, 1]?.Text?.Trim();
                            var productName = worksheet.Cells[row, 2]?.Text?.Trim();
                            var categoryName = worksheet.Cells[row, 3]?.Text?.Trim();
                            var brand = worksheet.Cells[row, 4]?.Text?.Trim();
                            var model = worksheet.Cells[row, 5]?.Text?.Trim();
                            var serialNo = worksheet.Cells[row, 6]?.Text?.Trim();
                            var imeiNo = worksheet.Cells[row, 7]?.Text?.Trim();
                            var quantityText = worksheet.Cells[row, 8]?.Text?.Trim();
                            var minStockText = worksheet.Cells[row, 9]?.Text?.Trim();
                            var maxStockText = worksheet.Cells[row, 10]?.Text?.Trim();
                            var location = worksheet.Cells[row, 11]?.Text?.Trim();
                            var supplier = worksheet.Cells[row, 12]?.Text?.Trim();
                            var purchasePriceText = worksheet.Cells[row, 13]?.Text?.Trim();
                            var salePriceText = worksheet.Cells[row, 14]?.Text?.Trim();
                            var description = worksheet.Cells[row, 15]?.Text?.Trim();

                            var rowErrors = new List<string>();

                            // ========== ZORUNLU ALAN KONTROLLERİ ==========
                            if (string.IsNullOrEmpty(productCode))
                                rowErrors.Add("Ürün kodu boş olamaz");
                            if (string.IsNullOrEmpty(productName))
                                rowErrors.Add("Ürün adı boş olamaz");
                            if (!int.TryParse(quantityText, out int quantity) || quantity <= 0)
                                rowErrors.Add("Geçerli bir miktar giriniz");

                            // ========== EXCEL İÇİ BENZERSİZLİK KONTROLÜ ==========
                            if (!string.IsNullOrEmpty(productCode) && excelProductCodes.Contains(productCode))
                                rowErrors.Add($"'{productCode}' ürün kodu Excel içinde tekrar ediyor");

                            if (!string.IsNullOrEmpty(serialNo) && excelSerialNos.Contains(serialNo))
                                rowErrors.Add($"'{serialNo}' seri numarası Excel içinde tekrar ediyor");

                            if (!string.IsNullOrEmpty(imeiNo) && excelImeiNos.Contains(imeiNo))
                                rowErrors.Add($"'{imeiNo}' IMEI numarası Excel içinde tekrar ediyor");

                            // ========== DB BENZERSİZLİK KONTROLLERİ ==========
                            if (!string.IsNullOrEmpty(productCode))
                            {
                                var existingProductCode = await _unitOfWork.Products
                                    .GetSingleAsync(p => p.ProductCode == productCode);
                                if (existingProductCode != null)
                                    rowErrors.Add($"'{productCode}' ürün kodu zaten kullanımda");
                            }

                            if (!string.IsNullOrEmpty(serialNo))
                            {
                                var existingSerialNo = await _unitOfWork.Products
                                    .GetSingleAsync(p => p.SerialNo == serialNo);
                                if (existingSerialNo != null)
                                    rowErrors.Add($"'{serialNo}' seri numarası zaten kullanımda");
                            }

                            if (!string.IsNullOrEmpty(imeiNo))
                            {
                                var existingImeiNo = await _unitOfWork.Products
                                    .GetSingleAsync(p => p.IMEINo == imeiNo);
                                if (existingImeiNo != null)
                                    rowErrors.Add($"'{imeiNo}' IMEI numarası zaten kullanımda");
                            }

                            // ========== HATA VARSA KAYDETME ==========
                            if (rowErrors.Any())
                            {
                                errors.Add($"Satır {row}: {string.Join(" | ", rowErrors)}");
                                continue;
                            }

                            // ========== EXCEL LİSTELERİNE EKLE ==========
                            if (!string.IsNullOrEmpty(productCode)) excelProductCodes.Add(productCode);
                            if (!string.IsNullOrEmpty(serialNo)) excelSerialNos.Add(serialNo);
                            if (!string.IsNullOrEmpty(imeiNo)) excelImeiNos.Add(imeiNo);

                            // ========== KATEGORİ KONTROLÜ ==========
                            int? categoryId = null;
                            if (!string.IsNullOrEmpty(categoryName))
                            {
                                var category = await _unitOfWork.Categories
                                    .GetSingleAsync(c => c.Name == categoryName && c.IsActive);
                                if (category != null)
                                    categoryId = category.Id;
                                else
                                {
                                    errors.Add($"Satır {row}: '{categoryName}' kategorisi bulunamadı");
                                    continue;
                                }
                            }

                            // Ürünü oluştur
                            var product = new Product
                            {
                                ProductCode = productCode,
                                ProductName = productName,
                                CategoryId = categoryId,
                                Brand = brand,
                                Model = model,
                                SerialNo = serialNo,
                                IMEINo = imeiNo,
                                Quantity = quantity,
                                MinStockLevel = int.TryParse(minStockText, out int min) ? min : 5,
                                MaxStockLevel = int.TryParse(maxStockText, out int max) ? max : 100,
                                Location = location,
                                Supplier = supplier,
                                PurchasePrice = decimal.TryParse(purchasePriceText, out decimal pp) ? pp : null,
                                SalePrice = decimal.TryParse(salePriceText, out decimal sp) ? sp : null,
                                Description = description,
                                IsActive = true
                            };

                            products.Add(product);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Satır {row}: İşlem hatası - {ex.Message}");
                        }
                    }
                }
            }

            // Veritabanına toplu kaydet 
            if (products.Any())
            {
                var userId = User.Identity?.Name ?? "System";

                foreach (var product in products)
                {
                    try
                    {
                        product.CreatedBy = userId;
                        product.CreatedAt = DateTime.Now;
                        await _unitOfWork.Products.AddAsync(product);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"'{product.ProductName}' eklenirken hata: {ex.Message}");
                        continue;
                    }
                }

                await _unitOfWork.CompleteAsync();
                successCount = products.Count;
            }

            var resultMessage = $"Toplam {successCount} ürün başarıyla eklendi.";
            if (errors.Any())
                resultMessage += $" {errors.Count} hata oluştu.";

            return Json(new
            {
                success = true,
                successCount = successCount,
                totalRows = products.Count + errors.Count,
                errors = errors,
                message = resultMessage
            });
        }

      

        [HttpGet]
        public async Task<IActionResult> GetAlertCount()
        {
            var lowStock = await _productService.GetLowStockProductsAsync();
            var criticalStock = await _productService.GetCriticalStockProductsAsync();

            return Json(new
            {
                lowStockCount = lowStock.Count,
                criticalCount = criticalStock.Count
            });
        }

        // GET: /Stock/StockMovementsAll 
        [HttpGet]
        public IActionResult StockMovementsAll()
        {
            return View();
        }

        // Server side Pagination
        [HttpPost]
        public async Task<IActionResult> GetStockMovementsJson(int draw, int start, int length, string search = null,
         string movementType = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _unitOfWork.StockMovements.GetQueryable()
                    .Include(m => m.Product)
                    .AsQueryable();

                // Arama filtresi
                if (!string.IsNullOrEmpty(search))
                {
                    var searchTerm = search.Trim().ToLower();
                    query = query.Where(m =>
                        (m.Product != null && m.Product.ProductName != null && m.Product.ProductName.ToLower().Contains(searchTerm)) ||
                        (m.Product != null && m.Product.ProductCode != null && m.Product.ProductCode.ToLower().Contains(searchTerm)) ||
                        (m.ReferenceNo != null && m.ReferenceNo.ToLower().Contains(searchTerm)) ||
                        (m.Description != null && m.Description.ToLower().Contains(searchTerm))
                    );
                }

                // Hareket tipi filtresi
                if (!string.IsNullOrEmpty(movementType))
                {
                    query = query.Where(m => m.MovementType == movementType);
                }

                //  Tarih filtresi 
                if (startDate.HasValue)
                {
                    var startDateValue = startDate.Value.Date;
                    query = query.Where(m => m.CreatedAt >= startDateValue);
                }
                if (endDate.HasValue)
                {
                    var endDateValue = endDate.Value.Date.AddDays(1);
                    query = query.Where(m => m.CreatedAt < endDateValue);
                }

                var totalCount = await query.CountAsync();

                var movements = await query
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip(start)  // start parametresi (DataTable'dan gelen)
                    .Take(length <= 0 ? 10 : length)
                    .Select(m => new
                    {
                        m.Id,
                        CreatedAt = m.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                        ProductCode = m.Product != null ? m.Product.ProductCode : "-",
                        ProductName = m.Product != null ? m.Product.ProductName : "-",
                        m.MovementType,
                        m.Quantity,
                        m.PreviousStock,
                        m.NewStock,
                        ReferenceNo = m.ReferenceNo ?? "-",
                        Description = m.Description ?? "-",
                        CreatedBy = m.CreatedBy ?? "-"
                    })
                    .ToListAsync();

                var data = movements.Select(m => new
                {
                    m.Id,
                    m.CreatedAt,
                    m.ProductCode,
                    m.ProductName,
                    MovementName = m.MovementType switch
                    {
                        "IN" => "Stok Girişi",
                        "OUT" => "Stok Çıkışı",
                        "ADJUST_IN" => "Düzeltme (+)",
                        "ADJUST_OUT" => "Düzeltme (-)",
                        _ => m.MovementType
                    },
                    MovementClass = m.MovementType switch
                    {
                        "IN" => "success",
                        "OUT" => "danger",
                        "ADJUST_IN" => "info",
                        "ADJUST_OUT" => "warning",
                        _ => "secondary"
                    },
                    m.Quantity,
                    m.PreviousStock,
                    m.NewStock,
                    m.ReferenceNo,
                    m.Description,
                    m.CreatedBy
                });

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalCount,
                    recordsFiltered = totalCount,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = draw,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }


        // Eexport Movements
        [HttpGet]
        public async Task<IActionResult> ExportMovementsExcel(DateTime? startDate, DateTime? endDate, string? movementType)
        {
            var movements = await _unitOfWork.StockMovements.GetAllWithIncludeAsync(m => m.Product);

            if (startDate.HasValue)
                movements = movements.Where(m => m.CreatedAt.Date >= startDate.Value.Date).ToList();
            if (endDate.HasValue)
                movements = movements.Where(m => m.CreatedAt.Date <= endDate.Value.Date).ToList();
            if (!string.IsNullOrEmpty(movementType))
                movements = movements.Where(m => m.MovementType == movementType).ToList();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("StokHareketleri");

                // Başlıklar
                worksheet.Cells[1, 1].Value = "Tarih";
                worksheet.Cells[1, 2].Value = "Ürün Kodu";
                worksheet.Cells[1, 3].Value = "Ürün Adı";
                worksheet.Cells[1, 4].Value = "İşlem Tipi";
                worksheet.Cells[1, 5].Value = "Miktar";
                worksheet.Cells[1, 6].Value = "Önceki Stok";
                worksheet.Cells[1, 7].Value = "Yeni Stok";
                worksheet.Cells[1, 8].Value = "Referans No";
                worksheet.Cells[1, 9].Value = "Açıklama";
                worksheet.Cells[1, 10].Value = "İşlemi Yapan";

                using (var range = worksheet.Cells[1, 1, 1, 10])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                int row = 2;
                foreach (var item in movements.OrderByDescending(m => m.CreatedAt))
                {
                    // İŞLEM TİPİNİ TÜRKÇELEŞTİR
                    var turkishMovementType = item.MovementType switch
                    {
                        "IN" => "Giriş",
                        "OUT" => "Çıkış",
                        "ADJUST_IN" => "Düzeltme Giriş",
                        "ADJUST_OUT" => "Düzeltme Çıkış",
                        _ => item.MovementType
                    };

                    worksheet.Cells[row, 1].Value = item.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 2].Value = item.Product?.ProductCode;
                    worksheet.Cells[row, 3].Value = item.Product?.ProductName;
                    worksheet.Cells[row, 4].Value = turkishMovementType;  
                    worksheet.Cells[row, 5].Value = item.Quantity;
                    worksheet.Cells[row, 6].Value = item.PreviousStock;
                    worksheet.Cells[row, 7].Value = item.NewStock;
                    worksheet.Cells[row, 8].Value = item.ReferenceNo;
                    worksheet.Cells[row, 9].Value = item.Description;
                    worksheet.Cells[row, 10].Value = item.CreatedBy;
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"StokHareketleri_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        // GET: /Stock/ExportMovementsPdf
        [HttpGet]
        public async Task<IActionResult> ExportMovementsPdf(DateTime? startDate, DateTime? endDate, string? movementType)
        {
            var movements = await _unitOfWork.StockMovements.GetAllWithIncludeAsync(m => m.Product);

            if (startDate.HasValue)
                movements = movements.Where(m => m.CreatedAt.Date >= startDate.Value.Date).ToList();
            if (endDate.HasValue)
                movements = movements.Where(m => m.CreatedAt.Date <= endDate.Value.Date).ToList();
            if (!string.IsNullOrEmpty(movementType))
                movements = movements.Where(m => m.MovementType == movementType).ToList();

            var filterText = "";
            if (startDate.HasValue) filterText += $"Başlangıç: {startDate.Value:dd.MM.yyyy} ";
            if (endDate.HasValue) filterText += $"Bitiş: {endDate.Value:dd.MM.yyyy} ";
            if (!string.IsNullOrEmpty(movementType))
            {
                var typeName = movementType switch { "IN" => "Stok Girişi", "OUT" => "Stok Çıkışı", "ADJUST_IN" => "Düzeltme(+)", "ADJUST_OUT" => "Düzeltme(-)", _ => movementType };
                filterText += $"İşlem Tipi: {typeName}";
            }

            var html = @$"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Stok Hareketleri</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'DejaVu Sans', Arial, sans-serif; 
            margin: 5px; 
            padding: 5px;
            font-size: 6.5px;
        }}
        h2 {{ color: #0d6efd; text-align: center; margin-bottom: 8px; font-size: 12px; }}
        .date {{ text-align: right; font-size: 6px; color: #666; margin-bottom: 8px; }}
        .filter {{ text-align: center; font-size: 7px; color: #666; margin: 5px 0; }}
        table {{ 
            border-collapse: collapse; 
            width: 100%; 
            margin-top: 5px;
            font-size: 6px;
        }}
        th, td {{ 
            border: 1px solid #999; 
            padding: 3px 2px; 
            text-align: left;
            vertical-align: top;
            word-wrap: break-word;
        }}
        th {{ 
            background-color: #0d6efd; 
            color: white; 
            font-weight: bold;
            text-align: center;
        }}
        .badge-success {{ background-color: #28a745; color: white; padding: 1px 4px; border-radius: 3px; display: inline-block; }}
        .badge-danger {{ background-color: #dc3545; color: white; padding: 1px 4px; border-radius: 3px; display: inline-block; }}
        .badge-info {{ background-color: #17a2b8; color: white; padding: 1px 4px; border-radius: 3px; display: inline-block; }}
        .badge-warning {{ background-color: #ffc107; color: #333; padding: 1px 4px; border-radius: 3px; display: inline-block; }}
        .text-center {{ text-align: center; }}
        .footer {{ text-align: center; font-size: 5px; color: #666; margin-top: 8px; }}
    </style>
</head>
<body>
    <h2>📊 STOK HAREKETLERİ RAPORU</h2>
    <div class='date'>Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}</div>
    <div class='filter'>{(string.IsNullOrEmpty(filterText) ? "Tüm Hareketler" : filterText)}</div>
    
    <table>
        <thead>
            <tr>
                <th width='12%'>Tarih</th>
                <th width='10%'>Ürün Kodu</th>
                <th width='20%'>Ürün Adı</th>
                <th width='9%'>İşlem Tipi</th>
                <th width='6%'>Miktar</th>
                <th width='8%'>Önceki Stok</th>
                <th width='8%'>Yeni Stok</th>
                <th width='12%'>Referans No</th>
                <th width='15%'>Açıklama</th>
            </tr>
        </thead>
        <tbody>";

            foreach (var item in movements.OrderByDescending(m => m.CreatedAt).Take(500))
            {
                var badgeClass = item.MovementType switch
                {
                    "IN" => "badge-success",
                    "OUT" => "badge-danger",
                    "ADJUST_IN" => "badge-info",
                    "ADJUST_OUT" => "badge-warning",
                    _ => "badge-info"
                };
                var movementName = item.MovementType switch
                {
                    "IN" => "Giriş",
                    "OUT" => "Çıkış",
                    "ADJUST_IN" => "Düzeltme(+)",
                    "ADJUST_OUT" => "Düzeltme(-)",
                    _ => item.MovementType
                };

                html += $@"
            <tr>
                <td>{item.CreatedAt:dd.MM.yyyy HH:mm}</td>
                <td>{item.Product?.ProductCode ?? "-"}</td>
                <td>{item.Product?.ProductName ?? "-"}</td>
                <td><span class='{badgeClass}'>{movementName}</span></td>
                <td class='text-center'>{item.Quantity}</td>
                <td>{item.PreviousStock}</td>
                <td>{item.NewStock}</td>
                <td>{item.ReferenceNo ?? "-"}</td>
                <td>{item.Description ?? "-"}</td>
            </tr>";
            }

            html += @"
        </tbody>
     </table
    
    <div class='footer'>
        Teknik Servis Takip Sistemi | Toplam Kayıt: " + movements.Count() + @"
    </div>
</body>
</html>";

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                Landscape = true,
                MarginOptions = new MarginOptions { Top = "8mm", Bottom = "8mm", Left = "5mm", Right = "5mm" }
            });

            return File(pdfBytes, "application/pdf", $"StokHareketleri_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }


        // Export Product pdf
        //        [HttpGet]
        //        public async Task<IActionResult> ExportProductsPdf()
        //        {
        //            var products = await _unitOfWork.Products.GetAllWithIncludeAsync(p => p.Category);
        //            var activeProducts = products.Where(p => p.IsActive).OrderBy(p => p.ProductName).ToList();

        //            var html = @$"
        //<!DOCTYPE html>
        //<html>
        //<head>
        //    <meta charset='utf-8'>
        //    <title>Stok Listesi</title>
        //    <style>
        //        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        //        body {{ 
        //            font-family: 'DejaVu Sans', Arial, sans-serif; 
        //            margin: 5px; 
        //            padding: 5px;
        //            font-size: 7px;
        //        }}
        //        h2 {{ color: #0d6efd; text-align: center; margin-bottom: 8px; font-size: 14px; }}
        //        .date {{ text-align: right; font-size: 7px; color: #666; margin-bottom: 10px; }}
        //        table {{ 
        //            border-collapse: collapse; 
        //            width: 100%; 
        //            margin-top: 5px;
        //            font-size: 7px;
        //        }}
        //        th, td {{ 
        //            border: 1px solid #999; 
        //            padding: 4px 3px; 
        //            text-align: left;
        //            vertical-align: top;
        //            word-wrap: break-word;
        //        }}
        //        th {{ 
        //            background-color: #0d6efd; 
        //            color: white; 
        //            font-weight: bold;
        //            text-align: center;
        //        }}
        //        .danger {{ color: red; font-weight: bold; }}
        //        .text-center {{ text-align: center; }}
        //        .text-right {{ text-align: right; }}
        //        .footer {{ text-align: center; font-size: 6px; color: #666; margin-top: 10px; }}
        //    </style>
        //</head>
        //<body>
        //    <h2>📦 STOK LİSTESİ RAPORU</h2>
        //    <div class='date'>Oluşturma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}</div>

        //    <table>
        //        <thead>
        //            <tr>
        //                <th width='8%'>Ürün Kodu</th>
        //                <th width='14%'>Ürün Adı</th>
        //                <th width='7%'>Kategori</th>
        //                <th width='7%'>Marka</th>
        //                <th width='7%'>Model</th>
        //                <th width='10%'>Seri No</th>
        //                <th width='11%'>IMEI No</th>
        //                <th width='6%'>Miktar</th>
        //                <th width='8%'>Alış Fiyatı</th>
        //                <th width='8%'>Satış Fiyatı</th>
        //                <th width='14%'>Lokasyon</th>
        //            </td>
        //        </thead>
        //        <tbody>";

        //            foreach (var p in activeProducts)
        //            {
        //                var className = p.Quantity <= p.MinStockLevel ? "danger" : "";
        //                var purchasePrice = p.PurchasePrice?.ToString("N2") ?? "-";
        //                var salePrice = p.SalePrice?.ToString("N2") ?? "-";

        //                html += $@"
        //            <tr>
        //                <td>{p.ProductCode}</td>
        //                <td>{p.ProductName}</td>
        //                <td>{p.Category?.Name ?? "-"}</td>
        //                <td>{p.Brand ?? "-"}</td>
        //                <td>{p.Model ?? "-"}</td>
        //                <td>{p.SerialNo ?? "-"}</td>
        //                <td>{p.IMEINo ?? "-"}</td>
        //                <td class='text-center {className}'>{p.Quantity} {p.Unit}</td>
        //                <td class='text-right'>{purchasePrice} ₺</td>
        //                <td class='text-right'>{salePrice} ₺</td>
        //                <td>{p.Location ?? "-"}</td>
        //            </tr>";
        //            }

        //            html += @"
        //        </tbody>
        //     </table

        //    <div class='footer'>
        //        Teknik Servis Takip Sistemi | Toplam Kayıt: " + activeProducts.Count + @"
        //    </div>
        //</body>
        //</html>";

        //            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        //            using var page = await browser.NewPageAsync();
        //            await page.SetContentAsync(html);

        //            var pdfBytes = await page.PdfDataAsync(new PdfOptions
        //            {
        //                Format = PaperFormat.A4,
        //                Landscape = true,
        //                MarginOptions = new MarginOptions { Top = "8mm", Bottom = "8mm", Left = "5mm", Right = "5mm" }
        //            });

        //            return File(pdfBytes, "application/pdf", $"StokListesi_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        //        }



        // Export Product pdf
        [HttpGet]
        public async Task<IActionResult> ExportProductsPdf()
        {
            var products = await _unitOfWork.Products.GetAllWithIncludeAsync(p => p.Category);
            var activeProducts = products.Where(p => p.IsActive).OrderBy(p => p.ProductName).ToList();

            var html = @$"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Stok Listesi</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'DejaVu Sans', Arial, sans-serif; 
            margin: 5px; 
            padding: 5px;
            font-size: 7px;
        }}
        h2 {{ color: #0d6efd; text-align: center; margin-bottom: 8px; font-size: 14px; }}
        .date {{ text-align: right; font-size: 7px; color: #666; margin-bottom: 10px; }}
        table {{ 
            border-collapse: collapse; 
            width: 100%; 
            margin-top: 5px;
            font-size: 7px;
        }}
        th, td {{ 
            border: 1px solid #999; 
            padding: 4px 3px; 
            text-align: left;
            vertical-align: top;
            word-wrap: break-word;
        }}
        th {{ 
            background-color: #0d6efd; 
            color: white; 
            font-weight: bold;
            text-align: center;
        }}
        .danger {{ color: red; font-weight: bold; }}
        .text-center {{ text-align: center; }}
        .text-right {{ text-align: right; }}
        .footer {{ text-align: center; font-size: 6px; color: #666; margin-top: 10px; }}
    </style>
</head>
<body>
    <h2>📦 STOK LİSTESİ RAPORU</h2>
    <div class='date'>Oluşturma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}</div>
    
    <table>
        <thead>
            <tr>
                <th width='8%'>Ürün Kodu</th>
                <th width='14%'>Ürün Adı</th>
                <th width='7%'>Kategori</th>
                <th width='7%'>Marka</th>
                <th width='7%'>Model</th>
                <th width='10%'>Seri No</th>
                <th width='11%'>IMEI No</th>
                <th width='6%'>Miktar</th>
                <th width='9%'>Alış Fiyatı</th>
                <th width='9%'>Satış Fiyatı</th>
                <th width='12%'>Lokasyon</th>
            </tr> </thead>
        <tbody>";

            foreach (var p in activeProducts)
            {
                var className = p.Quantity <= p.MinStockLevel ? "danger" : "";

                // Kanka eğer DB'de para birimi boş kalmışsa varsayılan olarak "TRY" yazsın dedik
                var currencySign = p.Currency ?? "TRY";

                var purchasePrice = p.PurchasePrice?.ToString("N2") ?? "-";
                var salePrice = p.SalePrice?.ToString("N2") ?? "-";

                // Fiyatların sonuna sabit ₺ yerine dinamik gelen currencySign (TRY, USD, EUR vb.) alanını ekledik
                html += $@"
            <tr>
                <td>{p.ProductCode}</td>
                <td>{p.ProductName}</td>
                <td>{p.Category?.Name ?? "-"}</td>
                <td>{p.Brand ?? "-"}</td>
                <td>{p.Model ?? "-"}</td>
                <td>{p.SerialNo ?? "-"}</td>
                <td>{p.IMEINo ?? "-"}</td>
                <td class='text-center {className}'>{p.Quantity} {p.Unit}</td>
                <td class='text-right'>{purchasePrice} {currencySign}</td>
                <td class='text-right'>{salePrice} {currencySign}</td>
                <td>{p.Location ?? "-"}</td>
            </tr>";
            }

            // Kanka buradaki yarım kalan </table etiketini de </table> olarak kapattım
            html += @"
        </tbody>
    </table>
    <div class='footer'>
        Teknik Servis Takip Sistemi | Toplam Kayıt: " + activeProducts.Count + @"
    </div>
</body>
</html>";

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                Landscape = true,
                MarginOptions = new MarginOptions { Top = "8mm", Bottom = "8mm", Left = "5mm", Right = "5mm" }
            });

            return File(pdfBytes, "application/pdf", $"StokListesi_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }


        [HttpGet]
        public async Task<IActionResult> GetProductsForSelect()
        {
            var products = await _productService.GetAllAsync();
            var activeProducts = products.Where(p => p.IsActive && p.Quantity > 0)
                .Select(p => new {
                    p.Id,
                    p.ProductName,
                    p.ProductCode,
                    p.SerialNo,
                    p.Brand,
                    p.Model,
                    p.Quantity
                })
                .OrderBy(p => p.ProductName);

            return Json(activeProducts);
        }
    }
}
