using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class MailSetting
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SMTP Sunucu zorunludur!")]
        [Display(Name = "SMTP Sunucu")]
        public string SmtpServer { get; set; }

        [Required(ErrorMessage = "Port zorunludur!")]
        [Display(Name = "Port")]
        public int Port { get; set; }

        [Required(ErrorMessage = "Gönderici E-posta zorunludur!")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz!")]
        [Display(Name = "Gönderici E-posta")]
        public string SenderEmail { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur!")]
        [Display(Name = "Şifre")]
        public string SenderPassword { get; set; }

        [Display(Name = "SSL Kullan")]
        public bool UseSSL { get; set; } = true;

        [Display(Name = "Aktif")]
        public bool IsActive { get; set; } = true;
    }
}