using DataAccess.Context;
using Microsoft.EntityFrameworkCore;


namespace TeknikServisTakip.Data
{
    public static class MigrationHelper
    {
        public static async Task EnsureMigrationAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                // Henüz migration yoksa oluştur
                if (db.Database.GetPendingMigrations().Any())
                {
                    logger.LogInformation("📦 Bekleyen migration'lar bulundu, uygulanıyor...");
                    await db.Database.MigrateAsync();
                    logger.LogInformation("✅ Migration başarıyla uygulandı.");
                }
                else
                {
                    logger.LogInformation("✅ Migration gerekmiyor (veritabanı güncel).");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Migration sırasında hata oluştu!");
                throw;
            }
        }
    }
}
