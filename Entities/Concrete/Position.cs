using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class Position
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Pozisyon adı zorunludur!")]
        [MaxLength(100, ErrorMessage = "Pozisyon adı en fazla 100 karakter olabilir!")]
        [Display(Name = "Pozisyon Adı")]
        public string Name { get; set; }

        [Display(Name = "Açıklama")]
        [MaxLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir!")]
        public string Description { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual ICollection<Personel> Personels { get; set; } = new List<Personel>();
    }
}