using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class OfferLine
    {
        public int Id { get; set; }
        public int OfferId { get; set; }
        public int RepairItemId { get; set; }        // Kalemin ait olduğu cihaz ID'si
        public int? ExpertiseLineId { get; set; }     // Personelin yazdığı ekspertiz satır ID'si

        public string Description { get; set; }       // Yapılan işlem / Parça adı
        public string TechnicianNote { get; set; }   // Teknisyen notu
        public int Quantity { get; set; }
        public string Unit { get; set; } = "Adet";
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "TRY";

        // Senin istediğin: Her ürün/kalem bazlı esnek maliyet yönetimi
        public decimal LaborCost { get; set; }        // Bu kaleme ait işçilik
        public decimal DiscountRate { get; set; }     // Bu kaleme ait iskonto %
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; } = 20;    // Bu kaleme ait KDV %
        public decimal TaxAmount { get; set; }

        public decimal SubTotal { get; set; }         // (Miktar * BirimFiyat) + İşçilik
        public decimal Total { get; set; }            // İskonto düşülmüş, KDV eklenmiş Satır Net Toplamı

        [ForeignKey("OfferId")]
        public virtual Offer Offer { get; set; }

        [ForeignKey("RepairItemId")]
        public virtual RepairItem RepairItem { get; set; }

        [ForeignKey("ExpertiseLineId")]
        public virtual ExpertiseLine ExpertiseLine { get; set; }
    }
}
