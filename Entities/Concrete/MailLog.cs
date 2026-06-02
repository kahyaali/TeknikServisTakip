using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class MailLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        public string? Body { get; set; }

        public bool IsSent { get; set; }

        [StringLength(500)]
        public string? ErrorMessage { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? SentBy { get; set; }

        [StringLength(50)]
        public string? MailType { get; set; }  

        public int? RelatedEntityId { get; set; }  

        [StringLength(100)]
        public string? RelatedEntityType { get; set; } 
    }
}
