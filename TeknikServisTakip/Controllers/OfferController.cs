using DataAccess.UnitOfWork;
using Entities.Concrete;
using Entities.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknikServisTakip.Helpers;
using TeknikServisTakip.Models;
using TeknikServisTakip.Models.ViewModels;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class OfferController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogService _logService;

        public OfferController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, IWebHostEnvironment webHostEnvironment, ILogService logService)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _logService = logService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allOffers = await _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            // Onaylanmış tekliflerin ID'lerini OfferArchive'den alıyoruz (son 20)
            var approvedOfferIds = await _unitOfWork.OfferArchives.GetQueryable()
                .OrderByDescending(a => a.ApprovedAt)
                .Take(20)
                .Select(a => a.OfferId)
                .Distinct()
                .ToListAsync();

            // TOPLAM onaylanan teklif sayısı (badge için)
            var totalApprovedCount = await _unitOfWork.OfferArchives.GetQueryable()
                .Select(a => a.OfferId)
                .Distinct()
                .CountAsync();

            ViewBag.ApprovedOfferIds = approvedOfferIds;
            ViewBag.TotalApprovedCount = totalApprovedCount;

            return View(allOffers);
        }



        [HttpGet]
        public async Task<IActionResult> Create(int? parentOfferId = null)
        {
            var model = new OfferFormViewModel();
            string companyName = "Müşteri Firma Bilgisi Bulunamadı";

            if (!parentOfferId.HasValue)
            {
                string selectedIdsStr = TempData["SelectedExpertiseIds"]?.ToString();
                if (string.IsNullOrEmpty(selectedIdsStr))
                {
                    TempData["ErrorMessage"] = "Havuzdan seçilmiş kalem bulunamadı.";
                    return RedirectToAction("Index", "ExpertiseApproval");
                }

                var ids = selectedIdsStr.Split(',').Select(int.Parse).ToList();
                var expLines = await _unitOfWork.ExpertiseLines.GetQueryable()
                    .Include(e => e.RepairItem)
                    .ThenInclude(r => r.AppUser)
                    .Where(e => ids.Contains(e.Id))
                    .ToListAsync();

                if (!expLines.Any()) return RedirectToAction("Index", "ExpertiseApproval");

                var firstLine = expLines.First();
                model.CustomerNumber = firstLine.RepairItem.CustomerNumber;
                model.CustomerName = firstLine.RepairItem.AppUser?.FullName ?? "Müşteri";
                model.Note = "Teklif Formu";

                // Havuzdan gelen CustomerNumber'a göre müşterinin firma adını çekiyoruz 
                if (!string.IsNullOrEmpty(model.CustomerNumber))
                {
                    var customerUser = await _unitOfWork.Users.GetQueryable()
                        .FirstOrDefaultAsync(u => u.CustomerNumber == model.CustomerNumber);

                    if (customerUser != null)
                    {
                        companyName = !string.IsNullOrWhiteSpace(customerUser.CompanyName)
                            ? customerUser.CompanyName
                            : $"{customerUser.FullName} (Firma Belirtilmemiş)";
                    }
                }

                var groupedByDevice = expLines.GroupBy(e => new { e.RepairItemId, e.RepairItem.ProductName });
                foreach (var g in groupedByDevice)
                {
                    var groupItem = new ProductGroupItemViewModel
                    {
                        RepairItemId = g.Key.RepairItemId,
                        ProductName = g.Key.ProductName,
                        TaxRate = 20
                    };

                    foreach (var line in g)
                    {
                        groupItem.Lines.Add(new ProductLineItemViewModel
                        {
                            ExpertiseLineId = line.Id,
                            Description = line.Description,
                            Quantity = line.Quantity,
                            Unit = line.Unit,
                            UnitPrice = 0,
                            TechnicianNote = line.Note ?? ""
                        });
                    }
                    model.ProductGroups.Add(groupItem);
                }
            }
            else
            {
                var oldOffer = await _unitOfWork.Offers.GetByIdAsync(parentOfferId.Value);
                if (oldOffer == null) return NotFound();

                var oldOfferLines = await _unitOfWork.OfferLines.GetQueryable()
                    .Where(l => l.OfferId == parentOfferId.Value)
                    .ToListAsync();

                if (oldOfferLines == null || !oldOfferLines.Any())
                {
                    TempData["ErrorMessage"] = "Revize edilecek teklife ait kalem bulunamadı.";
                    return RedirectToAction("Index", "Offer");
                }

                model.ParentOfferId = oldOffer.Id;
                model.CustomerNumber = oldOffer.CustomerNumber;
                model.CustomerName = oldOffer.CustomerName;
                model.Currency = oldOffer.Currency;
                string cleanedNote = System.Text.RegularExpressions.Regex.Replace(oldOffer.Note ?? "", @"\s*\(v\d+\s+Revizyonu\)", "");
                model.Note = cleanedNote + $" (v{oldOffer.Version} Revizyonu)";

                if (!string.IsNullOrEmpty(model.CustomerNumber))
                {
                    var customerUser = await _unitOfWork.Users.GetQueryable()
                        .FirstOrDefaultAsync(u => u.CustomerNumber == model.CustomerNumber);

                    if (customerUser != null)
                    {
                        companyName = !string.IsNullOrWhiteSpace(customerUser.CompanyName)
                            ? customerUser.CompanyName
                            : $"{customerUser.FullName} (Firma Belirtilmemiş)";
                    }
                }

                var groupedLines = oldOfferLines.GroupBy(l => l.RepairItemId);
                foreach (var g in groupedLines)
                {
                    var firstLine = g.First();
                    var repairItem = await _unitOfWork.RepairItems.GetByIdAsync(g.Key);

                    var groupItem = new ProductGroupItemViewModel
                    {
                        RepairItemId = g.Key,
                        ProductName = repairItem?.ProductName ?? "Ürün",
                        LaborCost = g.Sum(x => x.LaborCost),
                        DiscountRate = firstLine.DiscountRate,
                        TaxRate = firstLine.TaxRate
                    };

                    foreach (var l in g)
                    {
                        groupItem.Lines.Add(new ProductLineItemViewModel
                        {
                            ExpertiseLineId = l.ExpertiseLineId,
                            Description = l.Description,
                            Quantity = l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = l.UnitPrice,
                            Currency = l.Currency,
                            TechnicianNote = l.TechnicianNote ?? ""
                        });
                    }
                    model.ProductGroups.Add(groupItem);
                }
            }


            model.CompanyName = companyName;

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OfferFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = new List<string>();
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    if (state.Errors.Any())
                    {
                        errors.Add($"{key}: {string.Join(", ", state.Errors.Select(e => e.ErrorMessage))}");
                    }
                }

                TempData["ErrorMessage"] = $"ModelState Geçersiz: {string.Join(" | ", errors)}";
                return View(model);
            }


            if (model.ProductGroups != null)
            {
                for (int i = 0; i < model.ProductGroups.Count; i++)
                {
                    // 1. Grup bazlı Labor, Discount, Tax düzeltmeleri
                    string laborStr = Request.Form[$"ProductGroups[{i}].LaborCost"];
                    string discStr = Request.Form[$"ProductGroups[{i}].DiscountRate"];
                    string taxStr = Request.Form[$"ProductGroups[{i}].TaxRate"];

                    if (!string.IsNullOrEmpty(laborStr))
                        model.ProductGroups[i].LaborCost = decimal.Parse(laborStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(discStr))
                        model.ProductGroups[i].DiscountRate = decimal.Parse(discStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(taxStr))
                        model.ProductGroups[i].TaxRate = decimal.Parse(taxStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                    // ModelState üzerindeki haksız kültür hatalarını temizliyoruz
                    ModelState.Remove($"ProductGroups[{i}].LaborCost");
                    ModelState.Remove($"ProductGroups[{i}].DiscountRate");
                    ModelState.Remove($"ProductGroups[{i}].TaxRate");

                    // 2. Satır bazlı Quantity ve UnitPrice düzeltmeleri
                    if (model.ProductGroups[i].Lines != null)
                    {
                        for (int j = 0; j < model.ProductGroups[i].Lines.Count; j++)
                        {
                            string qtyStr = Request.Form[$"ProductGroups[{i}].Lines[{j}].Quantity"];
                            string priceStr = Request.Form[$"ProductGroups[{i}].Lines[{j}].UnitPrice"];

                            if (!string.IsNullOrEmpty(qtyStr))
                            {
                                var parsedQty = decimal.Parse(qtyStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                                model.ProductGroups[i].Lines[j].Quantity = (int)parsedQty;
                            }
                            if (!string.IsNullOrEmpty(priceStr))
                            {
                                model.ProductGroups[i].Lines[j].UnitPrice = decimal.Parse(priceStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                            }

                            ModelState.Remove($"ProductGroups[{i}].Lines[{j}].Quantity");
                            ModelState.Remove($"ProductGroups[{i}].Lines[{j}].UnitPrice");
                        }
                    }
                }
            }
            // ------------------------------------------------------------

            if (model.ProductGroups == null || !model.ProductGroups.Any())
            {
                ModelState.AddModelError("", "Fiyatlandırılacak geçerli bir ürün grubu bulunamadı!");
                return View(model);
            }


            if (!ModelState.IsValid) return View(model);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                int nextVersion = 1;
                if (model.ParentOfferId.HasValue)
                {
                    var parentOffer = await _unitOfWork.Offers.GetByIdAsync(model.ParentOfferId.Value);
                    if (parentOffer != null)
                        nextVersion = parentOffer.Version + 1;  
                }

                string currentUser = User.Identity?.Name ?? "System";
                var trZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, trZone);
                string uniqueSuffix = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
                string generatedOfferNumber = $"TKF-{now:yyyyMMdd}-{uniqueSuffix}";

                var offer = new Offer
                {
                    ParentOfferId = model.ParentOfferId,
                    Version = nextVersion,
                    CustomerNumber = model.CustomerNumber,
                    CustomerName = model.CustomerName,
                    Note = model.Note,
                    Currency = model.Currency ?? "TRY",
                    OfferNumber = generatedOfferNumber,
                    CreatedBy = currentUser,
                    CreatedDate = now,
                    IsActive = true,
                    TotalLinesAmount = 0,
                    TotalLaborCost = 0,
                    TotalDiscountAmount = 0,
                    TotalTaxAmount = 0,
                    GrandTotal = 0
                };

                await _unitOfWork.Offers.AddAsync(offer);
                await _unitOfWork.CompleteAsync();

                decimal totalLinesAmount = 0;
                decimal totalLaborCost = 0;
                decimal totalDiscountAmount = 0;
                decimal totalTaxAmount = 0;

                var expertiseLineIds = model.ProductGroups
                    .SelectMany(g => g.Lines ?? new List<ProductLineItemViewModel>())
                    .Where(l => l.ExpertiseLineId.HasValue)
                    .Select(l => l.ExpertiseLineId.Value)
                    .Distinct()
                    .ToList();

                var allExpertiseLines = await _unitOfWork.ExpertiseLines.GetQueryable()
                    .Where(e => expertiseLineIds.Contains(e.Id))
                    .ToListAsync();

                foreach (var group in model.ProductGroups)
                {
                    if (group.Lines == null || !group.Lines.Any()) continue;

                    var repairItem = await _unitOfWork.RepairItems.GetByIdAsync(group.RepairItemId);
                    if (repairItem != null)
                    {

                        repairItem.StatusId = (int)Entities.Enum.RepairStatusEnum.TeklifGonderildi;
                        _unitOfWork.RepairItems.Update(repairItem);
                    }

                    int lineIndex = 0;
                    foreach (var line in group.Lines)
                    {
                        decimal assignedLabor = (lineIndex == 0) ? group.LaborCost : 0;
                        decimal lineSubTotal = (line.Quantity * line.UnitPrice) + assignedLabor;
                        decimal lineDiscount = lineSubTotal * (group.DiscountRate / 100);
                        decimal lineTaxableAmount = lineSubTotal - lineDiscount;
                        decimal lineTax = lineTaxableAmount * (group.TaxRate / 100);
                        decimal lineTotal = lineTaxableAmount + lineTax;

                        totalLinesAmount += line.Quantity * line.UnitPrice;
                        totalLaborCost += assignedLabor;
                        totalDiscountAmount += lineDiscount;
                        totalTaxAmount += lineTax;

                        var offerLine = new OfferLine
                        {
                            OfferId = offer.Id,
                            RepairItemId = group.RepairItemId,
                            ExpertiseLineId = line.ExpertiseLineId,
                            Description = line.Description,
                            Quantity = line.Quantity,
                            Unit = line.Unit,
                            UnitPrice = line.UnitPrice,
                            LaborCost = assignedLabor,
                            TaxRate = group.TaxRate,
                            DiscountRate = group.DiscountRate,

                            Currency = offer.Currency,
                            SubTotal = lineSubTotal,
                            Total = lineTotal,
                            TechnicianNote = line.TechnicianNote ?? ""
                        };

                        await _unitOfWork.OfferLines.AddAsync(offerLine);

                        if (line.ExpertiseLineId.HasValue)
                        {
                            var expLine = allExpertiseLines.FirstOrDefault(e => e.Id == line.ExpertiseLineId.Value);
                            if (expLine != null)
                            {
                                expLine.IsIncludedInOffer = true;
                                _unitOfWork.ExpertiseLines.Update(expLine);
                            }
                        }
                        lineIndex++;
                    }
                }

                offer.TotalLinesAmount = totalLinesAmount;
                offer.TotalLaborCost = totalLaborCost;
                offer.TotalDiscountAmount = totalDiscountAmount;
                offer.TotalTaxAmount = totalTaxAmount;
                offer.GrandTotal = (totalLinesAmount + totalLaborCost - totalDiscountAmount) + totalTaxAmount;

                _unitOfWork.Offers.Update(offer);
                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                // ========== İşlem Logu ==========
                var crtUser = await _userManager.GetUserAsync(User);
                var currentUserName = crtUser?.FullName ?? crtUser?.Email ?? "Bilinmeyen Kullanıcı";
                var actionType = model.ParentOfferId.HasValue ? "Revize" : "Create";

                await _logService.LogAsync(
                    action: $"{currentUserName} - Teklif {actionType}",
                    actionType: actionType,
                    entityName: "Offer",
                    entityId: offer.Id,
                    description: $"{actionType} işlemi gerçekleştirildi. Teklif No: {offer.OfferNumber}, Versiyon: v{offer.Version}",
                    oldValues: model.ParentOfferId.HasValue ? new { ParentOfferId = model.ParentOfferId } : null,
                    newValues: new { offer.OfferNumber, offer.Version, offer.GrandTotal, ProductCount = model.ProductGroups.Count }
                );
                // ========== İşlem Logu Sonu ==========


                TempData["SuccessMessage"] = $"Teklif başarıyla kaydedildi. Sürüm: v{offer.Version}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                ModelState.AddModelError("", "Kayıt Başarısız! Detay: " + innerMessage);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var offer = await _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .ThenInclude(l => l.RepairItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null) return NotFound("Düzenlenecek teklif bulunamadı.");
            if (!offer.IsActive) return BadRequest("Onaylanmış veya arşivlenmiş teklifler doğrudan düzenlenemez!");


            string companyName = "Müşteri Firma Bilgisi Bulunamadı";

            if (!string.IsNullOrEmpty(offer.CustomerNumber))
            {

                var customerUser = await _unitOfWork.Users.GetQueryable()
                    .FirstOrDefaultAsync(u => u.CustomerNumber == offer.CustomerNumber);

                if (customerUser != null)
                {

                    companyName = !string.IsNullOrWhiteSpace(customerUser.CompanyName)
                        ? customerUser.CompanyName
                        : $"{customerUser.FullName} (Firma Belirtilmemiş)";
                }
            }

            string cleanedNote = System.Text.RegularExpressions.Regex.Replace(offer.Note ?? "", @"\s*\(v\d+\s+Revizyonu\)", "");

            var model = new OfferFormViewModel
            {
                Id = offer.Id,
                ParentOfferId = offer.ParentOfferId,
                CustomerNumber = offer.CustomerNumber,
                CustomerName = offer.CustomerName,
                CompanyName = companyName,
                Note = cleanedNote,
                Currency = offer.Currency
            };

            var groupedLines = offer.OfferLines.GroupBy(l => new { l.RepairItemId, l.RepairItem.ProductName });
            foreach (var g in groupedLines)
            {
                var firstLine = g.First();
                var groupItem = new ProductGroupItemViewModel
                {
                    RepairItemId = g.Key.RepairItemId,
                    ProductName = g.Key.ProductName,
                    LaborCost = g.Sum(x => x.LaborCost),
                    DiscountRate = firstLine.DiscountRate,
                    TaxRate = firstLine.TaxRate
                };

                foreach (var l in g)
                {
                    groupItem.Lines.Add(new ProductLineItemViewModel
                    {
                        ExpertiseLineId = l.ExpertiseLineId,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        Unit = l.Unit,
                        UnitPrice = l.UnitPrice,
                        Currency = l.Currency,
                        TechnicianNote = l.TechnicianNote ?? ""
                    });
                }
                model.ProductGroups.Add(groupItem);
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OfferFormViewModel model)
        {
            if (model.ProductGroups != null)
            {
                for (int i = 0; i < model.ProductGroups.Count; i++)
                {
                    // 1. Grup bazlı Labor, Discount, Tax düzeltmeleri
                    string laborStr = Request.Form[$"ProductGroups[{i}].LaborCost"];
                    string discStr = Request.Form[$"ProductGroups[{i}].DiscountRate"];
                    string taxStr = Request.Form[$"ProductGroups[{i}].TaxRate"];

                    if (!string.IsNullOrEmpty(laborStr))
                        model.ProductGroups[i].LaborCost = decimal.Parse(laborStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(discStr))
                        model.ProductGroups[i].DiscountRate = decimal.Parse(discStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(taxStr))
                        model.ProductGroups[i].TaxRate = decimal.Parse(taxStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                    ModelState.Remove($"ProductGroups[{i}].LaborCost");
                    ModelState.Remove($"ProductGroups[{i}].DiscountRate");
                    ModelState.Remove($"ProductGroups[{i}].TaxRate");

                    // 2. Satır bazlı Quantity ve UnitPrice düzeltmeleri
                    if (model.ProductGroups[i].Lines != null)
                    {
                        for (int j = 0; j < model.ProductGroups[i].Lines.Count; j++)
                        {
                            string qtyStr = Request.Form[$"ProductGroups[{i}].Lines[{j}].Quantity"];
                            string priceStr = Request.Form[$"ProductGroups[{i}].Lines[{j}].UnitPrice"];

                            if (!string.IsNullOrEmpty(qtyStr))
                            {
                                var parsedQty = decimal.Parse(qtyStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                                model.ProductGroups[i].Lines[j].Quantity = (int)parsedQty;
                            }

                            if (!string.IsNullOrEmpty(priceStr))
                            {
                                model.ProductGroups[i].Lines[j].UnitPrice = decimal.Parse(priceStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                            }

                            ModelState.Remove($"ProductGroups[{i}].Lines[{j}].Quantity");
                            ModelState.Remove($"ProductGroups[{i}].Lines[{j}].UnitPrice");
                        }
                    }
                }
                model.ProductGroups = model.ProductGroups
                    .Where(g => !g.IsGroupDeleted)
                    .ToList();
            }

            if (model.ProductGroups == null || !model.ProductGroups.Any())
            {
                ModelState.AddModelError("", "Fiyatlandırılacak geçerli bir ürün grubu bulunamadı!");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new { Alan = x.Key, Hata = x.Value.Errors.First().ErrorMessage });
                string hataMesaji = "Doğrulama Hatası: " + string.Join(" | ", errors.Select(e => $"{e.Alan}: {e.Hata}"));
                ModelState.AddModelError("", hataMesaji);
                return View(model);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var offer = await _unitOfWork.Offers.GetByIdAsync(model.Id);
                if (offer == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return NotFound("Güncellenecek teklif ana kaydı bulunamadı.");
                }

                if (!offer.IsActive)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return BadRequest("Aktif olmayan teklifler üzerinde değişiklik yapılamaz.");
                }

                offer.CustomerNumber = model.CustomerNumber;
                offer.CustomerName = model.CustomerName;
                offer.Note = model.Note;
                offer.Currency = model.Currency ?? "TRY";

                var oldLines = await _unitOfWork.OfferLines.GetQueryable()
                    .Where(x => x.OfferId == offer.Id)
                    .ToListAsync();

                var oldRepairItemIds = oldLines.Select(x => x.RepairItemId).Distinct().ToList();

                foreach (var oldLine in oldLines)
                {
                    if (oldLine.ExpertiseLineId.HasValue)
                    {
                        var expLine = await _unitOfWork.ExpertiseLines.GetByIdAsync(oldLine.ExpertiseLineId.Value);
                        if (expLine != null)
                        {
                            expLine.IsIncludedInOffer = false;
                            _unitOfWork.ExpertiseLines.Update(expLine);
                        }
                    }
                    _unitOfWork.OfferLines.Delete(oldLine);
                }

                await _unitOfWork.CompleteAsync();

                decimal totalLinesAmount = 0;
                decimal totalLaborCost = 0;
                decimal totalDiscountAmount = 0;
                decimal totalTaxAmount = 0;


                var currentRepairItemIds = model.ProductGroups.Select(g => g.RepairItemId).Distinct().ToList();

                foreach (var group in model.ProductGroups)
                {
                    if (group.Lines == null || !group.Lines.Any()) continue;

                    var repairItem = await _unitOfWork.RepairItems.GetByIdAsync(group.RepairItemId);
                    if (repairItem != null)
                    {
                        repairItem.StatusId = (int)Entities.Enum.RepairStatusEnum.TeklifGonderildi;
                        _unitOfWork.RepairItems.Update(repairItem);
                    }

                    int lineIndex = 0;
                    foreach (var line in group.Lines)
                    {
                        decimal assignedLabor = (lineIndex == 0) ? group.LaborCost : 0;

                        if (line.ExpertiseLineId.HasValue)
                        {
                            var targetExpLine = await _unitOfWork.ExpertiseLines.GetByIdAsync(line.ExpertiseLineId.Value);
                            if (targetExpLine == null)
                            {
                                await _unitOfWork.RollbackTransactionAsync();
                                ModelState.AddModelError("", $"Geçersiz Ekspertiz Referansı saptandı (ID: {line.ExpertiseLineId.Value})!");
                                return View(model);
                            }
                            targetExpLine.IsIncludedInOffer = true;
                            _unitOfWork.ExpertiseLines.Update(targetExpLine);
                        }

                        decimal lineSubTotal = (line.Quantity * line.UnitPrice) + assignedLabor;
                        decimal lineDiscount = lineSubTotal * (group.DiscountRate / 100);
                        decimal lineTaxableAmount = lineSubTotal - lineDiscount;
                        decimal lineTax = lineTaxableAmount * (group.TaxRate / 100);
                        decimal lineTotal = lineTaxableAmount + lineTax;

                        var offerLine = new OfferLine
                        {
                            OfferId = offer.Id,
                            RepairItemId = group.RepairItemId,
                            ExpertiseLineId = line.ExpertiseLineId,
                            Description = line.Description,
                            Quantity = line.Quantity,
                            Unit = line.Unit,
                            UnitPrice = line.UnitPrice,
                            LaborCost = assignedLabor,
                            TaxRate = group.TaxRate,
                            DiscountRate = group.DiscountRate,
                            Currency = offer.Currency,
                            SubTotal = lineSubTotal,
                            Total = lineTotal,
                            TechnicianNote = line.TechnicianNote ?? ""
                        };

                        totalLinesAmount += (offerLine.Quantity * offerLine.UnitPrice);
                        totalLaborCost += assignedLabor;
                        totalDiscountAmount += lineDiscount;
                        totalTaxAmount += lineTax;

                        await _unitOfWork.OfferLines.AddAsync(offerLine);
                        lineIndex++;
                    }
                }

                // Tekliften tamamen çıkartılan cihazları tespit edip durumunu boşa çıkartıyoruz
                var removedRepairItemIds = oldRepairItemIds.Except(currentRepairItemIds).ToList();
                foreach (var removedId in removedRepairItemIds)
                {
                    var removedRepairItem = await _unitOfWork.RepairItems.GetByIdAsync(removedId);
                    if (removedRepairItem != null)
                    {
                        removedRepairItem.StatusId = (int)Entities.Enum.RepairStatusEnum.TeklifHazirlaniyor;
                        _unitOfWork.RepairItems.Update(removedRepairItem);
                    }
                }

                offer.TotalLinesAmount = totalLinesAmount;
                offer.TotalLaborCost = totalLaborCost;
                offer.TotalDiscountAmount = totalDiscountAmount;
                offer.TotalTaxAmount = totalTaxAmount;
                offer.GrandTotal = (totalLinesAmount + totalLaborCost - totalDiscountAmount) + totalTaxAmount;

                _unitOfWork.Offers.Update(offer);

                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                // ========== İşlem Logu ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";

                await _logService.LogAsync(
                    action: $"{currentUserName} - Teklif Düzenleme",
                    actionType: "Edit",
                    entityName: "Offer",
                    entityId: offer.Id,
                    description: $"Teklif düzenlendi. Teklif No: {offer.OfferNumber}, Versiyon: v{offer.Version}",
                    oldValues: null,
                    newValues: new { offer.OfferNumber, offer.Version, offer.GrandTotal }
                );
                // ========== İşlem Logu Sonu ==========


                TempData["SuccessMessage"] = "Teklif başarıyla güncellendi.";
                return RedirectToAction("Index", "Offer");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                ModelState.AddModelError("", $"Güncelleme sırasında bir hata oluştu: {ex.Message}");
                return View(model);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOffer(int offerId)
        {
            var offer = await _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .FirstOrDefaultAsync(o => o.Id == offerId && o.IsActive);

            if (offer == null) return NotFound("Aktif teklif kaydı bulunamadı veya bu teklif zaten işlem görmüş.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var trZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, trZone);


                // ========== REVİZE ARŞİV: Onaylanan teklif dışındaki tüm revizeleri buluyoruz ==========
                var repairItemIdsFromOffer = offer.OfferLines.Select(l => l.RepairItemId).Distinct().ToList();

                var otherOffersQuery = _unitOfWork.Offers.GetQueryable()
                    .Include(o => o.OfferLines)
                        .ThenInclude(l => l.RepairItem)
                            .ThenInclude(r => r.AppUser)
                    .Where(o => o.Id != offer.Id);

                foreach (var repItemId in repairItemIdsFromOffer)
                {
                    otherOffersQuery = otherOffersQuery.Where(o => o.OfferLines.Any(l => l.RepairItemId == repItemId));
                }

                var otherOffers = await otherOffersQuery.ToListAsync();

                foreach (var oldOffer in otherOffers)
                {
                    var alreadyArchived = await _unitOfWork.ReviseArchives.GetQueryable()
                        .AsNoTracking()
                        .AnyAsync(r => r.OfferId == oldOffer.Id);

                    if (!alreadyArchived)
                    {
                        // ========== RepairItem bilgileri ile JSON snapshot oluşturuyoruz ==========
                        var oldOfferForJson = new
                        {
                            oldOffer.Id,
                            oldOffer.OfferNumber,
                            oldOffer.CustomerNumber,
                            oldOffer.CustomerName,
                            oldOffer.Note,
                            oldOffer.TotalLinesAmount,
                            oldOffer.TotalLaborCost,
                            oldOffer.TotalDiscountAmount,
                            oldOffer.TotalTaxAmount,
                            oldOffer.GrandTotal,
                            oldOffer.Currency,
                            oldOffer.Version,
                            oldOffer.ParentOfferId,
                            oldOffer.IsActive,
                            oldOffer.CreatedDate,
                            oldOffer.CreatedBy,
                            OfferLines = oldOffer.OfferLines.Select(line => new
                            {
                                line.Id,
                                line.OfferId,
                                line.RepairItemId,
                                line.ExpertiseLineId,
                                line.Description,
                                line.TechnicianNote,
                                line.Quantity,
                                line.Unit,
                                line.UnitPrice,
                                line.Currency,
                                line.LaborCost,
                                line.DiscountRate,
                                line.DiscountAmount,
                                line.TaxRate,
                                line.TaxAmount,
                                line.SubTotal,
                                line.Total,
                                RepairItem = line.RepairItem == null ? null : new
                                {
                                    line.RepairItem.Id,
                                    line.RepairItem.ProductName,
                                    line.RepairItem.CustomerNumber,
                                    line.RepairItem.StatusId,
                                    AppUser = line.RepairItem.AppUser == null ? null : new
                                    {
                                        line.RepairItem.AppUser.CompanyName,
                                        line.RepairItem.AppUser.FullName
                                    }
                                }
                            }).ToList()
                        };

                        var jsonSettingsArchive = new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                            Formatting = Formatting.Indented
                        };
                        string snapshotDataArchive = JsonConvert.SerializeObject(oldOfferForJson, jsonSettingsArchive);

                        var reviseArchive = new ReviseArchive
                        {
                            OfferId = oldOffer.Id,
                            OfferNumber = oldOffer.OfferNumber,
                            Version = oldOffer.Version,
                            RepairItemId = null,
                            CustomerNumber = oldOffer.CustomerNumber,
                            CustomerName = oldOffer.CustomerName,
                            RevokedAt = now,
                            RevokedBy = User.Identity?.Name ?? "Sistem",
                            Reason = $"v{offer.Version} onaylandığı için eski versiyon arşivlendi",
                            ApprovedOfferId = offer.Id,
                            ApprovedOfferNumber = offer.OfferNumber,
                            ApprovedVersion = offer.Version,
                            TotalSnapshotData = snapshotDataArchive
                        };

                        await _unitOfWork.ReviseArchives.AddAsync(reviseArchive);

                        if (oldOffer.IsActive)
                        {
                            oldOffer.IsActive = false;
                            _unitOfWork.Offers.Update(oldOffer);
                        }
                    }
                }
                // ========== REVİZE ARŞİV SONU ==========

                offer.IsActive = false;
                offer.Note += $" | [Onaylandı - {now:dd.MM.yyyy HH:mm}]";
                _unitOfWork.Offers.Update(offer);

                // OfferLines içerisindeki benzersiz cihaz (RepairItem) ID'lerini alıyoruz
                var repairItemIds = offer.OfferLines.Select(l => l.RepairItemId).Distinct().ToList();

                foreach (var itemId in repairItemIds)
                {
                    var repairItem = await _unitOfWork.RepairItems.GetByIdAsync(itemId);
                    if (repairItem != null)
                    {
                        repairItem.StatusId = (int)RepairStatusEnum.TeklifOnaylandi;

                        // Her cihazın kendi teklif satırlarındaki 'Total' değerlerinin toplamını alıyoruz
                        // Böylece KDV ve indirim dahil net rakamı bulmuş oluyoruz 
                        var deviceTotal = offer.OfferLines
                                               .Where(l => l.RepairItemId == itemId)
                                               .Sum(l => l.Total);

                        repairItem.Price = deviceTotal;
                        repairItem.Currency = offer.Currency; // Teklifteki para birimini cihaza aktarıyoruz

                        _unitOfWork.RepairItems.Update(repairItem);
                    }
                }

                // --- Archive ve Save işlemleri ---
                var jsonSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                string snapshotData = JsonConvert.SerializeObject(offer, jsonSettings);

                var archive = new OfferArchive
                {
                    OfferId = offer.Id,
                    OfferNumber = offer.OfferNumber,
                    CustomerNumber = offer.CustomerNumber,
                    ApprovedAt = now,
                    ArchivedBy = User.Identity?.Name ?? "Sistem",
                    TotalSnapshotData = snapshotData
                };
                await _unitOfWork.OfferArchives.AddAsync(archive);

                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                // ========== İşlem Logu ==========
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";

                await _logService.LogAsync(
                    action: $"{currentUserName} - Teklif Onaylama",
                    actionType: "Approve",
                    entityName: "Offer",
                    entityId: offer.Id,
                    description: $"Teklif onaylandı. Teklif No: {offer.OfferNumber}, Versiyon: v{offer.Version}, Onaylanan Tutar: {offer.GrandTotal} {offer.Currency}",
                    oldValues: new { IsActive = true },
                    newValues: new { IsActive = false, ApprovedAt = now, GrandTotal = offer.GrandTotal }
                );

                // ReviseArchive'e eklenen eski teklifleri  logluyoruz
                var archivedOldOffers = otherOffers.Where(o => !o.IsActive).ToList();
                if (archivedOldOffers.Any())
                {
                    await _logService.LogAsync(
                        action: $"{currentUserName} - Eski Revizeler Arşivlendi",
                        actionType: "ReviseArchive",
                        entityName: "Offer",
                        entityId: null,
                        description: $"v{offer.Version} onaylandığı için {archivedOldOffers.Count} adet eski revize arşivlendi. Teklif No'ları: {string.Join(", ", archivedOldOffers.Select(o => o.OfferNumber))}",
                        oldValues: null,
                        newValues: new { Count = archivedOldOffers.Count, OfferNumbers = archivedOldOffers.Select(o => o.OfferNumber) }
                    );
                }
                // ========== İşlem Logu Sonu ==========


                TempData["SuccessMessage"] = "Teklif başarıyla onaylandı ve cihaz fiyatları güncellendi!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest($"Onaylama işlemi sırasında hata oluştu: {innerMessage}");
            }
        }


        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var offer = await _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .ThenInclude(l => l.RepairItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null) return NotFound("PDF çıktısı alınacak teklif bulunamadı.");

            // 1. Firma bilgisini çekiyoruz
            string companyName = "---";
            if (!string.IsNullOrEmpty(offer.CustomerNumber))
            {
                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.CustomerNumber == offer.CustomerNumber);

                if (user != null && !string.IsNullOrEmpty(user.CompanyName))
                {
                    companyName = user.CompanyName;
                }
            }

            // 2. Dosyadan logoyu okuyup Base64'e çeviriyoruz 
            string logoBase64 = "";
            var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "firmaimages", "logo.png");

            if (System.IO.File.Exists(logoPath))
            {
                byte[] imageArray = await System.IO.File.ReadAllBytesAsync(logoPath);
                logoBase64 = $"data:image/png;base64,{Convert.ToBase64String(imageArray)}";
            }

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Teklif Formu</title><style>");

            sb.Append(@"    body { font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; color: #333; }    .header-container { width: 100%; border-bottom: 3px solid #2c3e50; padding-bottom: 15px; margin-bottom: 25px; display: block; height: 90px; }    .logo-box { float: left; width: 30%; height: 80px; }    .logo-box img { max-height: 80px; max-width: 100%; object-fit: contain; }    .title-box { float: right; width: 70%; text-align: right; margin-top: 10px; }    .title-box h2 { color: #2c3e50; margin: 0; font-size: 24px; }    .title-box p { margin: 5px 0 0; color: #7f8c8d; font-size: 14px; }    .info-table { width: 100%; border-collapse: collapse; margin-bottom: 25px; background: #f8f9fa; clear: both; }    .info-table td { padding: 8px; border: 1px solid #dee2e6; vertical-align: top; }    .items-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }    .items-table th { background-color: #34495e; color: white; padding: 10px; text-align: left; }    .items-table td { border: 1px solid #bdc3c7; padding: 8px; }    .product-title { background-color: #e9ecef; padding: 8px; margin-top: 15px; font-weight: bold; font-size: 16px; }    .total-box { float: right; width: 380px; margin-top: 20px; }    .total-table { width: 100%; border-collapse: collapse; }    .total-table td { padding: 6px; text-align: right; }    .grand-row { font-weight: bold; font-size: 18px; border-top: 2px solid #2c3e50; color: #27ae60; }    .footer { margin-top: 50px; text-align: center; font-size: 11px; color: #7f8c8d; border-top: 1px solid #dee2e6; padding-top: 15px; }    .text-center { text-align: center; }    .text-end { text-align: right; }");
            sb.Append("</style></head><body>");


            sb.Append("<div class='header-container'>");
            if (!string.IsNullOrEmpty(logoBase64))
            {
                sb.Append($"<div class='logo-box'><img src='{logoBase64}' /></div>");
            }
            else
            {
                // Eğer logo dosyası bulunamazsa tasarım kaymasın diye boş div bırakıyoruz
                sb.Append("<div class='logo-box'></div>");
            }

            sb.Append($@"
            <div class='title-box'>
                <h2>TEKNİK SERVİS TEKLİF FORMU</h2>
                <p>Teklif No: {offer.OfferNumber} / Versiyon: v{offer.Version}</p>
            </div>
        </div>");


            sb.Append($@"
        <table class='info-table'>
            <tr>
                <td style='width:50%'>
                    <strong>Firma Adı:</strong> {companyName}<br/>
                    <strong>Müşteri:</strong> {offer.CustomerName}<br/>
                    <strong>Müşteri No:</strong> {offer.CustomerNumber}
                </td>
                <td style='text-align:right;'>
                    <strong>Tarih:</strong> {offer.CreatedDate:dd.MM.yyyy}<br/>
                    <strong>Durum:</strong> {(offer.IsActive ? "Aktif Teklif" : "Onaylanmış")}
                </td>
            </tr>
        </table>");

            var groups = offer.OfferLines.GroupBy(l => l.RepairItem?.ProductName ?? "Ürün");
            foreach (var g in groups)
            {
                sb.Append($"<div class='product-title'>Ürün: {g.Key}</div>");
                sb.Append("<table class='items-table'><thead><tr><th>Açıklama / Yapılan İşlem</th><th width='10%'>Miktar</th><th width='10%'>Birim</th><th width='18%'>Birim Fiyat</th><th width='18%'>Toplam</th></tr></thead><tbody>");

                foreach (var item in g)
                {
                    string symbol = CurrencyHelper.GetSymbol(item.Currency);
                    sb.Append($@"            <tr>                <td>{item.Description}</td>                <td class='text-center'>{item.Quantity}</td>                <td class='text-center'>{item.Unit}</td>                <td class='text-end'>{item.UnitPrice:N2} {symbol}</td>                <td class='text-end'>{(item.Quantity * item.UnitPrice):N2} {symbol}</td>            </tr>");
                }
                sb.Append("</tbody></table>");
            }

            var totalsByCurrency = offer.OfferLines
                .GroupBy(l => l.Currency)
                .Select(g => new
                {
                    Currency = g.Key,
                    Symbol = CurrencyHelper.GetSymbol(g.Key),
                    TotalAmount = g.Sum(l => l.Quantity * l.UnitPrice)
                })
                .ToList();

            string totalHtml = "";
            foreach (var total in totalsByCurrency)
            {
                totalHtml += $"<tr><td>Yedek Parça Toplamı ({total.Currency}):</td><td>{total.TotalAmount:N2} {total.Symbol}</td></tr>";
            }

            string mainSymbol = CurrencyHelper.GetSymbol(offer.Currency);

            sb.Append($@"    <div class='total-box'>        <table class='total-table'>            {totalHtml}            <tr><td>Toplam İşçilik Bedeli:</td><td>{offer.TotalLaborCost:N2} {mainSymbol}</td></tr>            {(offer.TotalDiscountAmount > 0 ? $"<tr style='color:red;'><td>Toplam İndirim:</td><td>-{offer.TotalDiscountAmount:N2} {mainSymbol}</td></tr>" : "")}            <tr><td>KDV Tutarı:</td><td>{offer.TotalTaxAmount:N2} {mainSymbol}</td></tr>            <tr class='grand-row'><td>GENEL TOPLAM:</td><td>{offer.GrandTotal:N2} {mainSymbol}</td></tr>        </table>    </div>");

          

            // Şartlar ve Notlar
            if (!string.IsNullOrEmpty(offer.Note))
            {
                var cleanNote = System.Text.RegularExpressions.Regex.Replace(offer.Note, @"\s*\(v\d+\s+Revizyonu\)", "");
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\[Onaylandı.*?\]", "");
                cleanNote = cleanNote.Trim();

                if (!string.IsNullOrEmpty(cleanNote))
                {
                    sb.Append($"<div style='margin-top:60px; clear:both;'><strong>Şartlar ve Notlar:</strong><br/>{cleanNote}</div>");
                }
            }

            sb.Append("<div class='footer'>Bu teklif Teknik Servis Takip Sistemi tarafından oluşturulmuştur. Geçerlilik tarihi 30 gündür.</div>");
            sb.Append("</body></html>");

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(sb.ToString());
            var pdfBytes = await page.PdfDataAsync(new PdfOptions { Format = PaperFormat.A4, PrintBackground = true });

            return File(pdfBytes, "application/pdf", $"Teklif_{offer.OfferNumber}_v{offer.Version}.pdf");
        }


        // Teklif ürün ve kalemleri detayları
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var offer = await _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .ThenInclude(l => l.RepairItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null) return NotFound("İlgili teklif kaydı bulunamadı.");

            var archiveInfo = await _unitOfWork.OfferArchives.GetQueryable()
                .FirstOrDefaultAsync(a => a.OfferId == offer.Id);

            string companyName = "Firma Bilgisi Yok";

            if (!string.IsNullOrEmpty(offer.CustomerNumber))
            {

                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.CustomerNumber == offer.CustomerNumber);

                if (user != null)
                {
                    companyName = user.CompanyName;
                }
            }


            // Notu temizliyoruz
            string cleanNote = offer.Note;
            if (!string.IsNullOrEmpty(cleanNote))
            {
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\(v\d+\s+Revizyonu\)", "");
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\[Onaylandı.*?\]", "");
                cleanNote = cleanNote.Trim();
            }

            var viewModel = new OfferDetailsViewModel
            {
                OfferId = offer.Id,
                OfferNumber = offer.OfferNumber,
                CustomerNumber = offer.CustomerNumber,
                CustomerName = offer.CustomerName,

                CompanyName = companyName,
                Note = cleanNote,
                Version = offer.Version,
                Currency = offer.Currency,
                IsActive = offer.IsActive,
                CreatedDate = offer.CreatedDate,
                ApprovedAt = archiveInfo?.ApprovedAt,
                ApprovedBy = archiveInfo?.ArchivedBy ?? "---",
                TotalLinesAmount = offer.TotalLinesAmount,
                TotalLaborCost = offer.TotalLaborCost,
                TotalDiscountAmount = offer.TotalDiscountAmount,
                TotalTaxAmount = offer.TotalTaxAmount,
                GrandTotal = offer.GrandTotal
            };

            viewModel.Lines = offer.OfferLines.Select(line => new OfferLineDetailItem
            {
                ProductName = line.RepairItem?.ProductName ?? "Belirtilmemiş Ürün",
                Description = line.Description,
                Quantity = line.Quantity,
                Unit = line.Unit,
                UnitPrice = line.UnitPrice,
                TechnicianNote = line.TechnicianNote ?? ""
            }).ToList();

            return View(viewModel);
        }

    
        // ================= ARŞİVLENEN TEKLİFLER (SERVER-SIDE PAGINATION & FILTER) =================
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Archived(int page = 1, int pageSize = 25,string customerNo = null, string companyName = null, string productName = null)
        {

            // OfferArchive'de kaydı olan teklifleri getir (onaylananlar)
            var approvedOfferIds = await _unitOfWork.OfferArchives.GetQueryable()
                .Select(a => a.OfferId)
                .Distinct()
                .ToListAsync();

            // onaylanmış/arşivlenmiş teklifler
            var query = _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .ThenInclude(l => l.RepairItem)
                .ThenInclude(r => r.AppUser) // Firma bilgisi için
                .Where(o => approvedOfferIds.Contains(o.Id));

            // Müşteri No'ya göre filtre
            if (!string.IsNullOrEmpty(customerNo))
                query = query.Where(o => o.CustomerNumber.Contains(customerNo));

            // Firma Adı'na göre filtre (AppUser.CompanyName üzerinden)
            if (!string.IsNullOrEmpty(companyName))
                query = query.Where(o => o.OfferLines.Any(l => l.RepairItem.AppUser.CompanyName.Contains(companyName)));

            // Ürün Adı'na göre filtre
            if (!string.IsNullOrEmpty(productName))
                query = query.Where(o => o.OfferLines.Any(l => l.RepairItem.ProductName.Contains(productName)));

            // Toplam kayıt sayısı
            int totalCount = await query.CountAsync();

            // Pagination hesaplamaları
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
            int skip = (page - 1) * pageSize;

  
            var offers = await query
                .OrderByDescending(o => o.CreatedDate)
                .Skip(skip)
                .Take(pageSize)
                .Select(o => new ArchivedOfferViewModel
                {
                    Id = o.Id,
                    OfferNumber = o.OfferNumber,
                    Version = o.Version,
                    CustomerName = o.CustomerName,
                    CustomerNumber = o.CustomerNumber,
                    CompanyName = o.OfferLines.Select(l => l.RepairItem.AppUser.CompanyName).FirstOrDefault() ?? "-",
                    CreatedDate = o.CreatedDate,
                    GrandTotal = o.GrandTotal,
                    Currency = o.Currency,
                    CurrencySymbol = CurrencyHelper.GetSymbol(o.Currency)
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.CustomerNo = customerNo;
            ViewBag.CompanyName = companyName;
            ViewBag.ProductName = productName;

            return View(offers);
        }

        //========== Arşivlenmiş tekliflerin detay sayfası ================

        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> ArchivedDetails(int id)
        {
            var offer = await _unitOfWork.Offers.GetQueryable()
                .Include(o => o.OfferLines)
                .ThenInclude(l => l.RepairItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null) return NotFound("Arşivlenmiş teklif bulunamadı.");

            var archiveInfo = await _unitOfWork.OfferArchives.GetQueryable()
                .FirstOrDefaultAsync(a => a.OfferId == offer.Id);

            string companyName = "Firma Bilgisi Yok";
            if (!string.IsNullOrEmpty(offer.CustomerNumber))
            {
                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.CustomerNumber == offer.CustomerNumber);
                if (user != null)
                    companyName = user.CompanyName;
            }

            // Notu temizliyoruz
            string cleanNote = offer.Note;
            if (!string.IsNullOrEmpty(cleanNote))
            {
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\(v\d+\s+Revizyonu\)", "");
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\[Onaylandı.*?\]", "");
                cleanNote = cleanNote.Trim();
            }

            // Eğer temizlenmiş not boşsa, varsayılan mesaj göster
            if (string.IsNullOrEmpty(cleanNote))
            {
                cleanNote = "Teklif onaylanmış ve arşivlenmiştir.";
            }


            var viewModel = new OfferDetailsViewModel
            {
                OfferId = offer.Id,
                OfferNumber = offer.OfferNumber,
                CustomerNumber = offer.CustomerNumber,
                CustomerName = offer.CustomerName,
                CompanyName = companyName,
                Note = cleanNote,
                Version = offer.Version,
                Currency = offer.Currency,
                IsActive = offer.IsActive,
                CreatedDate = offer.CreatedDate,
                ApprovedAt = archiveInfo?.ApprovedAt,
                ApprovedBy = archiveInfo?.ArchivedBy ?? "---",
                TotalLinesAmount = offer.TotalLinesAmount,
                TotalLaborCost = offer.TotalLaborCost,
                TotalDiscountAmount = offer.TotalDiscountAmount,
                TotalTaxAmount = offer.TotalTaxAmount,
                GrandTotal = offer.GrandTotal,
                Lines = offer.OfferLines.Select(line => new OfferLineDetailItem
                {
                    ProductName = line.RepairItem?.ProductName ?? "Belirtilmemiş Ürün",
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    UnitPrice = line.UnitPrice,
                    LaborCost = line.LaborCost,
                    DiscountRate = line.DiscountRate,
                    DiscountAmount = line.DiscountAmount,
                    TaxRate = line.TaxRate,
                    TaxAmount = line.TaxAmount,
                    Total = line.Total,
                    TechnicianNote = line.TechnicianNote ?? ""
                }).ToList()
            };

            return View(viewModel);
        }


        // ================= REVİZE ARŞİV LİSTESİ (SERVER-SIDE PAGINATION & FILTER) =================
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> ReviseArchives(int page = 1, int pageSize = 25,
         string customerNo = null, string productName = null, string companyName = null)
        {
            IQueryable<ReviseArchive> query = _unitOfWork.ReviseArchives.GetQueryable()
                .Include(r => r.RepairItem)
                    .ThenInclude(r => r.AppUser);

            // Filtreler
            if (!string.IsNullOrEmpty(customerNo))
                query = query.Where(r => r.CustomerNumber != null && r.CustomerNumber.Contains(customerNo));

            if (!string.IsNullOrEmpty(productName))
                query = query.Where(r => r.RepairItem != null && r.RepairItem.ProductName != null && r.RepairItem.ProductName.Contains(productName));

            if (!string.IsNullOrEmpty(companyName))
                query = query.Where(r => (r.RepairItem != null && r.RepairItem.AppUser != null && r.RepairItem.AppUser.CompanyName != null && r.RepairItem.AppUser.CompanyName.Contains(companyName))
                                    || (r.CustomerNumber != null && _userManager.Users.Any(u => u.CustomerNumber == r.CustomerNumber && u.CompanyName != null && u.CompanyName.Contains(companyName))));

            int totalCount = await query.CountAsync();

            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));
            int skip = (page - 1) * pageSize;

            var pagedQuery = query
                .OrderByDescending(r => r.RevokedAt)
                .Skip(skip)
                .Take(pageSize);

            var archives = new List<ReviseArchiveViewModel>();
            var items = await pagedQuery.ToListAsync();

            foreach (var r in items)
            {
                string companyNameValue = "-";

            
                if (r.RepairItem?.AppUser?.CompanyName != null)
                {
                    companyNameValue = r.RepairItem.AppUser.CompanyName;
                }
           
                else if (!string.IsNullOrEmpty(r.CustomerNumber))
                {
                    var user = await _userManager.Users
                        .FirstOrDefaultAsync(u => u.CustomerNumber == r.CustomerNumber);
                    if (user != null && !string.IsNullOrEmpty(user.CompanyName))
                    {
                        companyNameValue = user.CompanyName;
                    }
                }

                archives.Add(new ReviseArchiveViewModel
                {
                    Id = r.Id,
                    OfferId = r.OfferId,
                    OfferNumber = r.OfferNumber ?? "-",
                    Version = r.Version,
                    RepairItemId = r.RepairItemId,
                    ProductName = r.RepairItem?.ProductName ?? "-",
                    CustomerNumber = r.CustomerNumber ?? "-",
                    CustomerName = r.CustomerName ?? "-",
                    CompanyName = companyNameValue,
                    RevokedAt = r.RevokedAt,
                    RevokedBy = r.RevokedBy ?? "-",
                    Reason = r.Reason ?? "-",
                    ApprovedOfferId = r.ApprovedOfferId,
                    ApprovedOfferNumber = r.ApprovedOfferNumber ?? "-",
                    ApprovedVersion = r.ApprovedVersion,
                    CurrencySymbol = CurrencyHelper.GetSymbol("TRY"),
                    GrandTotal = 0
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.CustomerNo = customerNo;
            ViewBag.ProductName = productName;
            ViewBag.CompanyName = companyName;
            ViewBag.PageSizeOptions = new List<int> { 10, 25, 50, 100 };

            return View(archives);
        }


        // ========== REVİZE ARŞİV DETAY SAYFASI ==========
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> ReviseArchiveDetails(int id)
        {
            var reviseArchive = await _unitOfWork.ReviseArchives.GetQueryable()
                .Include(r => r.RepairItem)
                    .ThenInclude(r => r.AppUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reviseArchive == null) return NotFound("Arşivlenmiş revize teklif bulunamadı.");

            // JSON'dan Offer objesini geri oluşturuyoruz
            var offer = JsonConvert.DeserializeObject<Offer>(reviseArchive.TotalSnapshotData, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            if (offer == null) return NotFound("Teklif verisi bozuk.");

            // CompanyName
            string companyName = "Firma Bilgisi Yok";
            if (reviseArchive.RepairItem?.AppUser != null)
            {
                companyName = reviseArchive.RepairItem.AppUser.CompanyName ?? "Firma Bilgisi Yok";
            }
            else if (!string.IsNullOrEmpty(reviseArchive.CustomerNumber))
            {
                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.CustomerNumber == reviseArchive.CustomerNumber);
                if (user != null && !string.IsNullOrEmpty(user.CompanyName))
                {
                    companyName = user.CompanyName;
                }
            }

            // Notu temizle
            string cleanNote = offer.Note;
            if (!string.IsNullOrEmpty(cleanNote))
            {
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\(v\d+\s+Revizyonu\)", "");
                cleanNote = System.Text.RegularExpressions.Regex.Replace(cleanNote, @"\s*\[Onaylandı.*?\]", "");
                cleanNote = cleanNote.Trim();
            }

            if (string.IsNullOrEmpty(cleanNote))
            {
                cleanNote = reviseArchive.Reason ?? "Revize edildiği için arşivlenmiştir.";
            }

 
            var viewModel = new OfferDetailsViewModel
            {
                OfferId = offer.Id,
                OfferNumber = offer.OfferNumber ?? reviseArchive.OfferNumber,
                CustomerNumber = offer.CustomerNumber ?? reviseArchive.CustomerNumber,
                CustomerName = offer.CustomerName ?? reviseArchive.CustomerName,
                CompanyName = companyName,
                Note = cleanNote,
                Version = offer.Version,
                Currency = offer.Currency ?? "TRY",
                IsActive = false,
                CreatedDate = offer.CreatedDate,
                ApprovedAt = reviseArchive.RevokedAt,
                ApprovedBy = reviseArchive.RevokedBy,
                TotalLinesAmount = offer.TotalLinesAmount,
                TotalLaborCost = offer.TotalLaborCost,
                TotalDiscountAmount = offer.TotalDiscountAmount,
                TotalTaxAmount = offer.TotalTaxAmount,
                GrandTotal = offer.GrandTotal,
                
                Lines = offer.OfferLines?.Select(line => new OfferLineDetailItem
                {
                    ProductName = line.RepairItem?.ProductName ?? "Ürün",  
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    UnitPrice = line.UnitPrice,
                    LaborCost = line.LaborCost,
                    DiscountRate = line.DiscountRate,
                    DiscountAmount = line.DiscountAmount,
                    TaxRate = line.TaxRate,
                    TaxAmount = line.TaxAmount,
                    Total = line.Total,
                    TechnicianNote = line.TechnicianNote ?? ""
                }).ToList() ?? new List<OfferLineDetailItem>()
            };

            return View(viewModel);
        }

    }
}