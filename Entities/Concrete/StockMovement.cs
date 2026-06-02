using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class StockMovement
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ürün ID zorunludur.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "İşlem tipi zorunludur.")]
        [StringLength(20, ErrorMessage = "İşlem tipi en fazla 20 karakter olabilir.")]
        [RegularExpression(@"^(IN|OUT|ADJUST_IN|ADJUST_OUT)$", ErrorMessage = "Geçersiz işlem tipi.")]
        public string MovementType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Miktar zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Miktar 1'den küçük olamaz.")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Önceki stok zorunludur.")]
        [Range(0, int.MaxValue, ErrorMessage = "Önceki stok 0'dan küçük olamaz.")]
        public int PreviousStock { get; set; }

        [Required(ErrorMessage = "Yeni stok zorunludur.")]
        [Range(0, int.MaxValue, ErrorMessage = "Yeni stok 0'dan küçük olamaz.")]
        public int NewStock { get; set; }

        [StringLength(100, ErrorMessage = "Referans No en fazla 100 karakter olabilir.")]
        public string? ReferenceNo { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Description { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        // Navigation property
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
