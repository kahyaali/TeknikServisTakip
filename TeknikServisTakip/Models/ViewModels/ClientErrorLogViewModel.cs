namespace TeknikServisTakip.Models.ViewModels
{
    public class ClientErrorLogViewModel
    {
        public string Message { get; set; }
        public string Url { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }
        public string Stack { get; set; }
        public string UserAgent { get; set; }
        public string PageUrl { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
