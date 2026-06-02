using System.ComponentModel.DataAnnotations;

namespace TeknikServisTakip.Models.ViewModels
{
    public class ResetUserPasswordViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Roles { get; set; }

        [Required(ErrorMessage = "Yeni şifre zorunludur!")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır!")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Şifre tekrarı zorunludur!")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Şifreler eşleşmiyor!")]
        public string ConfirmPassword { get; set; }
    }
}
