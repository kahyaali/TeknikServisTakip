namespace TeknikServisTakip.Models.ViewModels
{
    public class OfferDetailsViewModel
    {
        public int OfferId { get; set; }
        public string OfferNumber { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string CompanyName { get; set; }
        public string Note { get; set; }
        public int Version { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }

        // Tarih Alanları
        public DateTime CreatedDate { get; set; } // Teklif Gönderilme Tarihi
        public DateTime? ApprovedAt { get; set; } // Arşivden Gelecek Onay Tarihi
        public string ApprovedBy { get; set; } // Onaylayan Personel

        // Finansal Özet Alanları
        public decimal TotalLinesAmount { get; set; }
        public decimal TotalLaborCost { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal GrandTotal { get; set; }

        // Teklif Kalemleri
        public List<OfferLineDetailItem> Lines { get; set; } = new List<OfferLineDetailItem>();
    }

    public class OfferLineDetailItem
    {
        public string ProductName { get; set; } // Ürün/Cihaz Adı
        public string Description { get; set; } // Yapılan İşlem / Parça
        public string TechnicianNote { get; set; } // Teknisyen notu
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LaborCost { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
    }
}
