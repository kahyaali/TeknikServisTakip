using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.Concrete
{
    public class Reference
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Müşteri adı zorunludur")]
        [StringLength(100, ErrorMessage = "Müşteri adı en fazla 100 karakter olabilir")]
        public string CustomerName { get; set; }

        [StringLength(100, ErrorMessage = "Ünvan en fazla 100 karakter olabilir")]
        public string? Title { get; set; } 

     
        public string? Comment { get; set; }

        [StringLength(200, ErrorMessage = "Resim yolu en fazla 200 karakter olabilir")]
        public string? ImagePath { get; set; }

        public int Order { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}