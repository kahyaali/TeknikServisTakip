


using Business.Abstract;

namespace TeknikServisTakip.Services
{
    public class StockAlertBackgroundService: BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StockAlertBackgroundService> _logger;

        public StockAlertBackgroundService(IServiceProvider serviceProvider, ILogger<StockAlertBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Her gün saat 09:00'da çalış
                    var now = DateTime.Now;
                    var nextRun = DateTime.Today.AddHours(9);
                    if (now > nextRun)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    var delay = nextRun - now;
                    await Task.Delay(delay, stoppingToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                        await productService.SendStockAlertsAsync();
                        _logger.LogInformation("Stok uyarı maili gönderildi: {Time}", DateTime.Now);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stok uyarı maili gönderilirken hata oluştu");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }
    }
}
