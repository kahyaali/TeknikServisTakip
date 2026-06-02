using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class ProductTrackingLog
    {
        [Key]
        public int Id { get; set; }

        // Ürün bilgileri
        public int RepairItemId { get; set; }
        public string TrackingCode { get; set; }
        public string ProductName { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerEmail { get; set; }

        // Durum değişikliği
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }

        // İşlem bilgileri
        public string Action { get; set; }  // Created, Assigned, StatusChanged, Delivered durumları için
        public string PerformedBy { get; set; }  // Kim yaptı (Admin/Personel adı) logları için
        public string PerformedById { get; set; }

        // Açıklama
        public string Description { get; set; }

        // Zaman
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual RepairItem RepairItem { get; set; }
    }
}