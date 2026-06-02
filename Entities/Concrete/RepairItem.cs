using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Entities.Concrete
{
    public class RepairItem
    {
        public int Id { get; set; }

        public string? CustomerNumber { get; set; }
        public string? AppUserId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductBrand { get; set; }
        public string? ProductModel { get; set; }
        public string? SerialNumber { get; set; }
        public string? ProblemDescription { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
        public DateTime? DeliveryDate { get; set; }


        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
       
        public string? Currency { get; set; } // "TRY", "USD", "EUR" 

        public int? StatusId { get; set; }
        public int? PersonelId { get; set; }
        public string? BeforeImagePath { get; set; }
        public string? AfterImagePath { get; set; }
        public string? CustomerNote { get; set; }
        public string? InternalNote { get; set; }
        public bool IsDeleted { get; set; } = false;

        public string? TrackingCode { get; set; }  // Benzersiz takip kodu
        public string? QrCodePath { get; set; }    // Karekod resim yolu

        // Navigation properties 
        [JsonIgnore]
        [ForeignKey("AppUserId")]
        public virtual AppUser? AppUser { get; set; }

      
        [JsonIgnore]
        [ForeignKey("PersonelId")]
        public virtual Personel? Personel { get; set; }

        [JsonIgnore]
        public virtual ICollection<Material>? Materials { get; set; }
        public virtual ICollection<RepairImage> RepairImages { get; set; } = new List<RepairImage>();


    }
}