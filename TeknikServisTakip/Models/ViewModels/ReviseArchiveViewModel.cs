namespace TeknikServisTakip.Models.ViewModels
{
    public class ReviseArchiveViewModel
    {
        public int Id { get; set; }
        public int OfferId { get; set; }
        public string OfferNumber { get; set; }
        public int Version { get; set; }
        public int? RepairItemId { get; set; }
        public string ProductName { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string CompanyName { get; set; }
        public DateTime RevokedAt { get; set; }
        public string RevokedBy { get; set; }
        public string Reason { get; set; }
        public int ApprovedOfferId { get; set; }
        public string ApprovedOfferNumber { get; set; }
        public int ApprovedVersion { get; set; }
        public string CurrencySymbol { get; set; }
        public decimal GrandTotal { get; set; }
    }
}
