using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class ErrorLog
    {
        [Key]
        public int Id { get; set; }

        // Hata bilgileri
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
        public string InnerException { get; set; }

        // Kaynak bilgileri
        public string Controller { get; set; }
        public string Action { get; set; }
        public string RequestUrl { get; set; }
        public string RequestMethod { get; set; }

        // Kullanıcı bilgileri
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string IpAddress { get; set; }

        // Zaman
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Çözüldü mü?
        public bool IsResolved { get; set; } = false;
        public string ResolvedNote { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}