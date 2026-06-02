using Business.Abstract;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknikServisTakip.Business.Abstract;

namespace Business.Concrete
{
    public class ProductManager: IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMailService? _mailService;
        private readonly UserManager<AppUser> _userManager;

        public ProductManager(IUnitOfWork unitOfWork, IMailService mailService = null, UserManager<AppUser> userManager = null)
        {
            _unitOfWork = unitOfWork;
            _mailService = mailService;
            _userManager = userManager;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _unitOfWork.Products.GetByIdAsync(id);
        }

        public async Task<Product?> GetByIdWithMovementsAsync(int id)
        {
            return await _unitOfWork.ProductRepository.GetProductWithMovementsAsync(id);
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _unitOfWork.Products.GetAllAsync();
        }

        public async Task<(ICollection<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchTerm = null)
        {
            return await _unitOfWork.ProductRepository.GetPagedAsync(page, pageSize, searchTerm);
        }

        public async Task<Product> AddAsync(Product product, string userId)
        {
            // 🔥 Otomatik kod oluşturma KALDIRILDI - artık manuel girilecek

            // Ürün kodu kontrolü (zorunlu ve benzersiz)
            if (string.IsNullOrEmpty(product.ProductCode))
                throw new Exception("Ürün kodu zorunludur!");

            // Aynı kod var mı kontrol et
            var existingProduct = await _unitOfWork.Products.GetSingleAsync(p => p.ProductCode == product.ProductCode);
            if (existingProduct != null)
                throw new Exception($"'{product.ProductCode}' ürün kodu zaten kullanımda!");

            product.CreatedBy = userId;
            product.CreatedAt = DateTime.Now;
            product.IsActive = true;

            // SerialNo ve IMEI kontrolü
            if (!string.IsNullOrEmpty(product.SerialNo))
            {
                if (await _unitOfWork.ProductRepository.IsSerialNoExistsAsync(product.SerialNo))
                    throw new Exception($"'{product.SerialNo}' seri numarası zaten kullanımda!");
            }

            if (!string.IsNullOrEmpty(product.IMEINo))
            {
                if (await _unitOfWork.ProductRepository.IsIMEINoExistsAsync(product.IMEINo))
                    throw new Exception($"'{product.IMEINo}' IMEI numarası zaten kullanımda!");
            }

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.CompleteAsync();

            // Stok hareketi kaydet (ilk giriş)
            if (product.Quantity > 0)
            {
                await AddStockMovementAsync(product.Id, "IN", product.Quantity, 0, product.Quantity,
                    "Ürün oluşturuldu", userId);
            }

            await CheckAndCreateStockAlertAsync(product);

            return product;
        }

        public async Task<Product> UpdateAsync(Product product, string userId)
        {
            var existing = await _unitOfWork.Products.GetByIdAsync(product.Id);
            if (existing == null)
                throw new Exception("Ürün bulunamadı!");

            // 🔥 SerialNo ve IMEI kontrolü (existing ile karşılaştır)
            if (product.SerialNo != existing.SerialNo && !string.IsNullOrEmpty(product.SerialNo))
            {
                if (await _unitOfWork.ProductRepository.IsSerialNoExistsAsync(product.SerialNo, product.Id))
                    throw new Exception($"'{product.SerialNo}' seri numarası zaten kullanımda!");
            }

            if (product.IMEINo != existing.IMEINo && !string.IsNullOrEmpty(product.IMEINo))
            {
                if (await _unitOfWork.ProductRepository.IsIMEINoExistsAsync(product.IMEINo, product.Id))
                    throw new Exception($"'{product.IMEINo}' IMEI numarası zaten kullanımda!");
            }

            // Stok değişti mi kontrol et
            var stockChanged = existing.Quantity != product.Quantity;
            var oldQuantity = existing.Quantity;

      
            existing.ProductName = product.ProductName;
            existing.CategoryId = product.CategoryId;
            existing.Brand = product.Brand;
            existing.Model = product.Model;
            existing.SerialNo = product.SerialNo;
            existing.IMEINo = product.IMEINo;
            existing.Quantity = product.Quantity;
            existing.MinStockLevel = product.MinStockLevel;
            existing.MaxStockLevel = product.MaxStockLevel;
            existing.Unit = product.Unit;
            existing.Location = product.Location;
            existing.Supplier = product.Supplier;
            existing.PurchasePrice = product.PurchasePrice;
            existing.SalePrice = product.SalePrice;
            existing.Description = product.Description;
            existing.Notes = product.Notes;
            existing.IsActive = product.IsActive;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.Now;

           

            _unitOfWork.Products.Update(existing);
            await _unitOfWork.CompleteAsync();

            // Stok değiştiyse hareket kaydet
            if (stockChanged)
            {
                var diff = product.Quantity - oldQuantity;
                var movementType = diff > 0 ? "ADJUST_IN" : "ADJUST_OUT";
                var absDiff = Math.Abs(diff);

                await AddStockMovementAsync(product.Id, movementType, absDiff, oldQuantity, product.Quantity,
                    $"Stok düzeltmesi (El ile)", userId);
            }

            await CheckAndCreateStockAlertAsync(existing);

            return existing;
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return false;

            _unitOfWork.Products.Delete(product);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> SoftDeleteAsync(int id, string userId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(id);
            if (product == null)
                return false;

            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;
            product.UpdatedBy = userId;

            _unitOfWork.Products.Update(product);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<(bool Success, string Message, int NewStock)> StockInAsync(int productId, int quantity,
            string referenceNo, string description, string userId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return (false, "Ürün bulunamadı!", 0);

            var oldStock = product.Quantity;
            product.Quantity += quantity;
            product.UpdatedAt = DateTime.Now;
            product.UpdatedBy = userId;

            _unitOfWork.Products.Update(product);
            await AddStockMovementAsync(productId, "IN", quantity, oldStock, product.Quantity, description, userId, referenceNo);
            await _unitOfWork.CompleteAsync();

            await CheckAndCreateStockAlertAsync(product);

            return (true, $"{quantity} adet stok eklendi. Yeni stok: {product.Quantity}", product.Quantity);
        }

        public async Task<(bool Success, string Message, int NewStock)> StockOutAsync(int productId, int quantity,
            string referenceNo, string description, string userId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return (false, "Ürün bulunamadı!", 0);

            if (product.Quantity < quantity)
                return (false, $"Yetersiz stok! Mevcut: {product.Quantity}, İstenen: {quantity}", product.Quantity);

            var oldStock = product.Quantity;
            product.Quantity -= quantity;
            product.UpdatedAt = DateTime.Now;
            product.UpdatedBy = userId;

            _unitOfWork.Products.Update(product);
            await AddStockMovementAsync(productId, "OUT", quantity, oldStock, product.Quantity, description, userId, referenceNo);
            await _unitOfWork.CompleteAsync();

            await CheckAndCreateStockAlertAsync(product);

            return (true, $"{quantity} adet stok çıkışı yapıldı. Kalan stok: {product.Quantity}", product.Quantity);
        }

        public async Task<bool> IsSerialNoExistsAsync(string serialNo, int? excludeId = null)
        {
            return await _unitOfWork.ProductRepository.IsSerialNoExistsAsync(serialNo, excludeId);
        }

        public async Task<bool> IsIMEINoExistsAsync(string imeiNo, int? excludeId = null)
        {
            return await _unitOfWork.ProductRepository.IsIMEINoExistsAsync(imeiNo, excludeId);
        }

        public async Task<List<Product>> GetLowStockProductsAsync()
        {
            return await _unitOfWork.ProductRepository.GetLowStockProductsAsync();
        }

        public async Task<List<Product>> GetCriticalStockProductsAsync()
        {
            return await _unitOfWork.ProductRepository.GetCriticalStockProductsAsync();
        }

        private async Task<List<string>> GetAdminEmailsAsync()
        {
            var adminEmails = new List<string>();
            var adminRoles = new[] { "SuperAdmin", "Admin", "Idari", "Depo", "Sevkiyat" };

            foreach (var role in adminRoles)
            {
                if (_userManager != null)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                    adminEmails.AddRange(usersInRole.Where(u => u.Email != null).Select(u => u.Email));
                }
            }

            return adminEmails.Distinct().ToList();
        }

        public async Task SendStockAlertsAsync()
        {
            if (_mailService == null) return;

            var lowStockProducts = await GetLowStockProductsAsync();
            var criticalStockProducts = await GetCriticalStockProductsAsync();

            if (lowStockProducts.Any() || criticalStockProducts.Any())
            {
               
                var adminEmails = await GetAdminEmailsAsync();

                if (adminEmails.Any())
                {
                    var subject = "⚠️ STOK UYARISI - Kritik Seviye";
                    var body = BuildStockAlertEmailBody(lowStockProducts, criticalStockProducts);

                    foreach (var email in adminEmails)
                    {
                        await _mailService.SendMailAsync(email, subject, body, true);
                    }

                    // Uyarı gönderildi olarak işaretle
                    foreach (var product in lowStockProducts.Concat(criticalStockProducts))
                    {
                        var existingAlert = await _unitOfWork.StockAlerts
                            .GetSingleAsync(a => a.ProductId == product.Id && !a.IsSent);
                        if (existingAlert != null)
                        {
                            existingAlert.IsSent = true;
                            existingAlert.SentAt = DateTime.Now;
                            _unitOfWork.StockAlerts.Update(existingAlert);
                        }
                    }
                    await _unitOfWork.CompleteAsync();
                }
            }
        }

        public async Task<(int SuccessCount, List<string> Errors)> BulkImportAsync(List<Product> products, string userId)
        {
            var errors = new List<string>();
            var successCount = 0;

            foreach (var product in products)
            {
                try
                {
                    //  Excel'den gelen ProductCode'u kullan
                    if (string.IsNullOrEmpty(product.ProductCode))
                    {
                        errors.Add($"'{product.ProductName}' eklenirken hata: Ürün kodu zorunludur!");
                        continue;
                    }

                    //  Aynı kod var mı kontrol et (sadece aktif ürünleri değil, tümünü)
                    var existingProduct = await _unitOfWork.Products.GetSingleAsync(p => p.ProductCode == product.ProductCode);
                    if (existingProduct != null)
                    {
                        errors.Add($"'{product.ProductName}' eklenirken hata: '{product.ProductCode}' ürün kodu zaten kullanımda!");
                        continue;
                    }

                    product.CreatedBy = userId;
                    product.CreatedAt = DateTime.Now;
                    product.IsActive = true;

                    await _unitOfWork.Products.AddAsync(product);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"'{product.ProductName}' eklenirken hata: {ex.Message}");
                }
            }

            await _unitOfWork.CompleteAsync();
            return (successCount, errors);
        }

        public string GetStockStatusClass(int quantity, int minStockLevel, int maxStockLevel)
        {
            if (quantity == 0) return "table-danger";        // Kırmızı - Kritik
            if (quantity <= minStockLevel) return "table-warning";  // Sarı - Düşük stok
            if (quantity >= maxStockLevel) return "table-info";     // Mavi - Fazla stok
            return "";                                        // Normal - Yeşil
        }

        // Private helper metodlar
        private async Task AddStockMovementAsync(int productId, string movementType, int quantity,
            int previousStock, int newStock, string description, string userId, string referenceNo = null)
        {
            var movement = new StockMovement
            {
                ProductId = productId,
                MovementType = movementType,
                Quantity = quantity,
                PreviousStock = previousStock,
                NewStock = newStock,
                ReferenceNo = referenceNo,
                Description = description,
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            };

            await _unitOfWork.StockMovements.AddAsync(movement);
        }

        private async Task CheckAndCreateStockAlertAsync(Product product)
        {
            string? alertType = null;
            string? notes = null;

            if (product.Quantity <= 0)
            {
                alertType = "CRITICAL";
                notes = $"🔴 KRİTİK: Stok tükendi! (Stok: {product.Quantity})";
            }
            else if (product.Quantity <= product.MinStockLevel)
            {
                alertType = "LOW_STOCK";
                notes = $"🟡 DÜŞÜK STOK: Minimum seviyeye düştü! (Stok: {product.Quantity} / Min: {product.MinStockLevel})";
            }
            else if (product.Quantity >= product.MaxStockLevel)
            {
                alertType = "HIGH_STOCK";
                notes = $"🔵 YÜKSEK STOK: Maksimum seviyeyi aştı! (Stok: {product.Quantity} / Max: {product.MaxStockLevel})";
            }

            if (alertType != null)
            {
                var existingAlert = await _unitOfWork.StockAlerts
                    .GetSingleAsync(a => a.ProductId == product.Id && !a.IsSent);

                if (existingAlert == null)
                {
                    var alert = new StockAlert
                    {
                        ProductId = product.Id,
                        AlertType = alertType,
                        NewQuantity = product.Quantity,
                        Notes = notes,
                        CreatedAt = DateTime.Now,
                        IsSent = false
                    };

                    await _unitOfWork.StockAlerts.AddAsync(alert);
                    await _unitOfWork.CompleteAsync();
                }
            }
        }

        private string BuildStockAlertEmailBody(List<Product> lowStockProducts, List<Product> criticalStockProducts)
        {
            var html = new StringBuilder();
            html.Append("<html><head><meta charset='utf-8'><style>");
            html.Append("table{border-collapse:collapse;width:100%}");
            html.Append("th,td{border:1px solid #ddd;padding:8px;text-align:left}");
            html.Append("th{background-color:#4CAF50;color:white}");
            html.Append(".critical{background-color:#ffcccc}");
            html.Append(".low{background-color:#ffffcc}");
            html.Append("</style></head><body>");
            html.Append("<h2>⚠️ Stok Uyarı Raporu</h2>");

            if (criticalStockProducts.Any())
            {
                html.Append("<h3 style='color:red;'>🔴 Kritik Stok (Tükenen Ürünler)</h3>");
                html.Append("<table><tr><th>Ürün Adı</th><th>Ürün Kodu</th><th>Stok</th><th>Lokasyon</th></tr>");
                foreach (var product in criticalStockProducts)
                {
                    html.Append($"<tr class='critical'><td>{product.ProductName}</td><td>{product.ProductCode}</td><td>{product.Quantity}</td><td>{product.Location ?? "-"}</td></tr>");
                }
                html.Append("</table>");
            }

            if (lowStockProducts.Any())
            {
                html.Append("<h3 style='color:orange;'>🟡 Düşük Stok (Limit Altı)</h3>");
                html.Append("<table><tr><th>Ürün Adı</th><th>Ürün Kodu</th><th>Stok</th><th>Min Stok</th><th>Lokasyon</th></tr>");
                foreach (var product in lowStockProducts)
                {
                    html.Append($"<tr class='low'><td>{product.ProductName}</td><td>{product.ProductCode}</td><td>{product.Quantity}</td><td>{product.MinStockLevel}</td><td>{product.Location ?? "-"}</td></tr>");
                }
                html.Append("</table>");
            }

            html.Append("<hr/><small>Bu mail Teknik Servis Takip Sistemi tarafından otomatik gönderilmiştir.</small>");
            html.Append("</body></html>");

            return html.ToString();
        }

      
    }
}
