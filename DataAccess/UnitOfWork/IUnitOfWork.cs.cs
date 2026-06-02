using DataAccess.Repositories.Abstract;
using Entities.Concrete;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace DataAccess.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<RepairItem> RepairItems { get; }
        IGenericRepository<Material> Materials { get; }
        IGenericRepository<MailSetting> MailSettings { get; }
        IGenericRepository<Log> Logs { get; }
        IGenericRepository<AppUser> Users { get; }
        IGenericRepository<Personel> Personels { get; }
        IGenericRepository<PageContent> PageContents { get; }
        IGenericRepository<ProductTrackingLog> ProductTrackingLogs { get; }
        IGenericRepository<ErrorLog> ErrorLogs { get; }
        IGenericRepository<Department> Departments { get; }
        IGenericRepository<Position> Positions { get; }
        IGenericRepository<Delivery> Deliveries { get; }
        IGenericRepository<RepairImage> RepairImages { get; }
        IGenericRepository<Service> Services { get; }
        IGenericRepository<Reference> References { get; }
        IGenericRepository<ArchiveRepair> ArchiveRepairs { get; }
        IGenericRepository<ExpertiseLine> ExpertiseLines { get; }
        IGenericRepository<Offer> Offers { get; }
        IGenericRepository<OfferLine> OfferLines { get; }
        IGenericRepository<OfferArchive> OfferArchives { get; }
        Task<IEnumerable<RepairItem>> GetAllRepairsWithImagesAsync();
        IQueryable<RepairItem> GetQueryable(Expression<Func<RepairItem, bool>> predicate = null, params Expression<Func<RepairItem, object>>[] includes);


        // DEPO MODÜLÜ
        IGenericRepository<Product> Products { get; }
        IGenericRepository<StockMovement> StockMovements { get; }
        IGenericRepository<StockAlert> StockAlerts { get; }

        IProductRepository ProductRepository { get; }
        IStockMovementRepository StockMovementRepository { get; }
        IStockAlertRepository StockAlertRepository { get; }

        // KATEGORİ MODÜLÜ
        IGenericRepository<Category> Categories { get; }
        ICategoryRepository CategoryRepository { get; }

        IGenericRepository<RepairMaterial> RepairMaterials { get; }
        IRepairMaterialRepository RepairMaterialRepository { get; }

        IGenericRepository<MailLog> MailLogs { get; }

        IGenericRepository<ReviseArchive> ReviseArchives { get; }




        Task<RepairItem> GetRepairWithDetailsAsync(int id);
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();  
        Task RollbackTransactionAsync();

        Task<RepairItem> GetByIdWithIncludeAsync(int id, params Expression<Func<RepairItem, object>>[] includes);
        Task<ArchiveRepair> GetArchiveByIdWithIncludeAsync(int id, params Expression<Func<ArchiveRepair, object>>[] includes);
        Task<int> CompleteAsync();
    }
}