using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class ExpertiseApprovalController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogService _logService;
        private readonly UserManager<AppUser> _userManager;

        public ExpertiseApprovalController(IUnitOfWork unitOfWork, ILogService logService, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _logService = logService;
            _userManager = userManager;
        }

        // 1. ONAY BEKLEYEN EKSPERTİZ HAVUZU (Sadece İnceleme ve Seçim Alanı)
        [HttpGet]
        public async Task<IActionResult> Index(string search = "", int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;

            // 1. Temel Sorgu (Filtreler hazır)
            var query = _unitOfWork.ExpertiseLines
                .GetQueryable()
                .Include(e => e.RepairItem)
                .ThenInclude(r => r.AppUser)
                .Where(e => !e.IsIncludedInOffer && !e.IsApproved);

            // 2. Arama Filtresi (SQL seviyesinde filtreleniyor)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(e =>
                    (e.RepairItem.CustomerNumber != null && e.RepairItem.CustomerNumber.Contains(search)) ||
                    (e.RepairItem.AppUser != null && e.RepairItem.AppUser.CompanyName != null && e.RepairItem.AppUser.CompanyName.Contains(search)) ||
                    (e.RepairItem.ProductName != null && e.RepairItem.ProductName.Contains(search))
                );
            }

            // Önce Benzersiz Grupları (Müşteri No + Ürün Adı) Çıkarıyoruz
            var groupQuery = query
                .Select(e => new { e.RepairItem.CustomerNumber, e.RepairItem.ProductName })
                .Distinct();

            // Sayfalama  satır sayısına göre değil, "Grup Sayısına" göre yapıyoruz
            var totalGroupsCount = await groupQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalGroupsCount / (double)pageSize);

            // bu sayfaya ait olan 10 grubu SQL'den Skip-Take ile çekiyoruz
            var pagedGroups = await groupQuery
                .OrderBy(g => g.CustomerNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Bu 10 grubun içereceği tüm kalemleri çekmek için filtre oluşturuyoruz
            var groupedPagedItems = new List<(string CustomerName, string CompanyName, string CustomerNumber, string ProductName, List<ExpertiseLine> Items)>();

            if (pagedGroups.Any())
            {
                // Bu sayfadaki gruplara ait olan tüm ekspertiz satırlarını SQL'den tek seferde çekiyoruz
                var allLines = await query.ToListAsync();

                // Çektiğimiz satırları, bu sayfanın gruplarıyla eşleştirip Tuple listemize dolduruyoruz
                foreach (var groupKey in pagedGroups)
                {
                    var groupLines = allLines
                        .Where(e => e.RepairItem.CustomerNumber == groupKey.CustomerNumber && e.RepairItem.ProductName == groupKey.ProductName)
                        .OrderBy(e => e.CreatedAt)
                        .ToList();

                    if (groupLines.Any())
                    {
                        var firstLine = groupLines.First();
                        groupedPagedItems.Add((
                            CustomerName: firstLine.RepairItem.AppUser?.FullName ?? "-",
                            CompanyName: firstLine.RepairItem.AppUser?.CompanyName ?? "-",
                            CustomerNumber: groupKey.CustomerNumber,
                            ProductName: groupKey.ProductName,
                            Items: groupLines 
                        ));
                    }
                }
            }


            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalGroupsCount; // Ekranda kaç grup listelendiği bilgisi

            return View(groupedPagedItems);
        }




        // 2. SEÇİLEN KALEMLERİ FİYATLANDIRMAYA (OFFER/CREATE) GÖNDERİYORUZ
        [HttpPost]
        public async Task<IActionResult> ForwardToPricing([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "Lütfen fiyatlandırılacak en az bir ekspertiz kalemi seçin!" });

     
            TempData["SelectedExpertiseIds"] = string.Join(",", ids);

            // ========== İşlem Logu ==========
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserName = currentUser?.FullName ?? currentUser?.Email ?? "Bilinmeyen Kullanıcı";

            // Seçilen kalemlerin detaylarını al
            var selectedLines = await _unitOfWork.ExpertiseLines.GetQueryable()
                .Include(e => e.RepairItem)
                .Where(e => ids.Contains(e.Id))
                .ToListAsync();

            var selectedLineDetails = string.Join(", ", selectedLines.Select(l => $"{l.RepairItem?.ProductName} - {l.Description} (ID:{l.Id})"));

            await _logService.LogAsync(
                action: $"{currentUserName} - Ekspertiz Kalemleri Fiyatlandırmaya Gönderdi",
                actionType: "ForwardToPricing",
                entityName: "ExpertiseLine",
                entityId: null,
                description: $"{selectedLines.Count} adet ekspertiz kalemi fiyatlandırmaya gönderildi. Kalemler: {selectedLineDetails}",
                oldValues: null,
                newValues: new { Ids = ids, Count = selectedLines.Count }
            );
            // ========== İşlem Logu Sonu ==========

            return Json(new { success = true, redirectUrl = "/Offer/Create" });
        }
    }
}