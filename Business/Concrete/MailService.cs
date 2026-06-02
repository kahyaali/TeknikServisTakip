using System.Net;
using System.Net.Mail;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using TeknikServisTakip.Business.Abstract;
using System.IO;
using Microsoft.EntityFrameworkCore;


namespace TeknikServisTakip.Business.Concrete
{
    public class MailService : IMailService
    {
        private readonly IUnitOfWork _unitOfWork;

        public MailService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<MailSetting> GetActiveMailSettingAsync()
        {
            var settings = await _unitOfWork.MailSettings.GetWhereAsync(m => m.IsActive == true);
            return settings.FirstOrDefault();
        }

        public async Task<bool> SendMailAsync(string to, string subject, string body, bool isHtml = true)
        {

            var mailLog = new MailLog
            {
                ToEmail = to,
                Subject = subject,
                Body = body?.Length > 500 ? body.Substring(0, 500) : body,
                SentAt = DateTime.Now,
                IsSent = false,
                MailType = "General"
            };

            try
            {
                var mailSetting = await GetActiveMailSettingAsync();
                if (mailSetting == null)
                {
                    mailLog.ErrorMessage = "Aktif mail ayarı bulunamadı!";
                    await _unitOfWork.MailLogs.AddAsync(mailLog);  
                    await _unitOfWork.CompleteAsync();
                    Console.WriteLine("Aktif mail ayarı bulunamadı!");
                    return false;
                }

                using (var client = new SmtpClient(mailSetting.SmtpServer, mailSetting.Port))
                {
                    client.Credentials = new NetworkCredential(mailSetting.SenderEmail, mailSetting.SenderPassword);
                    client.EnableSsl = mailSetting.UseSSL;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(mailSetting.SenderEmail, "Teknik Servis Takip"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = isHtml
                    };
                    mailMessage.To.Add(to);

                    await client.SendMailAsync(mailMessage);
                    mailLog.IsSent = true;
                    mailLog.SentBy = mailSetting.SenderEmail;
                    await _unitOfWork.MailLogs.AddAsync(mailLog);  
                    await _unitOfWork.CompleteAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {

                mailLog.ErrorMessage = $"{ex.Message} | Inner: {ex.InnerException?.Message}";
                mailLog.IsSent = false;
                await _unitOfWork.MailLogs.AddAsync(mailLog);  
                await _unitOfWork.CompleteAsync();
                Console.WriteLine($"Mail gönderme hatası: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendTestMailAsync(string toEmail)
        {
            string body = @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family:Arial; text-align:center; padding:20px;'>
    <h2 style='color:#0d6efd;'>Teknik Servis Takip Sistemi</h2>
    <p>Test maili başarıyla gönderilmiştir.</p>
    <p>Mail ayarlarınız doğru çalışıyor.</p>
    <hr/>
    <small>Bu bir test mailidir.</small>
</body>
</html>";
            return await SendMailAsync(toEmail, "Test Maili", body, true);
        }

        public async Task<bool> SendRepairCreatedMailAsync(string toEmail, string customerName, string productName, string trackingCode, string qrCodePath, string baseUrl)
        {
            string fullQrUrl = $"{baseUrl}{qrCodePath}";
            string trackUrl = $"{baseUrl}/Track?trackingCode={trackingCode}";

            // QR kodu base64 olarak oku (mail istemcisi engellemesin diye)
            string qrBase64 = "";
            string qrFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", qrCodePath.TrimStart('/'));
            if (File.Exists(qrFilePath))
            {
                byte[] imageBytes = await File.ReadAllBytesAsync(qrFilePath);
                qrBase64 = Convert.ToBase64String(imageBytes);
                fullQrUrl = $"data:image/png;base64,{qrBase64}";
            }

            string body = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='utf-8'>
        <style>
            body {{ font-family: Arial, sans-serif; }}
            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
            .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; }}
            .content {{ padding: 20px; background: #f8f9fa; }}
            .info {{ margin: 15px 0; padding: 10px; background: white; border-radius: 8px; }}
            .tracking-code {{ font-size: 24px; font-weight: bold; color: #0d6efd; }}
            .qr-code {{ text-align: center; margin: 20px 0; }}
            .footer {{ text-align: center; padding: 15px; font-size: 12px; color: #6c757d; }}
            .btn {{ display: inline-block; padding: 10px 20px; background: #0d6efd; color: white; text-decoration: none; border-radius: 5px; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='header'>
                <h2>Teknik Servis Takip Sistemi</h2>
            </div>
            <div class='content'>
                <h3>Sayın {customerName},</h3>
                <p>Tamir kaydınız başarıyla oluşturulmuştur.</p>
                
                <div class='info'>
                    <strong>Ürün:</strong> {productName}<br/>
                    <strong>Takip Kodunuz:</strong> <span class='tracking-code'>{trackingCode}</span>
                </div>
                
                <div class='qr-code'>
                    <p>Karekodu okutarak da takip edebilirsiniz:</p>
                    <img src='{fullQrUrl}' alt='QR Kod' style='max-width: 200px;' />
                </div>
                
                <p>Tamir sürecinizi aşağıdaki butona tıklayarak takip edebilirsiniz:</p>
                <div style='text-align: center;'>
                    <a href='{trackUrl}' class='btn'>Tamirimi Takip Et</a>
                </div>
            </div>
            <div class='footer'>
                &copy; {DateTime.Now.Year} Teknik Servis Takip Sistemi<br/>
                Bu e-posta otomatik olarak gönderilmiştir.
            </div>
        </div>
    </body>
    </html>";

            return await SendMailAsync(toEmail, $"{trackingCode} - Tamir Kaydınız Oluşturuldu", body, true);
        }

        public async Task<List<MailLog>> GetMailLogsAsync(int take = 100, string? mailType = null)
        {
            var query = _unitOfWork.MailLogs.GetQueryable();

            if (!string.IsNullOrEmpty(mailType))
            {
                query = query.Where(m => m.MailType == mailType);
            }

            return await query.OrderByDescending(m => m.SentAt).Take(take).ToListAsync();
        }

        public async Task<MailLog?> GetMailLogByIdAsync(int id)
        {
            return await _unitOfWork.MailLogs.GetByIdAsync(id);
        }
    }
}