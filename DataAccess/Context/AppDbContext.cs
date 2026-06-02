using Entities.Concrete;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace DataAccess.Context
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<RepairItem> RepairItems { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<MailSetting> MailSettings { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Personel> Personels { get; set; }
        public DbSet<PageContent> PageContents { get; set; }
        public DbSet<ProductTrackingLog> ProductTrackingLogs { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<RepairImage> RepairImages { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Reference> References { get; set; }

        public DbSet<ArchiveRepair> ArchiveRepairs { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<StockAlert> StockAlerts { get; set; }
        public DbSet<Category> Categories { get; set; }

        public DbSet<RepairMaterial> RepairMaterials { get; set; }

        public DbSet<MailLog> MailLogs { get; set; }

        public DbSet<ExpertiseLine> ExpertiseLines { get; set; }
        public DbSet<Offer> Offers { get; set; }
        public DbSet<OfferLine> OfferLines { get; set; }
        public DbSet<OfferArchive> OfferArchives { get; set; }

        public DbSet<ReviseArchive> ReviseArchives { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ========== PRODUCT KONFIGURASYONU ==========
            builder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.ProductCode).IsUnique();
                entity.HasIndex(p => p.SerialNo).IsUnique().HasFilter("[SerialNo] IS NOT NULL");
                entity.HasIndex(p => p.IMEINo).IsUnique().HasFilter("[IMEINo] IS NOT NULL");

                entity.Property(p => p.PurchasePrice).HasPrecision(18, 2);
                entity.Property(p => p.SalePrice).HasPrecision(18, 2);

                entity.HasMany(p => p.StockMovements)
                    .WithOne(sm => sm.Product)
                    .HasForeignKey(sm => sm.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.StockAlerts)
                    .WithOne(sa => sa.Product)
                    .HasForeignKey(sa => sa.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== STOCK MOVEMENT KONFIGURASYONU ==========
            builder.Entity<StockMovement>(entity =>
            {
                entity.HasIndex(m => m.CreatedAt);
                entity.HasIndex(m => m.ProductId);
            });

            // ========== STOCK ALERT KONFIGURASYONU ==========
            builder.Entity<StockAlert>(entity =>
            {
                entity.HasIndex(a => a.IsSent);
            });

            builder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Name).IsUnique();
                entity.HasIndex(c => c.DisplayOrder);
            });

            // ExpertiseLine - RepairItem ilişkisi
            builder.Entity<ExpertiseLine>()
                .HasOne(e => e.RepairItem)
                .WithMany()
                .HasForeignKey(e => e.RepairItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // ========== OFFER KONFİGÜRASYONU (YENİ EKLENDİ) ==========
            builder.Entity<Offer>(entity =>
            {
                // Konsoldaki decimal uyarılarını ezen konfigurasyonlar
                entity.Property(o => o.GrandTotal).HasPrecision(18, 4);
                entity.Property(o => o.TotalDiscountAmount).HasPrecision(18, 4);
                entity.Property(o => o.TotalLaborCost).HasPrecision(18, 4);
                entity.Property(o => o.TotalLinesAmount).HasPrecision(18, 4);
                entity.Property(o => o.TotalTaxAmount).HasPrecision(18, 4);

                // Teklif numarası üzerinden hızlı arama için indeks
                entity.HasIndex(o => o.OfferNumber).IsUnique();
            });

            // ========== OFFERLINE KONFİGÜRASYONU ==========
            builder.Entity<OfferLine>(entity =>
            {
                entity.Property(ol => ol.UnitPrice).HasPrecision(18, 2);
                entity.Property(ol => ol.SubTotal).HasPrecision(18, 2);
                entity.Property(ol => ol.DiscountRate).HasPrecision(18, 2);
                entity.Property(ol => ol.DiscountAmount).HasPrecision(18, 2);
                entity.Property(ol => ol.TaxRate).HasPrecision(18, 2);
                entity.Property(ol => ol.TaxAmount).HasPrecision(18, 2);
                entity.Property(ol => ol.Total).HasPrecision(18, 2);

                // Konsolda uyarısı çıkan eksik alan buraya eklendi kanka
                entity.Property(ol => ol.LaborCost).HasPrecision(18, 4);

                // OfferLine - Offer ilişkisi 
                entity.HasOne(ol => ol.Offer)
                      .WithMany(o => o.OfferLines)
                      .HasForeignKey(ol => ol.OfferId)
                      .OnDelete(DeleteBehavior.Cascade);

                // OfferLine - RepairItem ilişkisi
                entity.HasOne(ol => ol.RepairItem)
                      .WithMany()
                      .HasForeignKey(ol => ol.RepairItemId)
                      .OnDelete(DeleteBehavior.Restrict);

                // OfferLine - ExpertiseLine ilişkisi
                entity.HasOne(ol => ol.ExpertiseLine)
                      .WithMany()
                      .HasForeignKey(ol => ol.ExpertiseLineId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ========== OFFERARCHIVE KONFİGÜRASYONU ==========
            builder.Entity<OfferArchive>(entity =>
            {
                entity.HasIndex(x => x.OfferNumber).HasDatabaseName("IX_OfferArchives_OfferNumber");
                entity.HasIndex(x => x.CustomerNumber).HasDatabaseName("IX_OfferArchives_CustomerNumber");
                entity.HasIndex(x => x.ApprovedAt).HasDatabaseName("IX_OfferArchives_ApprovedAt");

                entity.Property(x => x.TotalSnapshotData).IsRequired();
            });

            builder.Entity<ReviseArchive>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.OfferNumber).HasMaxLength(50).IsRequired();
                entity.Property(r => r.CustomerNumber).HasMaxLength(50);
                entity.Property(r => r.CustomerName).HasMaxLength(200);
                entity.Property(r => r.RevokedBy).HasMaxLength(200);
                entity.Property(r => r.Reason).HasMaxLength(500);
                entity.Property(r => r.ApprovedOfferNumber).HasMaxLength(50);
                entity.Property(r => r.TotalSnapshotData).IsRequired();

                // İndeksler
                entity.HasIndex(r => r.OfferNumber).HasDatabaseName("IX_ReviseArchives_OfferNumber");
                entity.HasIndex(r => r.CustomerNumber).HasDatabaseName("IX_ReviseArchives_CustomerNumber");
                entity.HasIndex(r => r.RevokedAt).HasDatabaseName("IX_ReviseArchives_RevokedAt");
                entity.HasIndex(r => r.RepairItemId).HasDatabaseName("IX_ReviseArchives_RepairItemId");

                // İlişkiler
                entity.HasOne(r => r.Offer)
                      .WithMany()
                      .HasForeignKey(r => r.OfferId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.RepairItem)
                      .WithMany()
                      .HasForeignKey(r => r.RepairItemId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== APPUSER VE IDENTITY INDEKSLERI ==========
            builder.Entity<AppUser>()
                .HasIndex(x => x.CustomerNumber)
                .HasDatabaseName("IX_AspNetUsers_CustomerNumber");

            builder.Entity<AppUser>()
                 .HasIndex(x => x.CreatedAt)
                 .HasDatabaseName("IX_AspNetUsers_CreatedAt");

            builder.Entity<AppUser>()
                .HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_AspNetUsers_IsActive");

            builder.Entity<AppUser>()
                .HasIndex(x => x.FullName)
                .HasDatabaseName("IX_AspNetUsers_FullName");

            builder.Entity<AppUser>()
                .HasIndex(x => x.Email)
                .HasDatabaseName("IX_AspNetUsers_Email");

            builder.Entity<IdentityUserRole<string>>()
                 .HasIndex(x => new { x.UserId, x.RoleId })
                 .HasDatabaseName("IX_AspNetUserRoles_UserId_RoleId");

            builder.Entity<AppUser>()
       .HasIndex(u => u.CariNo)
       .IsUnique()
       .HasFilter("[CariNo] IS NOT NULL")
       .HasDatabaseName("IX_AspNetUsers_CariNo");
        }
    }
}