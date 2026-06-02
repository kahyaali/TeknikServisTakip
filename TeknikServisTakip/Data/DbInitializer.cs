using DataAccess.Context;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TeknikServisTakip.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // ========== SABİT ROLLER ==========
            var roles = new Dictionary<string, string>
            {
                { "SuperAdmin", "Süper Yönetici - Tüm yetkilere sahiptir" },
                { "Admin", "Normal Yönetici" },
                { "Idari", "Departman ve Pozisyon yönetimi" },
                { "Depo", "Ürün kayıt ve müşteri kaydı" },
                { "Sevkiyat", "Teslimat işlemleri" },
                { "Personel", "Teknik Servis Personeli" },
                { "Customer", "Müşteri" }
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role.Key))
                {
                    await roleManager.CreateAsync(new IdentityRole(role.Key));
                    Console.WriteLine($"✅ {role.Key} rolü oluşturuldu");
                }
            }

            // ========== SUPERADMIN KULLANICISI ==========
            var superAdminEmail = "superadmin@teknikservis.com";
            var superAdminUser = await userManager.FindByEmailAsync(superAdminEmail);
            if (superAdminUser == null)
            {
                superAdminUser = new AppUser
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    FullName = "Süper Yönetici",
                    Address = "İstanbul",
                    City = "İstanbul",
                    PhoneNumber = "05550000001",
                    IsSystemAdmin = true,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                await userManager.CreateAsync(superAdminUser, "SuperAdmin123.");
                await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
                Console.WriteLine("✅ SuperAdmin kullanıcısı oluşturuldu");
            }

            Console.WriteLine("=== DbInitializer Tamamlandı ===");
        }
    }
}