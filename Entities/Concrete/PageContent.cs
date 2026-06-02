using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class PageContent
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Sayfa adı zorunludur!")]
        public string PageName { get; set; } // About, Contact

        [Required(ErrorMessage = "Başlık zorunludur!")]
        public string Title { get; set; }

        [Required(ErrorMessage = "İçerik zorunludur!")]
        public string Content { get; set; }

        public string? ImageUrl { get; set; }
        public string? SliderImages { get; set; }
        public string? MapUrl { get; set; } // İletişim için harita

        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz!")]
        [RegularExpression(@"^0[0-9]{2} [0-9]{3} [0-9]{2} [0-9]{2}$|^[0-9]{10}$|^05[0-9]{9}$",
           ErrorMessage = "Telefon formatı: 05XX XXX XX XX")]
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}