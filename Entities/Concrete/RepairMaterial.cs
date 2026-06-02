using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class RepairMaterial
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RepairId { get; set; }

      
        public int? ProductId { get; set; }

        [StringLength(200)]
        public string? ExternalProductName { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Miktar 1'den küçük olamaz.")]
        public int Quantity { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string MaterialType { get; set; } = "Stock"; // Stock veya External

        public DateTime UsedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? UsedBy { get; set; }

        // Navigation properties
        [ForeignKey("RepairId")]
        public virtual RepairItem? Repair { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
