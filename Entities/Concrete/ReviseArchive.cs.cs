using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class ReviseArchive
    {
        public int Id { get; set; }
        public int OfferId { get; set; }              // Eski teklifin ID'si
        public string OfferNumber { get; set; }       // Teklif numarası (TKF-20260520-001)
        public int Version { get; set; }              // Versiyonu (v1, v2, v3...)
        public int? RepairItemId { get; set; }         // Hangi ürüne ait olduğu
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public DateTime RevokedAt { get; set; }       // Ne zaman arşivlendiği
        public string RevokedBy { get; set; }         // Hangi personel tarafından (onaylayan)
        public string Reason { get; set; }            // İptal/Arşiv sebebi (opsiyonel)

        // Onaylanan yeni versiyon bilgisi
        public int ApprovedOfferId { get; set; }      // Hangi teklif onaylandığı için bu arşivlendi
        public string ApprovedOfferNumber { get; set; }
        public int ApprovedVersion { get; set; }

        // Tüm teklifin JSON snapshot'ı (onay anındaki hali)
        public string TotalSnapshotData { get; set; }

        [ForeignKey("OfferId")]
        public virtual Offer Offer { get; set; }

        [ForeignKey("RepairItemId")]
        public virtual RepairItem RepairItem { get; set; }
    }
}
