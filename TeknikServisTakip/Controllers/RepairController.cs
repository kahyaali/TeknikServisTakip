using Business.Abstract;
using DataAccess.Context;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Entities.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Hubs;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Idari,Depo")]
    public class RepairController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IMailService _mailService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly AppDbContext _context;
        private readonly IProductService _productService;

        public RepairController(IUnitOfWork unitOfWork, ILogService logService, UserManager<AppUser> userManager, IMailService mailService, IWebHostEnvironment webHostEnvironment, AppDbContext context, IProductService productService)
        {
            _unitOfWork = unitOfWork; _logService = logService; _userManager = userManager; _mailService = mailService; _webHostEnvironment = webHostEnvironment; _context = context;
            _productService = productService;
        }

        // Tamir Listesi
        public async Task<IActionResult> Index()
        {

            var allRepairs = await _unitOfWork.GetAllRepairsWithImagesAsync();
            // StatusId 9 = Teslim Edildi, onları gösterme
            var repairs = allRepairs.Where(r => r.StatusId != (int)RepairStatusEnum.TeslimEdildi).OrderByDescending(r => r.ReceivedDate);
            return View(repairs);
        }

        // Tamir Ekle

        [HttpGet]
        public async Task<IActionResult> Create()
        {

            ViewBag.Personels = await _unitOfWork.Personels.GetWhereAsync(p => p.IsActive == true);

            // SADECE Customer rolündeki kullanıcıları getir
            var customers = await _userManager.GetUsersInRoleAsync("Customer");

            ViewBag.Customers = customers.Where(c => c.IsActive == true).OrderBy(c => c.FullName).ThenBy(c => c.CustomerNumber).ToList();

            // ========== Durum Listesi  ==========
            ViewBag.StatusList = Enum.GetValues(typeof(RepairStatusEnum))
                .Cast<RepairStatusEnum>()
                .Select(s => new SelectListItem
                {
                    Value = ((int)s).ToString(),
                    Text = s.GetDisplayName(),
                    Selected = (int)s == 1  // Default olarak "Ürün Kaydedildi" seçili
                }).ToList();

            return View(new RepairItem());
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RepairItem repair, List<IFormFile> BeforeImages, List<IFormFile> AfterImages)
        {
            // TRANSACTION BAŞLAT (TEK EKLENEN SATIR)
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // ========== MÜŞTERİ KONTROLÜ ==========
                if (string.IsNullOrEmpty(repair.AppUserId))
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Lütfen bir müşteri seçiniz!" });
                    }
                    TempData["Error"] = "Lütfen bir müşteri seçiniz!";
                    ViewBag.Personels = await _unitOfWork.Personels.GetWhereAsync(p => p.IsActive == true);
                    var customers = await _userManager.GetUsersInRoleAsync("Customer");
                    ViewBag.Customers = customers.Where(c => c.IsActive == true).ToList();

                    //======== Durum Listesi =========
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

                // Müşteri bilgilerini al
                var customer = await _userManager.FindByIdAsync(repair.AppUserId.ToString());
                if (customer == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Seçilen müşteri bulunamadı!" });
                    }
                    TempData["Error"] = "Seçilen müşteri bulunamadı!";
                    ViewBag.Personels = await _unitOfWork.Personels.GetWhereAsync(p => p.IsActive == true);
                    var customers = await _userManager.GetUsersInRoleAsync("Customer");
                    ViewBag.Customers = customers.Where(c => c.IsActive == true).ToList();

                    //========== Durum Lİstesi ===========
                    ViewBag.StatusList = Enum.GetValues(typeof(RepairStatusEnum))
                    .Cast<RepairStatusEnum>()
                    .Select(s => new SelectListItem
                    {
                        Value = ((int)s).ToString(),
                        Text = s.GetDisplayName(),
                        Selected = (int)s == 1
                    }).ToList();
                    return View(repair);
                }

                repair.CustomerNumber = customer.CustomerNumber;

                // Müşterinin emaili var mı kontrol et
                if (string.IsNullOrEmpty(customer.Email))
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Müşterinin email adresi bulunamadı! Mail gönderilemeyecek." });
                    }
                    TempData["Warning"] = "Müşterinin email adresi bulunamadı! Mail gönderilemeyecek.";
                }

                // PersonelId'yi al
                var personelIdStr = Request.Form["PersonelId"].ToString();
                if (!string.IsNullOrEmpty(personelIdStr))
                    repair.PersonelId = Convert.ToInt32(personelIdStr);

                // Takip Kodu ve Karekod oluştur
                repair.TrackingCode = await GenerateTrackingCode();
                repair.QrCodePath = await GenerateQRCode(repair.TrackingCode);

                if (repair.StatusId == null || repair.StatusId == 0)
                    repair.StatusId = (int)RepairStatusEnum.UrunKaydedildi;

                repair.ReceivedDate = DateTime.Now;
                repair.IsDeleted = false;

                await _unitOfWork.RepairItems.AddAsync(repair);
                await _unitOfWork.CompleteAsync();

                // ========== RESİMLERİ KAYDET ==========
                int beforeOrder = 1;
                int afterOrder = 1;

                // Öncesi resimleri
                if (BeforeImages != null && BeforeImages.Any())
                {
                    foreach (var image in BeforeImages.Take(5))
                    {
                        if (image != null && image.Length > 0)
                        {
                            if (!IsValidImage(image))
                            {
                                // HATA OLURSA ROLLBACK YAP
                                await transaction.RollbackAsync();

                                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                {
                                    return Json(new { success = false, message = GetImageErrorMessage(image) });
                                }
                                TempData["Error"] = GetImageErrorMessage(image);
                                return View(repair);
                            }

                            string fileName = await SaveImage(image, $"before_{repair.Id}_{beforeOrder}");
                            var repairImage = new RepairImage
                            {
                                RepairItemId = repair.Id,
                                ImagePath = "/uploads/" + fileName,
                                ImageType = "Before",
                                Order = beforeOrder
                            };
                            await _unitOfWork.RepairImages.AddAsync(repairImage);
                            beforeOrder++;
                        }
                    }
                }

                // Sonrası resimleri
                if (AfterImages != null && AfterImages.Any())
                {
                    foreach (var image in AfterImages.Take(5))
                    {
                        if (image != null && image.Length > 0)
                        {
                            if (!IsValidImage(image))
                            {
                                // HATA OLURSA ROLLBACK YAP
                                await transaction.RollbackAsync();

                                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                {
                                    return Json(new { success = false, message = GetImageErrorMessage(image) });
                                }
                                TempData["Error"] = GetImageErrorMessage(image);
                                return View(repair);
                            }
                            string fileName = await SaveImage(image, $"after_{repair.Id}_{afterOrder}");
                            var repairImage = new RepairImage
                            {
                                RepairItemId = repair.Id,
                                ImagePath = "/uploads/" + fileName,
                                ImageType = "After",
                                Order = afterOrder
                            };
                            await _unitOfWork.RepairImages.AddAsync(repairImage);
                            afterOrder++;
                        }
                    }
                }
                await _unitOfWork.CompleteAsync();

                // ========== ÜRÜN TAKİP LOGU ==========
                await _logService.LogProductTrackingAsync(
                    repairId: repair.Id,
                    action: "Created",
                    oldStatus: null,
                    newStatus: (repair.StatusId ?? 1).ToString(),
                    description: $"Yeni tamir kaydı oluşturuldu. Takip Kodu: {repair.TrackingCode}"
                );

                // ========== MÜŞTERİYE MAIL GÖNDER ==========
                bool mailSent = false;
                if (!string.IsNullOrEmpty(customer.Email))
                {
                    try
                    {
                        var baseUrl = $"{Request.Scheme}://{Request.Host}";
                        await _mailService.SendRepairCreatedMailAsync(
                            customer.Email,
                            customer.FullName ?? "Değerli Müşterimiz",
                            repair.ProductName ?? "Ürün",
                            repair.TrackingCode,
                            repair.QrCodePath,
                            baseUrl
                        );
                        mailSent = true;
                    }
                    catch (Exception ex)
                    {
                        await _logService.LogAsync(
                            action: "Repair/Create",
                            actionType: "EmailError",
                            entityName: "RepairItem",
                            entityId: repair.Id,
                            description: $"Mail gönderilemedi: {ex.Message} - Takip Kodu: {repair.TrackingCode}",
                            oldValues: null,
                            newValues: null
                        );
                    }
                }

                // BAŞARILI - COMMIT (TEK EKLENEN SATIR)
                await transaction.CommitAsync();

                // AJAX isteği mi kontrol ediyoruz
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, redirect = true, message = $"Tamir kaydı eklendi! Takip Kodu: {repair.TrackingCode}" });
                }

                TempData["Success"] = $"Tamir kaydı başarıyla eklendi! Takip Kodu: {repair.TrackingCode}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // HATA OLURSA ROLLBACK YAP
                await transaction.RollbackAsync();

                // HATA MESAJINI DETAYLI LOGLA
                var errorDetail = $"Hata: {ex.Message} | Inner: {ex.InnerException?.Message} | Stack: {ex.StackTrace}";
                await _logService.LogAsync(
                    action: "Repair/Create",
                    actionType: "Error",
                    entityName: "RepairItem",
                    entityId: null,
                    description: errorDetail,
                    oldValues: null,
                    newValues: null
                );

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    // DETAYLI HATA MESAJI
                    return Json(new
                    {
                        success = false,
                        message = $"Hata oluştu: {ex.Message}",
                        detail = ex.InnerException?.Message
                    });
                }

                TempData["Error"] = $"Hata oluştu: {ex.Message}";
                ViewBag.Personels = await _unitOfWork.Personels.GetWhereAsync(p => p.IsActive == true);
                var customers = await _userManager.GetUsersInRoleAsync("Customer");
                ViewBag.Customers = customers.Where(c => c.IsActive == true).ToList();
                //========= Durum Listesi =========
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
        }

        private string GetImageErrorMessage(IFormFile image)
        {
            var fileSizeMB = image.Length / 1024.0 / 1024.0;
            var ext = Path.GetExtension(image.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

            // Format kontrolü
            if (!allowedExtensions.Contains(ext))
            {
                return $"❌ {image.FileName}: Desteklenmeyen format. Sadece JPG, JPEG, PNG, GIF yükleyebilirsiniz.";
            }

            // Boyut kontrolü - AYNI LIMIT (10MB)
            if (image.Length > 10 * 1024 * 1024)
            {
                return $"📸 {image.FileName}: {fileSizeMB:F1}MB - Çok büyük! Maksimum 10MB olmalıdır.\n💡 İpucu: Fotoğrafı WhatsApp'a gönderip tekrar indirerek küçültebilirsiniz.";
            }

            return $"❌ {image.FileName}: Geçersiz dosya. (Boyut: {fileSizeMB:F1}MB, Limit: 10MB)";
        }

        // Tamir Düzenle
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var repair = await _unitOfWork.RepairItems.GetByIdWithIncludeAsync(id,
                r => r.Personel,
                r => r.RepairImages);

            if (repair == null) return NotFound();

            ViewBag.Personels = await _unitOfWork.Personels.GetWhereAsync(p => p.IsActive == true);
            ViewBag.Customers = await _userManager.GetUsersInRoleAsync("Customer");

            // Para birimi listesini view tarafına gönderiyoruz
            ViewBag.CurrencyList = TeknikServisTakip.Helpers.CurrencyHelper.GetCurrencyList();

            //========= Durum Listesi =========
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

        // Tamir Düzenle 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RepairItem model, List<IFormFile> BeforeImages, List<IFormFile> AfterImages, string DeletedBeforeImageIds, string DeletedAfterImageIds)
        {
            try
            {
                
                var existingRepair = await _unitOfWork.RepairItems.GetByIdWithIncludeAsync(model.Id,
                    r => r.RepairImages, r => r.AppUser); 

                if (existingRepair == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Kayıt bulunamadı!" });
                    return NotFound();
                }

                // ========== ESKİ DURUMU KAYDET ==========
                var oldStatusId = existingRepair.StatusId;

                // ========== TEMEL BİLGİLERİ GÜNCELLE ==========
                existingRepair.CustomerNumber = model.CustomerNumber;
                existingRepair.AppUserId = model.AppUserId;
                existingRepair.ProductName = model.ProductName;
                existingRepair.ProductBrand = model.ProductBrand;
                existingRepair.ProductModel = model.ProductModel;
                existingRepair.SerialNumber = model.SerialNumber;
                existingRepair.ProblemDescription = model.ProblemDescription;
                existingRepair.CustomerNote = model.CustomerNote;
                existingRepair.InternalNote = model.InternalNote;
                existingRepair.PersonelId = model.PersonelId;
                existingRepair.StatusId = model.StatusId;

                // ========== ÜRÜN TAKİP LOGU (DURUM DEĞİŞTİYSE) ==========
                // Durum değiştiyse ürün takip logu ekle 
                if (oldStatusId != model.StatusId)
                {
                    await _logService.LogProductTrackingAsync(
                        repairId: existingRepair.Id,
                        action: "StatusChanged",
                        oldStatus: (oldStatusId ?? 0).ToString(),
                        newStatus: (model.StatusId ?? 0).ToString(),
                        description: $"Tamir durumu değiştirildi: {oldStatusId} → {model.StatusId}"
                    );
                }
                existingRepair.Price = model.Price;
                existingRepair.EstimatedDeliveryDate = model.EstimatedDeliveryDate;
                // ========== TESLİM TARİHİNİ GÜNCELLE  ==========
                // Durum "Teslim Edildi" (StatusId=9) ise teslim tarihini ata
                int teslimEdildiId = (int)RepairStatusEnum.TeslimEdildi;
                if (model.StatusId == teslimEdildiId && existingRepair.DeliveryDate == null)
                {
                    existingRepair.DeliveryDate = DateTime.Now;
                }
                // Durum "Teslim Edildi" değilse teslim tarihini temizle
                else if (model.StatusId != teslimEdildiId)
                {
                    existingRepair.DeliveryDate = null;
                }


                // ========== TEKLİF KONTROLÜ ==========
                // OfferArchive'de bu repair item'a ait onaylanmış bir teklif var mı?
                var hasApprovedOffer = await _unitOfWork.OfferArchives.GetQueryable()
                    .AnyAsync(a => _unitOfWork.OfferLines.GetQueryable()
                        .Any(l => l.OfferId == a.OfferId && l.RepairItemId == existingRepair.Id));

                if (!hasApprovedOffer)
                {
                    // existingRepair'i güncelle, model.Price'ı değil!
                    existingRepair.Price = 0;
                    existingRepair.Currency = "TRY";

                    // İstersen model.Price'ı da güncelle (aynı değer olur)
                    model.Price = 0;
                    model.Currency = "TRY";
                }
                else
                {
                    // Onaylanmış teklif varsa gelen değeri kullan
                    existingRepair.Price = model.Price;
                    existingRepair.Currency = model.Currency;
                }
                // ========== TEKLİF KONTROLÜ SONU ==========





                var existingImages = existingRepair.RepairImages?.ToList() ?? new List<RepairImage>();

                // ========== SİLİNEN ÖNCESİ RESİMLER ==========
                if (!string.IsNullOrEmpty(DeletedBeforeImageIds))
                {
                    var deletedBeforeIds = DeletedBeforeImageIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                    foreach (var id in deletedBeforeIds)
                    {
                        var imageToDelete = existingImages.FirstOrDefault(i => i.Id == id && i.ImageType == "Before");
                        if (imageToDelete != null)
                        {
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageToDelete.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(filePath))
                                System.IO.File.Delete(filePath);
                            existingRepair.RepairImages.Remove(imageToDelete);
                        }
                    }
                }

                // ========== SİLİNEN SONRASI RESİMLER ==========
                if (!string.IsNullOrEmpty(DeletedAfterImageIds))
                {
                    var deletedAfterIds = DeletedAfterImageIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                    foreach (var id in deletedAfterIds)
                    {
                        var imageToDelete = existingImages.FirstOrDefault(i => i.Id == id && i.ImageType == "After");
                        if (imageToDelete != null)
                        {
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageToDelete.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(filePath))
                                System.IO.File.Delete(filePath);
                            existingRepair.RepairImages.Remove(imageToDelete);
                        }
                    }
                }

                // Mevcut resim sayılarını güncelle
                var currentBeforeCount = existingRepair.RepairImages.Count(i => i.ImageType == "Before");
                var currentAfterCount = existingRepair.RepairImages.Count(i => i.ImageType == "After");

                // ========== YENİ ÖNCESİ RESİMLER ==========
                if (BeforeImages != null && BeforeImages.Any())
                {
                    var newBeforeCount = BeforeImages.Count(b => b != null && b.Length > 0);
                    if (currentBeforeCount + newBeforeCount > 5)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            return Json(new { success = false, message = "Öncesi resimler en fazla 5 olabilir!" });
                        TempData["Error"] = "Öncesi resimler en fazla 5 olabilir!";
                        await LoadEditViewBags();
                        return View(model);
                    }

                    foreach (var file in BeforeImages)
                    {
                        if (file != null && file.Length > 0)
                        {
                            if (!IsValidImage(file))
                            {
                                string errorMsg = $"Geçersiz dosya: {file.FileName}. Sadece .jpg, .png, .gif dosyaları yükleyin!";
                                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                    return Json(new { success = false, message = errorMsg });
                                TempData["Error"] = errorMsg;
                                await LoadEditViewBags();
                                return View(model);
                            }
                            var imagePath = await SaveRepairImage(file, "before");
                            existingRepair.RepairImages.Add(new RepairImage
                            {
                                ImagePath = imagePath,
                                ImageType = "Before",
                                Order = existingRepair.RepairImages.Count(i => i.ImageType == "Before") + 1,
                                RepairItemId = existingRepair.Id
                            });
                        }
                    }
                }

                // ========== YENİ SONRASI RESİMLER ==========
                if (AfterImages != null && AfterImages.Any())
                {
                    var newAfterCount = AfterImages.Count(a => a != null && a.Length > 0);
                    if (currentAfterCount + newAfterCount > 5)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            return Json(new { success = false, message = "Sonrası resimler en fazla 5 olabilir!" });
                        TempData["Error"] = "Sonrası resimler en fazla 5 olabilir!";
                        await LoadEditViewBags();
                        return View(model);
                    }

                    foreach (var file in AfterImages)
                    {
                        if (file != null && file.Length > 0)
                        {
                            if (!IsValidImage(file))
                            {
                                string errorMsg = $"Geçersiz dosya: {file.FileName}. Sadece .jpg, .png, .gif dosyaları yükleyin!";
                                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                    return Json(new { success = false, message = errorMsg });
                                TempData["Error"] = errorMsg;
                                await LoadEditViewBags();
                                return View(model);
                            }
                            var imagePath = await SaveRepairImage(file, "after");
                            existingRepair.RepairImages.Add(new RepairImage
                            {
                                ImagePath = imagePath,
                                ImageType = "After",
                                Order = existingRepair.RepairImages.Count(i => i.ImageType == "After") + 1,
                                RepairItemId = existingRepair.Id
                            });
                        }
                    }
                }

                // ========== ARŞİVLEME (TESLİM EDİLDİ İSE) ==========
         
                if (oldStatusId != teslimEdildiId && model.StatusId == teslimEdildiId)
                {
                    var alreadyArchived = await _unitOfWork.ArchiveRepairs
                        .GetWhereAsync(a => a.OriginalRepairId == existingRepair.Id);

                    if (!alreadyArchived.Any())
                    {
                        var archive = new ArchiveRepair
                        {
                            CustomerNumber = existingRepair.CustomerNumber,
                            CustomerName = existingRepair.AppUser?.FullName,
                            AppUserId = existingRepair.AppUserId,
                            TrackingCode = existingRepair.TrackingCode,
                            ProductName = existingRepair.ProductName,
                            ProductBrand = existingRepair.ProductBrand,
                            ProductModel = existingRepair.ProductModel,
                            SerialNumber = model.SerialNumber,
                            ProblemDescription = existingRepair.ProblemDescription,
                            InternalNote = existingRepair.InternalNote,
                            ReceivedDate = existingRepair.ReceivedDate,
                            DeliveryDate = DateTime.Now,
                            PersonelId = existingRepair.PersonelId,
                            Price = existingRepair.Price,
                            Currency= existingRepair.Currency,
                            OriginalRepairId = existingRepair.Id,
                            ArchivedAt = DateTime.Now
                        };

                        await _unitOfWork.ArchiveRepairs.AddAsync(archive);
                        System.Diagnostics.Debug.WriteLine($"Arşive eklendi: {existingRepair.TrackingCode}");
                    }
                }

                _unitOfWork.RepairItems.Update(existingRepair);
                await _unitOfWork.CompleteAsync();

                // Signal iletişimi        
                try
                {
                    var hubContext = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub>)) as Microsoft.AspNetCore.SignalR.IHubContext<NotificationHub>;
                    if (hubContext != null && existingRepair.AppUserId != null)
                    {
                        string statusName = "Güncellendi";
                        if (model.StatusId.HasValue)
                        {
                            var statusEnum = (RepairStatusEnum)model.StatusId.Value;
                            statusName = statusEnum.GetDisplayName();
                        }

                        // KONSOLA YAZDIR
                        System.Diagnostics.Debug.WriteLine($"SignalR gönderiliyor - UserId: {existingRepair.AppUserId}, Status: {statusName}");

                        // Toastr mesajı gönder
                        await hubContext.Clients.User(existingRepair.AppUserId).SendAsync("ReceiveMessage",
                            $"Tamir kaydınız ({existingRepair.TrackingCode}) güncellendi. Yeni durum: {statusName}",
                            "success");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR mesaj gönderilemedi: {ex.Message}");
                }


                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Tamir kaydı güncellendi!", redirectUrl = "/Repair/Index" });
                }

                TempData["Success"] = "Tamir kaydı başarıyla güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = ex.Message });
                }
                TempData["Error"] = ex.Message;
                await LoadEditViewBags();
                return View(model);
            }
        }

        // Yardımcı metod - ViewBag'leri yükle
        private async Task LoadEditViewBags()
        {         
            ViewBag.Personels = await _unitOfWork.Personels.GetWhereAsync(p => p.IsActive == true);
            ViewBag.Customers = await _unitOfWork.Users.GetAllAsync();

            //========= Durum Bilgisi ==========
            ViewBag.StatusList = Enum.GetValues(typeof(RepairStatusEnum))
        .Cast<RepairStatusEnum>()
        .Select(s => new SelectListItem
        {
            Value = ((int)s).ToString(),
            Text = s.GetDisplayName(),
            Selected = false
        }).ToList();
        }

        // Virüs ve zararlı dosya eklenmesini engellemek için ve dosya boyutu kontrolü 
        private bool IsValidImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(ext)) return false;
            //if (file.Length > 5 * 1024 * 1024) return false; // 5MB resim boyutu                                                          
            if (file.Length > 10 * 1024 * 1024) return false; // 10MB resim boyutu

            return true;
        }

        // Resim kaydetme
        private async Task<string> SaveRepairImage(IFormFile file, string type)
        {
            if (!IsValidImage(file))
            {
                throw new Exception("Geçersiz dosya formatı veya boyutu! Sadece .jpg, .png, .gif dosyaları ve max 10MB yüklenebilir.");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "repairs");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{type}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/repairs/{uniqueFileName}";
        }

        // Resim kaydetme 
        private async Task<string> SaveImage(IFormFile image, string prefix)
        {
            if (!IsValidImage(image))
            {
                throw new Exception("Geçersiz dosya formatı veya boyutu! Sadece .jpg, .png, .gif dosyaları ve max 10MB yüklenebilir.");
            }

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"{prefix}_{Guid.NewGuid()}_{image.FileName}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            return uniqueFileName;
        }

        // Ürün Silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var repair = await _unitOfWork.RepairItems.GetByIdAsync(id);
            if (repair == null)
            {
                return Json(new { success = false, message = "Kayıt bulunamadı!" });
            }

            // QR kod dosyasını sil
            if (!string.IsNullOrEmpty(repair.QrCodePath))
            {
                string qrFilePath = Path.Combine(_webHostEnvironment.WebRootPath, repair.QrCodePath.TrimStart('/'));
                if (System.IO.File.Exists(qrFilePath))
                {
                    System.IO.File.Delete(qrFilePath);
                }
            }

            _unitOfWork.RepairItems.Delete(repair);
            await _unitOfWork.CompleteAsync();

            return Json(new { success = true, message = "Tamir kaydı ve dosyaları silindi!" });
        }

        // Tamir Ürünü Detay
        public async Task<IActionResult> Details(int id)
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

        // Benzersiz takip kodu üret
        private async Task<string> GenerateTrackingCode()
        {
            var repairs = await _unitOfWork.RepairItems.GetAllAsync();
            int count = repairs.Count() + 1;
            string year = DateTime.Now.Year.ToString();
            return $"TAK-{year}-{count:D4}";
        }

        // Karekod oluştur
        private async Task<string> GenerateQRCode(string text)
        {
           
            string qrFolder = Path.Combine(_webHostEnvironment.WebRootPath, "qrcodes");
            if (!Directory.Exists(qrFolder))
                Directory.CreateDirectory(qrFolder);

            string fileName = $"qr_{text}.png";
            string filePath = Path.Combine(qrFolder, fileName);

            using (var qrGenerator = new QRCodeGenerator())
            {        
                var qrData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrData))
                {
                    
                    // 1. Parametre (15): Her bir karecik 15x15 piksel olsun (görsel çok devasa olmasın).
                    // 2. Parametre (new byte[] { 0, 0, 0 }): Siyah renk (RGB).
                    // 3. Parametre (new byte[] { 255, 255, 255 }): Beyaz renk (RGB).
                    // 4. Parametre (true): ETRAFINA BEYAZ BOŞLUK (QUIET ZONE) ÇİZ! (Kameranın okuması için en kritik ayar)
                    byte[] qrCodeBytes = qrCode.GetGraphic(15, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 }, true);

                    await System.IO.File.WriteAllBytesAsync(filePath, qrCodeBytes);
                }
            }

            return "/qrcodes/" + fileName;
        }

        // ========== TAMİR ARŞİVİ ==========
        public async Task<IActionResult> ArchiveRepairs(string searchTerm)
        {
         
            return View();
        }

        // ArciveRepair server side 
        [HttpPost]
        public async Task<IActionResult> GetArchiveRepairsJson(int draw, int start, int length, string search = null)
        {
            var query = _unitOfWork.ArchiveRepairs.GetAllAsync(a => a.AppUser, a => a.Personel).Result.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    (a.CustomerNumber != null && a.CustomerNumber.Contains(search)) ||
                    (a.TrackingCode != null && a.TrackingCode.Contains(search)) ||
                    (a.ProductName != null && a.ProductName.Contains(search)) ||
                    (a.CustomerName != null && a.CustomerName.Contains(search))
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
                customerNumber = a.CustomerNumber ?? "-",
                customerName = a.CustomerName ?? "-",
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

        // Arşiv Detay
        //public async Task<IActionResult> ArchiveRepairDetail(int id)
        //{
        //    var archive = await _unitOfWork.ArchiveRepairs.GetByIdWithIncludeAsync(id, a => a.AppUser, a => a.Personel);
        //    if (archive == null) return NotFound();
        //    return View(archive);
        //}


        public async Task<IActionResult> ArchiveRepairDetail(int id)
        {
            var archive = await _unitOfWork.ArchiveRepairs.GetByIdWithIncludeAsync(id,
                a => a.AppUser,
                a => a.Personel);

            if (archive == null) return NotFound();

            // Bu arşiv kaydına ait malzemeleri getir (OriginalRepairId ile)
            var materials = await _unitOfWork.RepairMaterials
                .GetWhereAsync(m => m.RepairId == archive.OriginalRepairId,
                    m => m.Product);

            ViewBag.Materials = materials.OrderByDescending(m => m.UsedAt).ToList();

            return View(archive);
        }

        // Arşivden Sil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArchiveRepair(int id)
        {
            var archive = await _unitOfWork.ArchiveRepairs.GetByIdAsync(id);
            if (archive == null)
            {
                return Json(new { success = false, message = "Kayıt bulunamadı!" });
            }

            var trackingCode = archive.TrackingCode;
            // ========== İŞLEM LOGU ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";
            await _logService.LogAsync(
                action: $"{currentUserName} - Ürün Arşiv Bilgileri Silme",
                actionType: "Delete",
                entityName: "ArchiveRepair",
                entityId: id,
                description: $"Arşiv kaydı silindi: {trackingCode}",
                oldValues: null,
                newValues: null
            );

            _unitOfWork.ArchiveRepairs.Delete(archive);
            await _unitOfWork.CompleteAsync();

            return Json(new { success = true, message = "Arşiv kaydı silindi!" });
        }


        public async Task<IActionResult> PrintAll()
        {
            var repairs = await _unitOfWork.RepairItems
                .GetAllWithIncludeAsync( r => r.Personel, r => r.RepairImages);

            var orderedRepairs = repairs.OrderByDescending(r => r.ReceivedDate).ToList();

            return View(orderedRepairs);
        }


        // ========== TESLİM EDİLENLER ARŞİVİ (Server-side) ==========
        public async Task<IActionResult> DeliveredRepairs()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetDeliveredRepairsJson(int draw, int start, int length, string search = null)
        {
            var allRepairs = await _unitOfWork.GetAllRepairsWithImagesAsync();
            var query = allRepairs.Where(r => r.StatusId == (int)RepairStatusEnum.TeslimEdildi).AsQueryable(); // Sadece Teslim Edilenler

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r =>
                    (r.CustomerNumber != null && r.CustomerNumber.Contains(search)) ||
                    (r.ProductName != null && r.ProductName.Contains(search)) ||
                    (r.ProductBrand != null && r.ProductBrand.Contains(search)) ||
                    (r.SerialNumber != null && r.SerialNumber.Contains(search)) ||
                    (r.TrackingCode != null && r.TrackingCode.Contains(search))
                );
            }

            var totalCount = query.Count();
            var take = length <= 0 ? 10 : length;
            var skip = start < 0 ? 0 : start;

            var repairs = query
                .OrderByDescending(r => r.ReceivedDate)
                .Skip(skip)
                .Take(take)
                .ToList();

            var dataList = new List<object>();
            foreach (var item in repairs)
            {
                var beforeImages = item.RepairImages?.Where(i => i.ImageType == "Before").OrderBy(i => i.Order).Select(i => i.ImagePath).Where(p => !string.IsNullOrEmpty(p)).ToList() ?? new List<string>();
                var afterImages = item.RepairImages?.Where(i => i.ImageType == "After").OrderBy(i => i.Order).Select(i => i.ImagePath).Where(p => !string.IsNullOrEmpty(p)).ToList() ?? new List<string>();
                var allImages = beforeImages.Concat(afterImages).ToList();

                var currency = item.Currency ?? "TRY";

                dataList.Add(new
                {
                    id = item.Id,
                    customerNumber = item.CustomerNumber ?? "-",
                    productName = item.ProductName ?? "-",
                    productBrand = item.ProductBrand ?? "-",
                    productModel = item.ProductModel ?? "-",
                    serialNumber = item.SerialNumber ?? "-",
                    problemDescription = (item.ProblemDescription?.Length > 50 ? item.ProblemDescription.Substring(0, 50) + "..." : item.ProblemDescription) ?? "-",
                    receivedDate = item.ReceivedDate.ToString("dd.MM.yyyy"),
                    deliveryDate = item.DeliveryDate?.ToString("dd.MM.yyyy") ?? "-",
                    personel = item.Personel?.FullName ?? "Atanmamış",
                    price = item.Price.ToString("F2"),
                    currency = currency, 
                    currencySymbol = CurrencyHelper.GetSymbol(currency),
                    trackingCode = item.TrackingCode ?? "-",
                    beforeImageCount = beforeImages.Count,
                    afterImageCount = afterImages.Count,
                    beforeFirstImage = beforeImages.FirstOrDefault() ?? "",
                    afterFirstImage = afterImages.FirstOrDefault() ?? "",
                    imagesJson = System.Text.Json.JsonSerializer.Serialize(allImages)
                });
            }

            return Json(new { draw, recordsTotal = totalCount, recordsFiltered = totalCount, data = dataList });
        }

        // Teslim Edilenler için Detay Sayfası
        public async Task<IActionResult> DeliveredDetails(int id)
        {
            var repair = await _unitOfWork.RepairItems
                .GetByIdWithIncludeAsync(id,
                    r => r.Personel,
                    r => r.RepairImages,
                    r => r.AppUser);

            if (repair == null)
            {
                TempData["Error"] = "Kayıt bulunamadı!";
                return RedirectToAction("DeliveredRepairs");
            }

            return View(repair);

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

    }
}
