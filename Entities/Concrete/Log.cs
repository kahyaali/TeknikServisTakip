using System.ComponentModel.DataAnnotations;

namespace Entities.Concrete
{
    public class Log
    {
        [Key]
        public int Id { get; set; }

        // Kullanıcı bilgileri
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserRole { get; set; }
        public string IpAddress { get; set; }

        // İşlem bilgileri
        public string Action { get; set; }          // Controller/Action logu için
        public string ActionType { get; set; }      // Create, Update, Delete, Read, Login, Logout işlemleri için
        public string EntityName { get; set; }      // Personel, Repair, Admin, User kulanıcılar için
        public int? EntityId { get; set; }          // Kaydın ID'si

        // Detaylar
        public string Description { get; set; }
        public string OldValues { get; set; }       // JSON formatında eski değerler
        public string NewValues { get; set; }       // JSON formatında yeni değerler

        // Teknik detaylar
        public string RequestMethod { get; set; }   // GET, POST, PUT, DELETE
        public string RequestUrl { get; set; }
        public string UserAgent { get; set; }

        // Zaman
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Başarı durumu
        public bool IsSuccess { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
}