namespace TeknikServisTakip.Models.ViewModels
{
    public class ArchivedOfferViewModel
    {
        public int Id { get; set; }
        public string OfferNumber { get; set; }
        public int Version { get; set; }
        public string CustomerName { get; set; }
        public string CustomerNumber { get; set; }
        public string CompanyName { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal GrandTotal { get; set; }
        public string Currency { get; set; }
        public string CurrencySymbol { get; set; }
    }
}
