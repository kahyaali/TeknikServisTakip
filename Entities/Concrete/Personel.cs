using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.Concrete
{
    public class Personel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "AppUserId zorunludur!")]
        public string AppUserId { get; set; }

        [Required(ErrorMessage = "Ad Soyad zorunludur!")]
        [MinLength(3, ErrorMessage = "Ad Soyad en az 3 karakter olmalıdır!")]
        [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir!")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Pozisyon seçilmelidir!")]
        [Display(Name = "Pozisyon")]
        public int? PositionId { get; set; }

        [Display(Name = "Departman")]
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Telefon numarası zorunludur!")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz!")]
        [RegularExpression(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$", ErrorMessage = "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 05XX XXX XX XX)")]
        [Display(Name = "Telefon")]
        public string PhoneNumber { get; set; }

        [MaxLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir!")]
        [Display(Name = "Adres")]
        public string? Address { get; set; }

        [Display(Name = "Şehir")]
        public string? City { get; set; }

        [Display(Name = "İlçe")]
        public string? District { get; set; }

        [Required(ErrorMessage = "E-posta adresi zorunludur!")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz!")]
        [Display(Name = "E-posta")]
        public string? Email { get; set; }

        [Display(Name = "Durum")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Kayıt Tarihi")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("AppUserId")]
        public virtual AppUser AppUser { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department Department { get; set; }

        [ForeignKey("PositionId")]
        public virtual Position Position { get; set; }

        public virtual ICollection<RepairItem> Repairs { get; set; } = new List<RepairItem>();
    }
}