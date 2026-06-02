using DataAccess.Context;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Entities.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Sevkiyat")]
    public class DeliveryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogService _logService;
        private readonly AppDbContext _context;

        public DeliveryController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, ILogService logService, AppDbContext context)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _logService = logService;
            _context = context;
        }

        // Teslimat Sayfası
        public IActionResult Index()
        {
            return View();
        }

        // Müşteri No ile ara (Tüm ürünler)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SearchByCustomerNumber(string customerNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(customerNumber))
                {
                    return Json(new { success = false, message = "Müşteri No giriniz!" });
                }

                var customer = await _userManager.Users.FirstOrDefaultAsync(u => u.CustomerNumber == customerNumber && u.IsActive == true);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Müşteri bulunamadı!" });
                }

                // SADECE teslim edilmemiş ürünleri getir (StatusId != 9)
                var repairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.AppUserId == customer.Id && r.StatusId != (int)RepairStatusEnum.TeslimEdildi);

                var result = new
                {
                    success = true,
                    customer = new { customer.FullName, customer.CustomerNumber, customer.PhoneNumber, customer.CompanyName },
                    repairs = repairs.Select(r => new
                    {
                        r.Id,
                        r.TrackingCode,
                        r.ProductName,
                        r.ProductBrand,
                        r.ProductModel,
                        r.StatusId,
                        StatusName = ((RepairStatusEnum)(r.StatusId ?? 1)).GetDisplayName(),
                        r.ReceivedDate,
                        r.Price
                    })
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        //Takip Kodu ile ara
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SearchByTrackingCode(string trackingCode)
        {
            try
            {
                if (string.IsNullOrEmpty(trackingCode))
                {
                    return Json(new { success = false, message = "Takip Kodu giriniz!" });
                }

                var repair = (await _unitOfWork.RepairItems.GetWhereAsync(r => r.TrackingCode == trackingCode && r.StatusId != (int)RepairStatusEnum.TeslimEdildi)).FirstOrDefault();

                if (repair == null)
                {
                    return Json(new { success = false, message = "Teslim edilmemiş ürün bulunamadı!" });
                }

                var customer = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == repair.AppUserId);

                var result = new
                {
                    success = true,
                    customer = customer != null ? new { customer.FullName, customer.CustomerNumber, customer.PhoneNumber, customer.CompanyName } : null,
                    repairs = new[]
                    {
                new
                {
                    repair.Id,
                    repair.TrackingCode,
                    repair.ProductName,
                    repair.ProductBrand,
                    repair.ProductModel,
                    repair.StatusId,
                    StatusName = ((RepairStatusEnum)(repair.StatusId ?? 1)).GetDisplayName(),
                    repair.ReceivedDate,
                    repair.Price
                }
            }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Delivery Detail (Teslimat sayfasından açılan detay)
        public async Task<IActionResult> DeliveryRepairDetail(int id)
        {
            var repair = await _unitOfWork.RepairItems
                .GetByIdWithIncludeAsync(id,
                    r => r.Personel,
                    r => r.RepairImages,
                    r => r.AppUser);

            if (repair == null)
            {
                TempData["Error"] = "Kayıt bulunamadı!";
                return RedirectToAction("Index");
            }

            return View(repair);
        }


        // Teslim et
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deliver(int repairId, string deliveryType, string cargoCompany,
            string cargoTrackingNumber, string recipientName, string recipientPhone,
            string receiverName, string receiverPhone, string note)
        {
            var repair = await _unitOfWork.RepairItems.GetByIdWithIncludeAsync(repairId, r => r.AppUser, r => r.Personel);
            if (repair == null)
            {
                return Json(new { success = false, message = "Tamir kaydı bulunamadı!" });
            }

            if (repair.StatusId == (int)RepairStatusEnum.TeslimEdildi)
            {
                return Json(new { success = false, message = "Bu ürün zaten teslim edilmiş!" });
            }

            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$");

            if (deliveryType == "Cargo")
            {
                if (string.IsNullOrEmpty(cargoCompany))
                    return Json(new { success = false, message = "Kargo firması zorunludur!" });
                if (string.IsNullOrEmpty(recipientName))
                    return Json(new { success = false, message = "Alıcı adı zorunludur!" });

                // Telefon kontrolü
                if (!string.IsNullOrEmpty(recipientPhone) && !recipientPhone.IsValidTurkishPhone())
                    return Json(new { success = false, message = "Geçerli bir alıcı telefon numarası giriniz! (Örn: 05XX XXX XX XX)" });

                if (!string.IsNullOrEmpty(recipientPhone))
                    recipientPhone = recipientPhone.NormalizePhone();
            }
            else
            {
                if (string.IsNullOrEmpty(receiverName))
                    return Json(new { success = false, message = "Teslim alan kişi adı zorunludur!" });

                // Telefon kontrolü
                if (!string.IsNullOrEmpty(receiverPhone) && !receiverPhone.IsValidTurkishPhone())
                    return Json(new { success = false, message = "Geçerli bir teslim alan telefon numarası giriniz! (Örn: 05XX XXX XX XX)" });

                if (!string.IsNullOrEmpty(receiverPhone))
                    receiverPhone = receiverPhone.NormalizePhone();
            }

            var user = await _userManager.GetUserAsync(User);
            var deliveredBy = user?.FullName ?? "Sistem";

            var delivery = new Delivery
            {
                RepairItemId = repairId,
                CustomerId = repair.AppUserId,
                DeliveryType = deliveryType,
                DeliveryDate = DateTime.Now,
                DeliveredBy = deliveredBy,
                Note = note
            };

            if (deliveryType == "Cargo")
            {
                delivery.CargoCompany = cargoCompany;
                delivery.CargoTrackingNumber = cargoTrackingNumber;
                delivery.RecipientName = recipientName;
                delivery.RecipientPhone = recipientPhone;
            }
            else
            {
                delivery.ReceiverName = receiverName;
                delivery.ReceiverPhone = receiverPhone;
            }

            // ========== ESKİ DURUMU KAYDET ==========
            var oldStatusId = repair.StatusId;
            var teslimEdildiId = (int)RepairStatusEnum.TeslimEdildi; // 9 Teslim edildi

            repair.StatusId = (int)RepairStatusEnum.TeslimEdildi;
            repair.DeliveryDate = DateTime.Now;

            _unitOfWork.RepairItems.Update(repair);
            await _unitOfWork.Deliveries.AddAsync(delivery);


            // ========== ARŞİVLEME (TESLİM EDİLDİ İSE) ==========
            if (oldStatusId != teslimEdildiId)
            {
                var alreadyArchived = await _unitOfWork.ArchiveRepairs
                    .GetWhereAsync(a => a.OriginalRepairId == repair.Id);

                if (!alreadyArchived.Any())
                {
                    var archive = new ArchiveRepair
                    {
                        CustomerNumber = repair.CustomerNumber,
                        CustomerName = repair.AppUser?.FullName,
                        CompanyName = repair.AppUser?.CompanyName,
                        AppUserId = repair.AppUserId,
                        TrackingCode = repair.TrackingCode,
                        ProductName = repair.ProductName,
                        ProductBrand = repair.ProductBrand,
                        ProductModel = repair.ProductModel,
                        SerialNumber = repair.SerialNumber,
                        ProblemDescription = repair.ProblemDescription,
                        InternalNote = repair.InternalNote,
                        ReceivedDate = repair.ReceivedDate,
                        DeliveryDate = DateTime.Now,
                        PersonelId = repair.PersonelId,
                        Price = repair.Price,
                        Currency = repair.Currency,
                        OriginalRepairId = repair.Id,
                        ArchivedAt = DateTime.Now
                    };

                    await _unitOfWork.ArchiveRepairs.AddAsync(archive);
                    System.Diagnostics.Debug.WriteLine($"Arşive eklendi (Delivery): {repair.TrackingCode}");
                }
            }

            // ========== İŞLEM LOGU ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Ürün Teslimat",
                actionType: "Update",
                entityName: "RepairItem",
                entityId: repairId,
                description: $"Ürün teslim edildi. Takip Kodu: {repair.TrackingCode}, Teslim Tipi: {deliveryType}, Teslim Eden: {deliveredBy}",
                oldValues: new { StatusId = oldStatusId },
                newValues: new { StatusId = (int)RepairStatusEnum.TeslimEdildi, DeliveryDate = DateTime.Now }
            );
            await _unitOfWork.CompleteAsync();

            return Json(new { success = true, message = "Ürün başarıyla teslim edildi!" });
        }

        // Teslim Edilen Ürünler Listesi
        public async Task<IActionResult> DeliveryList()
        {
            var deliveries = await _unitOfWork.Deliveries.GetAllAsync(d => d.RepairItem, d => d.Customer);
            return View(deliveries.OrderByDescending(d => d.DeliveryDate));
        }

        public async Task<IActionResult> DeliveryDetail(int id)
        {
            var delivery = await _context.Deliveries
                .Include(d => d.RepairItem)
                .Include(d => d.Customer)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (delivery == null)
            {
                return NotFound();
            }

            return View(delivery);
        }


        // Teslimat kaydını sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDelivery(int id)
        {
            try
            {
                var delivery = await _unitOfWork.Deliveries.GetByIdAsync(id);
                if (delivery == null)
                {
                    return Json(new { success = false, message = "Kayıt bulunamadı!" });
                }

                // Teslim edilen ürünün durumunu geri al (StatusId'yi 4'ten eski haline döndür)
                var repair = await _unitOfWork.RepairItems.GetByIdAsync(delivery.RepairItemId);
                if (repair != null && repair.StatusId == (int)RepairStatusEnum.TeslimEdildi)
                {
                    // Geri alınacak durum? Varsayılan olarak "Tamamlandı"  yapıyoruz
                    repair.StatusId = (int)RepairStatusEnum.Tamamlandi;
                    repair.DeliveryDate = null;
                    _unitOfWork.RepairItems.Update(repair);
                }

                // Arşiv kaydını da sil (varsa)
                var archive = (await _unitOfWork.ArchiveRepairs
                    .GetWhereAsync(a => a.OriginalRepairId == delivery.RepairItemId))
                    .FirstOrDefault();

                if (archive != null)
                {
                    _unitOfWork.ArchiveRepairs.Delete(archive);
                }

                // Teslimat kaydını sil
                _unitOfWork.Deliveries.Delete(delivery);
                await _unitOfWork.CompleteAsync();

                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Teslimat Kaydı Silme",
                    actionType: "Delete",
                    entityName: "Delivery",
                    entityId: id,
                    description: $"Teslimat kaydı silindi. ID: {id}",
                    oldValues: null,
                    newValues: null
                );

                return Json(new { success = true, message = "Teslimat kaydı başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
        }

        public async Task<IActionResult> Print(int id)
        {
            var delivery = await _unitOfWork.Deliveries
                .GetByIdWithIncludeAsync(id, d => d.RepairItem, d => d.Customer);

            if (delivery == null)
                return NotFound();

            return View(delivery);
        }

        public async Task<IActionResult> PrintAll()
        {
            // Tüm delivery'leri al
            var deliveries = await _unitOfWork.Deliveries.GetAllAsync();

            // Her bir delivery için RepairItem ve Customer bilgilerini manuel yükle
            var deliveriesWithDetails = new List<Delivery>();

            foreach (var delivery in deliveries)
            {
                // RepairItem'i yükle (int olduğu için > 0 kontrolü yeterli)
                if (delivery.RepairItemId > 0)
                {
                    delivery.RepairItem = await _unitOfWork.RepairItems.GetByIdAsync(delivery.RepairItemId);
                }

                // Customer'ı yükle (CustomerId ile)
                if (!string.IsNullOrEmpty(delivery.CustomerId))
                {
                    delivery.Customer = await _userManager.FindByIdAsync(delivery.CustomerId);
                }

                deliveriesWithDetails.Add(delivery);
            }

            // Sıralama
            var orderedDeliveries = deliveriesWithDetails
                .OrderByDescending(d => d.DeliveryDate)
                .ToList();

            return View(orderedDeliveries);
        }
    }
}