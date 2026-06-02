using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.Concrete
{
    public class ArchiveRepair
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Müşteri Bilgileri
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string? CompanyName { get; set; }
        public string? AppUserId { get; set; }
        [ForeignKey("AppUserId")]
        public virtual AppUser? AppUser { get; set; }

        // Tamir Bilgileri
        public string TrackingCode { get; set; }
        public string ProductName { get; set; }
        public string? ProductBrand { get; set; }
        public string? ProductModel { get; set; }
        public string? SerialNumber { get; set; }
        public string ProblemDescription { get; set; }
        public string? InternalNote { get; set; }

        // Tarihler
        public DateTime ReceivedDate { get; set; }
        public DateTime? DeliveryDate { get; set; }

        // Personel
        public int? PersonelId { get; set; }
        [ForeignKey("PersonelId")]
        public virtual Personel? Personel { get; set; }

        // Ücret
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? Currency { get; set; } // TRY, USD, EUR, GBP

        // Arşiv Tarihi (ne zaman arşivlendi)
        public DateTime ArchivedAt { get; set; } = DateTime.Now;

        // Orijinal RepairItem Id (hangi tamirden geldi)
        public int OriginalRepairId { get; set; }
    }
}