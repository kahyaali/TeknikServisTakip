using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class ExpertiseLine
    {
        public int Id { get; set; }
        public int RepairItemId { get; set; }
        public string Description { get; set; }          
        public int Quantity { get; set; } = 1;
        public string Unit { get; set; } = "Piece";       
        public string Note { get; set; }
        public int LineOrder { get; set; }
        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsIncludedInOffer { get; set; }

        [ForeignKey("RepairItemId")]
        public virtual RepairItem RepairItem { get; set; }
    }
}
