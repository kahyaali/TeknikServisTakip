using Business.Abstract;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Entities.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Hubs;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "Personel")]
    public class PersonelDashboardController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogService _logService;
        private readonly IProductService _productService;

        public PersonelDashboardController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, ILogService logService, IProductService productService)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _logService = logService;
            _productService = productService;
        }

        // Personel Dashboard - Kendi tamirleri
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var personel = (await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == user.Id)).FirstOrDefault();

            if (personel == null)
            {
                return RedirectToAction("Index", "Home");
            }

            int urunKaydedildiId = (int)RepairStatusEnum.UrunKaydedildi;
            int teklifOnaylandiId = (int)RepairStatusEnum.TeklifOnaylandi;
            int islemeAlindiId = (int)RepairStatusEnum.IslemeAlindi;      
            int tamamlandiId = (int)RepairStatusEnum.Tamamlandi;
            int expertizeGonderildiId = (int)RepairStatusEnum.ExpertizeGonderildi;
            int expertizBekleniyorId = (int)RepairStatusEnum.ExpertizBekleniyor;
            int teklifHazirlaniyorId = (int)RepairStatusEnum.TeklifHazirlaniyor;
            int teklifGonderildiId = (int)RepairStatusEnum.TeklifGonderildi;

            // Bana Atanmış olan tamirler 
            var myRepairs = await _unitOfWork.RepairItems.GetWhereAsync(r => r.PersonelId == personel.Id);
            myRepairs = myRepairs.Where(r => r.StatusId != (int)RepairStatusEnum.TeslimEdildi).ToList();

            // Bana Atanmamış olan tamirler (PersonelId = null ve StatusId = 1)
            var unassignedRepairs = await _unitOfWork.RepairItems.GetWhereAsync(r => (r.PersonelId == null || r.PersonelId == 0) && r.StatusId == urunKaydedildiId);

            // Bana atanmış olanları durumlarına göre ayır
            var pendingList = myRepairs.Where(r => r.StatusId == urunKaydedildiId ||
            r.StatusId == teklifOnaylandiId ||
            r.StatusId == expertizeGonderildiId ||
            r.StatusId==expertizBekleniyorId ||
            r.StatusId==teklifHazirlaniyorId ||
            r.StatusId==teklifGonderildiId).ToList();
            var inProgressList = myRepairs.Where(r => r.StatusId == islemeAlindiId).ToList();
            var completedList = myRepairs.Where(r => r.StatusId == tamamlandiId).ToList();

            ViewBag.PendingRepairs = pendingList;        // Bana atanmış ama henüz işleme alınmamış (ürün kaydedildi)
            ViewBag.InProgressRepairs = inProgressList;  // Bana atanmış ve işlemde (işleme alındı)
            ViewBag.CompletedRepairs = completedList;    // Bana atanmış ve tamamlanmış (tamamlandı)
            ViewBag.UnassignedRepairs = unassignedRepairs; // Henüz kimseye atanmamış tamirler

            ViewBag.PendingCount = pendingList.Count;
            ViewBag.InProgressCount = inProgressList.Count;
            ViewBag.CompletedCount = completedList.Count;
            ViewBag.UnassignedCount = unassignedRepairs.Count();
            ViewBag.TotalCount = myRepairs.Count();
            ViewBag.PersonelName = personel.FullName;

            return View();
        }

        // Tamir Detay ve Not Ekleme
        public async Task<IActionResult> RepairDetail(int id)
        {
            var repair = await _unitOfWork.RepairItems.GetByIdAsync(id);
            if (repair == null) return NotFound();

            // Yetki kontrolü: Bu tamir bu personele ait mi?
            var user = await _userManager.GetUserAsync(User);
            var personel = (await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == user.Id)).FirstOrDefault();

            if (repair.PersonelId != personel?.Id)
            {
                TempData["Error"] = "Bu tamir size ait değil!";
                return RedirectToAction("Index");
            }

            // İlişkili verileri getir
            repair.Personel = personel;
            repair.AppUser = await _unitOfWork.Users.GetByIdAsync(repair.AppUserId);

            //====== Durum Listesi ========
                        ViewBag.StatusList = Enum.GetValues(typeof(RepairStatusEnum))
                         .Cast<RepairStatusEnum>()
                         .Select(s => new SelectListItem
                         {
                             Value = ((int)s).ToString(),
                             Text = s.GetDisplayName(),
                             Selected = (int)s == (repair.StatusId ?? 1)
                         }).ToList();

            return View(repair);
        }



        private string GetStatusName(int statusId)
        {
            if (Enum.IsDefined(typeof(RepairStatusEnum), statusId))
            {
                var statusEnum = (RepairStatusEnum)statusId;
                return statusEnum.GetDisplayName();
            }
            return "Güncellendi";
        }

        // Personel Notu Ekle/Güncelle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNote(int id, string internalNote, int statusId)
        {
            var repair = await _unitOfWork.GetByIdWithIncludeAsync(id, r => r.AppUser);
            if (repair == null) return NotFound();

            // Yetki kontrolü
            var user = await _userManager.GetUserAsync(User);
            var personel = (await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == user.Id)).FirstOrDefault();

            if (repair.PersonelId != personel?.Id)
            {
                TempData["Error"] = "Bu tamir size ait değil!";
                return RedirectToAction("Index");
            }

            var oldStatusId = repair.StatusId;
            var oldNote = repair.InternalNote;
            var trackingCode = repair.TrackingCode;


            repair.InternalNote = internalNote;
            repair.StatusId = statusId;
            _unitOfWork.RepairItems.Update(repair);
            await _unitOfWork.CompleteAsync();

            // ========== SIGNALR İLE MÜŞTERİYE BİLDİRİM GÖNDER ==========
            try
            {
                var hubContext = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub>)) as Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub>;
                if (hubContext != null && repair.AppUserId != null)
                {
                    string message = "";
                    string type = "info";

                    // Durum değiştiyse
                    if (oldStatusId != statusId)
                    {
                        string statusName = GetStatusName(statusId);
                        message = $"Tamir kaydınız ({repair.TrackingCode}) durumu güncellendi: {statusName}";
                        type = "success";
                    }
                    // Sadece not değiştiyse
                    else if (oldNote != internalNote)
                    {
                        message = $"Tamir kaydınıza ({repair.TrackingCode}) yeni bir not eklendi.";
                        type = "info";
                    }

                    // Mesaj varsa gönder
                    if (!string.IsNullOrEmpty(message))
                    {
                        await hubContext.Clients.User(repair.AppUserId).SendAsync("ReceiveMessage", message, type);
                        Console.WriteLine($"SignalR mesajı gönderildi: {repair.AppUserId} - {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR mesaj gönderilemedi: {ex.Message}");
            }



            // ========== İŞLEM LOGU ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Not Güncelleme",
                actionType: "Update",
                entityName: "RepairItem",
                entityId: id,
                description: $"Personel notu ve durum güncellendi. Takip Kodu: {repair.TrackingCode}",
                oldValues: new { StatusId = oldStatusId, InternalNote = "Eski not" },
                newValues: new { StatusId = statusId, InternalNote = internalNote }
            );


            TempData["Success"] = "Personel notu ve durum güncellendi!";
            return RedirectToAction("Index");
        }


        // Tamiri Üzerine Alma (ATANMAMIŞ TAMİRLER İÇİN)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeOver(int id)
        {
            try
            {
                var repair = await _unitOfWork.RepairItems.GetByIdWithIncludeAsync(id, r => r.AppUser);
                if (repair == null)
                {
                    return Json(new { success = false, message = "Tamir kaydı bulunamadı!" });
                }
                //  Teklif Onaylandı ise işleme alınabilir
                if (repair.StatusId != (int)RepairStatusEnum.TeklifOnaylandi)
                {
                    return Json(new { success = false, message = "Bu tamir henüz teklif onaylanmadığı için işleme alınamaz!" });
                }

                var user = await _userManager.GetUserAsync(User);
                var personel = (await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == user.Id)).FirstOrDefault();

                if (personel == null)
                {
                    return Json(new { success = false, message = "Personel bilgisi bulunamadı!" });
                }

                // KENDİ TAMİRİ Mİ KONTROL ET (bana atanmış mı?)
                if (repair.PersonelId != personel.Id)
                {
                    return Json(new { success = false, message = "Bu tamir size ait değil!" });
                }

                // Zaten işlemdeyse veya tamamlanmışsa
                if (repair.StatusId == (int)RepairStatusEnum.IslemeAlindi)
                {
                    return Json(new { success = false, message = "Bu tamir zaten işlemde!" });
                }
                if (repair.StatusId == (int)RepairStatusEnum.Tamamlandi)
                {
                    return Json(new { success = false, message = "Bu tamir zaten tamamlanmış!" });
                }

                // SADECE DURUMU GÜNCELLE
                var oldStatusId = repair.StatusId;
                repair.StatusId = (int)RepairStatusEnum.IslemeAlindi;
                _unitOfWork.RepairItems.Update(repair);
                await _unitOfWork.CompleteAsync();

                // ========== SIGNALR İLE MÜŞTERİYE BİLDİRİM GÖNDER ==========
                try
                {
                    var hubContext = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub>)) as Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub>;
                    if (hubContext != null && repair.AppUserId != null)
                    {
                        await hubContext.Clients.User(repair.AppUserId).SendAsync("ReceiveMessage",
                            $"Tamir kaydınız ({repair.TrackingCode}) işleme alındı. Personel: {personel.FullName}",
                            "info");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR mesaj gönderilemedi: {ex.Message}");
                }

                // ========== İŞLEM LOGU ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
                await _logService.LogAsync(
                    action: $"{currentUserName} - Durum Güncelleme",
                    actionType: "Update",
                    entityName: "RepairItem",
                    entityId: id,
                    description: $"Personel tamiri üzerine aldı. Takip Kodu: {repair.TrackingCode}, Personel: {personel.FullName}",
                    oldValues: new { StatusId = (int)RepairStatusEnum.TeklifOnaylandi },
                    newValues: new { StatusId = (int)RepairStatusEnum.IslemeAlindi }
                );

                return Json(new { success = true, message = "Tamir işleme alındı!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        // POST: /Repair/AddMaterial
        [HttpPost]
        public async Task<IActionResult> AddMaterial(int repairId, bool isExternal, int? productId, string externalProductName, int quantity, string description)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";

                if (!isExternal && productId.HasValue)
                {
                    // Depodan malzeme - Stok düş
                    var product = await _productService.GetByIdAsync(productId.Value);
                    if (product == null)
                        return Json(new { success = false, message = "Ürün bulunamadı!" });

                    if (product.Quantity < quantity)
                        return Json(new { success = false, message = $"Yetersiz stok! Mevcut: {product.Quantity}" });

                    var stockResult = await _productService.StockOutAsync(productId.Value, quantity,
                        $"TAMIR-{repairId}", description ?? "Tamirde malzeme kullanımı", userId);

                    if (!stockResult.Success)
                        return Json(new { success = false, message = stockResult.Message });

                    var repairMaterial = new RepairMaterial
                    {
                        RepairId = repairId,
                        ProductId = productId,
                        Quantity = quantity,
                        Description = description,
                        MaterialType = "Stock",
                        UsedAt = DateTime.Now,
                        UsedBy = userId
                    };

                    await _unitOfWork.RepairMaterials.AddAsync(repairMaterial);
                    await _unitOfWork.CompleteAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"{quantity} adet {product.ProductName} stoktan düşüldü."
                    });
                }
                else
                {
                    // Dışarıdan malzeme
                    var repairMaterial = new RepairMaterial
                    {
                        RepairId = repairId,
                        ExternalProductName = externalProductName,
                        Quantity = quantity,
                        Description = description,
                        MaterialType = "External",
                        UsedAt = DateTime.Now,
                        UsedBy = userId
                    };

                    await _unitOfWork.RepairMaterials.AddAsync(repairMaterial);
                    await _unitOfWork.CompleteAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"{quantity} adet {externalProductName} dışarıdan malzeme olarak eklendi."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // POST: /Repair/DeleteMaterial
        [HttpPost]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            try
            {
                var material = await _unitOfWork.RepairMaterialRepository.GetByIdAsync(id);
                if (material == null)
                    return Json(new { success = false, message = "Malzeme bulunamadı!" });

                // Eğer stoktan düşülmüş bir malzemeyse, stoğu geri ekle
                if (material.MaterialType == "Stock" && material.ProductId.HasValue)
                {
                    var product = await _productService.GetByIdAsync(material.ProductId.Value);
                    if (product != null)
                    {
                        // Stok geri ekle
                        await _productService.StockInAsync(material.ProductId.Value, material.Quantity,
                            $"SILINEN-{material.Id}", $"Malzeme silindi: {material.Description}", User.Identity?.Name ?? "System");
                    }
                }

                await _unitOfWork.RepairMaterialRepository.DeleteAsync(id);
                await _unitOfWork.CompleteAsync();

                return Json(new { success = true, message = "Malzeme başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Repair/UpdateMaterial
        [HttpPost]
        public async Task<IActionResult> UpdateMaterial(int id, int quantity, string description)
        {
            try
            {
                var material = await _unitOfWork.RepairMaterialRepository.GetByIdAsync(id);
                if (material == null)
                    return Json(new { success = false, message = "Malzeme bulunamadı!" });

                var oldQuantity = material.Quantity;

                // Stoktan düşülmüş bir malzemeyse stok miktarını güncelle
                if (material.MaterialType == "Stock" && material.ProductId.HasValue && quantity != oldQuantity)
                {
                    var product = await _productService.GetByIdAsync(material.ProductId.Value);
                    if (product != null)
                    {
                        var diff = quantity - oldQuantity;
                        if (diff > 0)
                        {
                            // Daha fazla malzeme kullanıldı, stoktan düş
                            var stockResult = await _productService.StockOutAsync(material.ProductId.Value, diff,
                                $"TAMIR-{material.RepairId}", $"Malzeme miktarı güncellendi: {description}", User.Identity?.Name ?? "System");
                            if (!stockResult.Success)
                                return Json(new { success = false, message = stockResult.Message });
                        }
                        else if (diff < 0)
                        {
                            // Daha az malzeme kullanıldı, stoğa ekle
                            await _productService.StockInAsync(material.ProductId.Value, Math.Abs(diff),
                                $"TAMIR-{material.RepairId}", $"Malzeme miktarı güncellendi: {description}", User.Identity?.Name ?? "System");
                        }
                    }
                }

                material.Quantity = quantity;
                material.Description = description;
                material.UsedAt = DateTime.Now;

                _unitOfWork.RepairMaterials.Update(material);
                await _unitOfWork.CompleteAsync();

                return Json(new { success = true, message = "Malzeme başarıyla güncellendi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /Repair/GetMaterials
        [HttpGet]
        public async Task<IActionResult> GetMaterials(int repairId)
        {
            var materials = await _unitOfWork.RepairMaterialRepository.GetMaterialsByRepairIdAsync(repairId);

            var result = materials.Select(m => new
            {
                m.Id,

                productName = m.MaterialType == "External"
                    ? (m.ExternalProductName ?? "Dışarıdan Malzeme")
                    : (m.Product != null ? m.Product.ProductName : "Ürün bulunamadı"),
                m.Quantity,
                m.Description,
                usedAt = m.UsedAt.ToString("dd.MM.yyyy HH:mm"),
                m.UsedBy,
                materialType = m.MaterialType,
                externalProductName = m.ExternalProductName
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsForSelect()
        {
            var products = await _productService.GetAllAsync();
            var activeProducts = products.Where(p => p.IsActive && p.Quantity > 0)
                .Select(p => new
                {
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

        //========= Expertiz çıkarma ========
        [HttpGet]
        public async Task<IActionResult> AddExpertise(int id)
        {
            var repair = await _unitOfWork.RepairItems
                .GetByIdWithIncludeAsync(id, r => r.AppUser, r => r.Personel);

            if (repair == null)
                return NotFound();

            // Yetki kontrolü
            var user = await _userManager.GetUserAsync(User);
            var personel = (await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == user.Id)).FirstOrDefault();

            if (repair.PersonelId != personel?.Id)
                return Forbid();

            // Sadece "Ürün Kaydedildi" (StatusId=1) durumunda expertiz eklenebilir
            if (repair.StatusId != (int)RepairStatusEnum.UrunKaydedildi)
            {
                TempData["Error"] = "Bu ürün için expertiz eklenemez. Ürün durumu uygun değil.";
                return RedirectToAction("Index");
            }

            return View(repair);
        }

        [HttpPost]
        public async Task<IActionResult> SendExpertise(int repairId, [FromBody] List<ExpertiseLineDto> expertiseLines)
        {
            try
            {
                var repair = await _unitOfWork.RepairItems.GetByIdAsync(repairId);
                if (repair == null)
                    return Json(new { success = false, message = "Tamir kaydı bulunamadı!" });

                // Yetki kontrolü
                var user = await _userManager.GetUserAsync(User);
                var personel = (await _unitOfWork.Personels.GetWhereAsync(p => p.AppUserId == user.Id)).FirstOrDefault();

                if (repair.PersonelId != personel?.Id)
                    return Json(new { success = false, message = "Bu tamir size ait değil!" });

                // Durum kontrolü
                if (repair.StatusId != (int)RepairStatusEnum.UrunKaydedildi)
                    return Json(new { success = false, message = "Bu ürün için expertiz eklenemez!" });

                // Expertiz satırlarını kaydet
                foreach (var line in expertiseLines)
                {
                    var expertiseLine = new ExpertiseLine
                    {
                        RepairItemId = repairId,
                        Description = line.Description,
                        Quantity = line.Quantity,
                        Unit = line.Unit,
                        Note = line.Note,
                        LineOrder = line.LineOrder,
                        IsApproved = false,
                        IsIncludedInOffer = false,
                        CreatedAt = DateTime.Now
                    };
                    await _unitOfWork.ExpertiseLines.AddAsync(expertiseLine);
                }

                // Tamir durumunu güncelle
                repair.StatusId = (int)RepairStatusEnum.ExpertizeGonderildi;
                _unitOfWork.RepairItems.Update(repair);
                await _unitOfWork.CompleteAsync();

                return Json(new { success = true, message = $"{expertiseLines.Count} kalem expertiz notu onaya gönderildi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}", detail = ex.InnerException?.Message });
            }
        }



        // DTO
        public class ExpertiseLineDto
        {
            public string Description { get; set; }
            public int Quantity { get; set; }
            public string Unit { get; set; }
            public string Note { get; set; }
            public int LineOrder { get; set; }
        }

    }
}