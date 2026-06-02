using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.Concrete
{
    public class Delivery
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tamir kaydı zorunludur!")]
        public int RepairItemId { get; set; }

        [Required(ErrorMessage = "Müşteri zorunludur!")]
        public string CustomerId { get; set; }

        [Required(ErrorMessage = "Teslim tipi zorunludur!")]
        public string DeliveryType { get; set; } // "Cargo" veya "InPerson"

        // Kargo için
        [Display(Name = "Kargo Firması")]
        public string? CargoCompany { get; set; }

        [Display(Name = "Kargo Takip No")]
        public string? CargoTrackingNumber { get; set; }

        [Display(Name = "Alıcı Ad Soyad")]
        public string? RecipientName { get; set; }

        [Display(Name = "Alıcı Telefon")]
        [RegularExpression(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$", ErrorMessage = "Geçerli bir telefon numarası giriniz! (Örn: 05XX XXX XX XX)")]
        public string? RecipientPhone { get; set; }

        // Elden teslim için
        [Display(Name = "Teslim Alan Ad Soyad")]
        public string? ReceiverName { get; set; }

        [Display(Name = "Teslim Alan Telefon")]
        [RegularExpression(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$", ErrorMessage = "Geçerli bir telefon numarası giriniz! (Örn: 05XX XXX XX XX)")]
        public string? ReceiverPhone { get; set; }

        [Required(ErrorMessage = "Teslim tarihi zorunludur!")]
        [Display(Name = "Teslim Tarihi")]
        public DateTime DeliveryDate { get; set; } = DateTime.Now;

        [Display(Name = "Teslim Eden")]
        public string? DeliveredBy { get; set; }

        [Display(Name = "Not")]
        public string? Note { get; set; }

        [ForeignKey("RepairItemId")]
        public virtual RepairItem RepairItem { get; set; }

        [ForeignKey("CustomerId")]
        public virtual AppUser Customer { get; set; }
    }
}