using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Entities.Concrete
{
    public class RepairImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RepairItemId { get; set; }

        [Required]
        public string ImagePath { get; set; }

        [Required]
        public string ImageType { get; set; } // "Before" veya "After"

        public int Order { get; set; } // Sıralama için (1-5)

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("RepairItemId")]
        [JsonIgnore]
        public virtual RepairItem RepairItem { get; set; }
    }
}