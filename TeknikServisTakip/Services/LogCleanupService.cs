using DataAccess.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TeknikServisTakip.Services
{
    public class LogCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogCleanupService> _logger;

        public LogCleanupService(IServiceScopeFactory scopeFactory, ILogger<LogCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Her gün saat 03:00'te çalış
                    var now = DateTime.Now;
                    var nextRun = now.Date.AddDays(1).AddHours(3);
                    var delay = nextRun - now;

                    if (delay.TotalMilliseconds < 0)
                        delay = TimeSpan.FromHours(24);

                    _logger.LogInformation($"Log temizleme servisi {nextRun:HH:mm} saatinde çalışacak.");
                    await Task.Delay(delay, stoppingToken);

                    await CleanupOldLogs();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Log temizleme servisi hatası");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private async Task CleanupOldLogs()
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // 3 aydan (90 gün) eski logları sil
            var cutoffDate = DateTime.Now.AddDays(-90);

            var allLogs = await unitOfWork.Logs.GetAllAsync();
            var oldLogs = allLogs.Where(l => l.CreatedAt < cutoffDate).ToList();

            if (oldLogs.Any())
            {
                foreach (var log in oldLogs)
                {
                    unitOfWork.Logs.Delete(log);
                }

                await unitOfWork.CompleteAsync();
                _logger.LogInformation($"{oldLogs.Count} eski log kaydı temizlendi. (Tarih: {cutoffDate:dd.MM.yyyy})");
            }
            else
            {
                _logger.LogInformation("Temizlenecek eski log kaydı bulunamadı.");
            }

            // ErrorLogs için de temizlik (1 yıldan eski hataları sil)
            var errorCutoffDate = DateTime.Now.AddYears(-1);
            var allErrors = await unitOfWork.ErrorLogs.GetAllAsync();
            var oldErrors = allErrors.Where(e => e.CreatedAt < errorCutoffDate && e.IsResolved == true).ToList();

            if (oldErrors.Any())
            {
                foreach (var error in oldErrors)
                {
                    unitOfWork.ErrorLogs.Delete(error);
                }
                await unitOfWork.CompleteAsync();
                _logger.LogInformation($"{oldErrors.Count} eski hata logu temizlendi.");
            }
        }
    }
}