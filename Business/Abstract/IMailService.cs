using Entities.Concrete;

namespace TeknikServisTakip.Business.Abstract
{
    public interface IMailService
    {
        Task<bool> SendMailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendTestMailAsync(string toEmail);
        Task<bool> SendRepairCreatedMailAsync(string toEmail, string customerName, string productName, string trackingCode, string qrCodePath, string baseUrl);
        Task<MailSetting> GetActiveMailSettingAsync();


        // Mail log sorgulama
        Task<List<MailLog>> GetMailLogsAsync(int take = 100, string? mailType = null);
        Task<MailLog?> GetMailLogByIdAsync(int id);
    }
}