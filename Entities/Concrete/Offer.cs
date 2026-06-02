using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class Offer
    {
        public int Id { get; set; }
        public string OfferNumber { get; set; }     // TEK-20260520-001
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string Note { get; set; }

        // Tüm satırların kümülatif toplamları
        public decimal TotalLinesAmount { get; set; } // Parça Toplamları
        public decimal TotalLaborCost { get; set; }   // Tüm cihazların toplam işçiliği
        public decimal TotalDiscountAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal GrandTotal { get; set; }       // Müşterinin ödeyeceği son net rakam
        public string Currency { get; set; }          // Teklifin ana para birimi (Arayüzden seçilen)

        public int Version { get; set; } = 1;
        public int? ParentOfferId { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; }

       
        // Bir teklif birden fazla cihaz satırı barındırabilir!
        public virtual ICollection<OfferLine> OfferLines { get; set; } = new List<OfferLine>();
    }
}

