using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class StockAlert
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ürün ID zorunludur.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Uyarı tipi zorunludur.")]
        [StringLength(20, ErrorMessage = "Uyarı tipi en fazla 20 karakter olabilir.")]
        [RegularExpression(@"^(LOW_STOCK|HIGH_STOCK|CRITICAL)$", ErrorMessage = "Geçersiz uyarı tipi.")]
        public string AlertType { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "Eski miktar 0'dan küçük olamaz.")]
        public int? OldQuantity { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Yeni miktar 0'dan küçük olamaz.")]
        public int? NewQuantity { get; set; }

        public bool IsSent { get; set; } = false;

        [DataType(DataType.DateTime)]
        public DateTime? SentAt { get; set; }

        [StringLength(500, ErrorMessage = "Notlar en fazla 500 karakter olabilir.")]
        public string? Notes { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
