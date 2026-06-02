using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Entities.Concrete
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ürün kodu zorunludur.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Ürün kodu 3-50 karakter arasında olmalıdır.")]
        public string ProductCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ürün adı zorunludur.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Ürün adı 2-200 karakter arasında olmalıdır.")]
        public string ProductName { get; set; } = string.Empty;


        [StringLength(100, ErrorMessage = "Marka en fazla 100 karakter olabilir.")]
        public string? Brand { get; set; }

        [StringLength(100, ErrorMessage = "Model en fazla 100 karakter olabilir.")]
        public string? Model { get; set; }

        [StringLength(100, ErrorMessage = "Seri No en fazla 100 karakter olabilir.")]
        public string? SerialNo { get; set; }

        [StringLength(50, ErrorMessage = "IMEI No en fazla 50 karakter olabilir.")]
        [RegularExpression(@"^\d{15}$", ErrorMessage = "IMEI No 15 haneli sayı olmalıdır.")]
        public string? IMEINo { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Miktar 0'dan küçük olamaz.")]
        public int Quantity { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Minimum stok seviyesi 0'dan küçük olamaz.")]
        public int MinStockLevel { get; set; } = 5;

        [Range(0, int.MaxValue, ErrorMessage = "Maximum stok seviyesi 0'dan küçük olamaz.")]
        public int MaxStockLevel { get; set; } = 100;

        [Required(ErrorMessage = "Birim zorunludur.")]
        [StringLength(20, ErrorMessage = "Birim en fazla 20 karakter olabilir.")]
        public string Unit { get; set; } = "Adet";

        [StringLength(50, ErrorMessage = "Lokasyon en fazla 50 karakter olabilir.")]
        public string? Location { get; set; }

        [StringLength(200, ErrorMessage = "Tedarikçi en fazla 200 karakter olabilir.")]
        public string? Supplier { get; set; }

        [DataType(DataType.Currency)]
        [Range(0, 999999.99, ErrorMessage = "Alış fiyatı 0-999999.99 arasında olmalıdır.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PurchasePrice { get; set; }

        [DataType(DataType.Currency)]
        [Range(0, 999999.99, ErrorMessage = "Satış fiyatı 0-999999.99 arasında olmalıdır.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalePrice { get; set; }

        [Display(Name = "Para Birimi")]
        [StringLength(3, MinimumLength = 3, ErrorMessage = "Para birimi 3 karakter olmalıdır (TRY, USD, EUR, GBP).")]
        public string? Currency { get; set; } = "TRY";

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Description { get; set; }

        [StringLength(1000, ErrorMessage = "Notlar en fazla 1000 karakter olabilir.")]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [DataType(DataType.DateTime)]
        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }
        public int? CategoryId { get; set; }

        // Navigation properties

        [ValidateNever] // Form doğrulamasına bunu dahil etme 
        public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

        [ValidateNever] // Form doğrulamasına bunu dahil etme 
        public virtual ICollection<StockAlert> StockAlerts { get; set; } = new List<StockAlert>();

        [ValidateNever] // Form doğrulamasına bunu dahil etme 
        public virtual Category? Category { get; set; }
    }
}
