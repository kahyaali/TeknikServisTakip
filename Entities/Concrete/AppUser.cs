using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class AppUser : IdentityUser
    {
        [Required(ErrorMessage = "Ad Soyad zorunludur!")]
        [MinLength(3, ErrorMessage = "Ad Soyad en az 3 karakter olmalıdır!")]
        [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir!")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; }

        [Display(Name = "Müşteri No")]
        public string? CustomerNumber { get; set; }


        [Display(Name = "Cari No")]
        //[RegularExpression(@"^[A-Za-z0-9-]{3,20}$", ErrorMessage = "Cari No en az 3, en fazla 20 karakter olabilir!")]
        //[RegularExpression(@"^[A-Za-z0-9ğüşıöçĞÜŞİÖÇ-]{3,20}$", ErrorMessage = "Cari No en az 3, en fazla 20 karakter olabilir!")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Cari No boşluk içeremez!")]
        public string? CariNo { get; set; }

        [Display(Name = "Firma Adı")]
        [MinLength(2, ErrorMessage = "Firma adı en az 2 karakter olmalıdır!")]
        [MaxLength(200, ErrorMessage = "Firma adı en fazla 200 karakter olabilir!")]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Telefon numarası zorunludur!")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz! (Örn: 05XX XXX XX XX)")]
        [RegularExpression(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$", ErrorMessage = "Geçerli bir Türkiye telefon numarası giriniz! (Örn: 0532 123 4567)")]
        [Display(Name = "Telefon")]
        public override string? PhoneNumber { get; set; }

      
        [MinLength(10, ErrorMessage = "Adres en az 10 karakter olmalıdır!")]
        [MaxLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir!")]
        [Display(Name = "Adres")]
        public string? Address { get; set; }

     
        [MinLength(2, ErrorMessage = "Şehir en az 2 karakter olmalıdır!")]
        [Display(Name = "Şehir")]
        public string? City { get; set; }

        [MinLength(2, ErrorMessage = "İlçe en az 2 karakter olmalıdır!")]
        [Display(Name = "İlçe")]
        public string? District { get; set; }

        [Display(Name = "Posta Kodu")]
        [RegularExpression(@"^\d{5}$", ErrorMessage = "Posta kodu 5 haneli sayı olmalıdır!")]
        public string? PostalCode { get; set; }

        [Display(Name = "TC Kimlik No")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "TC Kimlik No 11 haneli sayı olmalıdır!")]
        public string? IdentityNumber { get; set; }

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Kayıt Tarihi")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Sistem Admini")]
        public bool IsSystemAdmin { get; set; } = false;
    }
}