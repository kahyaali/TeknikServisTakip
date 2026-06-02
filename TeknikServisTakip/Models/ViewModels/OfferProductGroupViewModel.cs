using System.ComponentModel.DataAnnotations;

namespace TeknikServisTakip.Models.ViewModels
{
     public class OfferFormViewModel
        {
        public int Id { get; set; }
      
        public string CustomerNumber { get; set; }
  
        public string CustomerName { get; set; }
        public string CompanyName { get; set; } // Firma / Şirket Adı Ünvanı
        public string Note { get; set; }
       public int? ParentOfferId { get; set; }
    
        public string Currency { get; set; } = "TRY";

            // Ekrandaki her bir ürün grubu
            public List<ProductGroupItemViewModel> ProductGroups { get; set; } = new List<ProductGroupItemViewModel>();
        }

        public class ProductGroupItemViewModel
        {
            public int RepairItemId { get; set; }
        [Required]
        public string ProductName { get; set; }
     
        public decimal LaborCost { get; set; }

    
        public decimal DiscountRate { get; set; }
    
        public decimal TaxRate { get; set; } = 20;
        public bool IsGroupDeleted { get; set; } = false;

        // Ürünün altındaki ekspertiz kalemleri
        public List<ProductLineItemViewModel> Lines { get; set; } = new List<ProductLineItemViewModel>();
        }

        public class ProductLineItemViewModel
        {
            public int? ExpertiseLineId { get; set; }
            public string Description { get; set; }

        public string TechnicianNote { get; set; }

        public int Quantity { get; set; }
            public string Unit { get; set; } = "Adet";
     
        public decimal UnitPrice { get; set; }
            public string Currency { get; set; } = "TRY";
        }
    
}
