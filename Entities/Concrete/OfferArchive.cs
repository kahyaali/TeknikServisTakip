using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete
{
    public class OfferArchive
    {
        public int Id { get; set; }
        public int OfferId { get; set; }
        public string OfferNumber { get; set; }
        public string CustomerNumber { get; set; }
        public DateTime ApprovedAt { get; set; }
        public string ArchivedBy { get; set; }

        // Onay anındaki tüm ürün ve kalemlerin JSON hali 
        public string TotalSnapshotData { get; set; }
    }
}
