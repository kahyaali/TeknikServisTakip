namespace TeknikServisTakip.Models.ViewModels
{
    public class PersonelTakipViewModel
    {
        public int PersonelId { get; set; }
        public string PersonelAdi { get; set; }
        public string Email { get; set; }
        public string Telefon { get; set; }
    }

    public class PersonelIsTakipViewModel
    {
        public int RepairId { get; set; }
        public string CustomerNumber { get; set; }
        public string CompanyName { get; set; }
        public string ProductName { get; set; }
        public string ProblemDescription { get; set; }
        public DateTime ReceivedDate { get; set; }
        public int StatusId { get; set; }
        public string StatusName { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
    }

    public class PersonelIsTakipListViewModel
    {
        public int PersonelId { get; set; }
        public string PersonelAdi { get; set; }
        public List<PersonelIsTakipViewModel> Bekleyenler { get; set; } = new List<PersonelIsTakipViewModel>();
        public List<PersonelIsTakipViewModel> Islemdekiler { get; set; } = new List<PersonelIsTakipViewModel>();
        public List<PersonelIsTakipViewModel> Tamamlananlar { get; set; } = new List<PersonelIsTakipViewModel>();
        public int BekleyenCount { get; set; }
        public int IslemdeCount { get; set; }
        public int TamamlananCount { get; set; }
    }
}
