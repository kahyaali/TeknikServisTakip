using DataAccess.Context;
using DataAccess.Repositories.Abstract;
using DataAccess.Repositories.Concrete;
using Entities.Concrete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace DataAccess.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction _currentTransaction;

        private IGenericRepository<RepairItem> _repairItems;
        private IGenericRepository<Material> _materials;
        private IGenericRepository<MailSetting> _mailSettings;
        private IGenericRepository<Log> _logs;
        private IGenericRepository<AppUser> _users;
        private IGenericRepository<Personel> _personels;
        private IGenericRepository<PageContent> _pageContents;
        private IGenericRepository<ProductTrackingLog> _productTrackingLogs;
        private IGenericRepository<ErrorLog> _errorLogs;
        private IGenericRepository<Department> _departments;
        private IGenericRepository<Position> _positions;
        private IGenericRepository<Delivery> _deliveries;
        private IGenericRepository<RepairImage> _repairImages;
        private IGenericRepository<Service> _services;
        private IGenericRepository<Reference> _references;
        private IGenericRepository<ArchiveRepair> _archiveRepairs;
        private IGenericRepository<ExpertiseLine> _expertiseLines;
        private IGenericRepository<Offer> _offers;
        private IGenericRepository<OfferLine> _offerLines;
        private IGenericRepository<OfferArchive> _offerArchives;


        // DEPO MODÜLÜ
        private IGenericRepository<Product> _products;
        private IGenericRepository<StockMovement> _stockMovements;
        private IGenericRepository<StockAlert> _stockAlerts;

        private IProductRepository _productRepository;
        private IStockMovementRepository _stockMovementRepository;
        private IStockAlertRepository _stockAlertRepository;

        // KATEGORİ MODÜLÜ
        private IGenericRepository<Category> _categories;
        private ICategoryRepository _categoryRepository;

        private IGenericRepository<RepairMaterial> _repairMaterials;
        private IRepairMaterialRepository _repairMaterialRepository;

        private IGenericRepository<MailLog> _mailLogs;

        private IGenericRepository<ReviseArchive> _reviseArchives;

        public IGenericRepository<ReviseArchive> ReviseArchives => _reviseArchives ??= new GenericRepository<ReviseArchive>(_context);

        public IGenericRepository<MailLog> MailLogs
        => _mailLogs ??= new GenericRepository<MailLog>(_context);

        public IGenericRepository<Product> Products
         => _products ??= new GenericRepository<Product>(_context);

        public IGenericRepository<StockMovement> StockMovements
            => _stockMovements ??= new GenericRepository<StockMovement>(_context);

        public IGenericRepository<StockAlert> StockAlerts
            => _stockAlerts ??= new GenericRepository<StockAlert>(_context);

        public IProductRepository ProductRepository
            => _productRepository ??= new EfProductRepository(_context);

        public IStockMovementRepository StockMovementRepository
            => _stockMovementRepository ??= new EfStockMovementRepository(_context);

        public IStockAlertRepository StockAlertRepository
            => _stockAlertRepository ??= new EfStockAlertRepository(_context);

        public IGenericRepository<Category> Categories
        => _categories ??= new GenericRepository<Category>(_context);

        public ICategoryRepository CategoryRepository
            => _categoryRepository ??= new EfCategoryRepository(_context);

        public IGenericRepository<RepairMaterial> RepairMaterials
         => _repairMaterials ??= new GenericRepository<RepairMaterial>(_context);

        public IRepairMaterialRepository RepairMaterialRepository
            => _repairMaterialRepository ??= new EfRepairMaterialRepository(_context);
        public IGenericRepository<ExpertiseLine> ExpertiseLines
        => _expertiseLines ??= new GenericRepository<ExpertiseLine>(_context);

        public IGenericRepository<Offer> Offers
            => _offers ??= new GenericRepository<Offer>(_context);

        public IGenericRepository<OfferLine> OfferLines
            => _offerLines ??= new GenericRepository<OfferLine>(_context);

        public IGenericRepository<OfferArchive> OfferArchives
    => _offerArchives ??= new GenericRepository<OfferArchive>(_context);


        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IGenericRepository<RepairItem> RepairItems
            => _repairItems ??= new GenericRepository<RepairItem>(_context);

        public IGenericRepository<Material> Materials
            => _materials ??= new GenericRepository<Material>(_context);

        public IGenericRepository<MailSetting> MailSettings
            => _mailSettings ??= new GenericRepository<MailSetting>(_context);

        public IGenericRepository<Log> Logs
            => _logs ??= new GenericRepository<Log>(_context);

        public IGenericRepository<ProductTrackingLog> ProductTrackingLogs
            => _productTrackingLogs ??= new GenericRepository<ProductTrackingLog>(_context);

        public IGenericRepository<ErrorLog> ErrorLogs
            => _errorLogs ??= new GenericRepository<ErrorLog>(_context);

        public IGenericRepository<AppUser> Users
            => _users ??= new GenericRepository<AppUser>(_context);

        public IGenericRepository<Personel> Personels
            => _personels ??= new GenericRepository<Personel>(_context);

        public IGenericRepository<PageContent> PageContents
         => _pageContents ??= new GenericRepository<PageContent>(_context);

        public IGenericRepository<RepairImage> RepairImages => _repairImages ??= new GenericRepository<RepairImage>(_context);

        public IGenericRepository<Department> Departments => _departments ??= new GenericRepository<Department>(_context);
        public IGenericRepository<Position> Positions => _positions ??= new GenericRepository<Position>(_context);
        public IGenericRepository<Delivery> Deliveries => _deliveries ??= new GenericRepository<Delivery>(_context);
        public IGenericRepository<Service> Services => _services ??= new GenericRepository<Service>(_context);
        public IGenericRepository<Reference> References => _references ??= new GenericRepository<Reference>(_context);
        public IGenericRepository<ArchiveRepair> ArchiveRepairs => _archiveRepairs ??= new GenericRepository<ArchiveRepair>(_context);

        public async Task<RepairItem> GetRepairWithDetailsAsync(int id)
        {
            if (_repairItems == null)
            {
                _repairItems = new GenericRepository<RepairItem>(_context);
            }
            return await _repairItems.GetByIdWithIncludeAsync(id, r => r.Personel,  r => r.RepairImages);
        }

        public IQueryable<RepairItem> GetQueryable(Expression<Func<RepairItem, bool>> predicate = null, params Expression<Func<RepairItem, object>>[] includes)
        {
            IQueryable<RepairItem> query = _repairItems.GetQueryable();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return query;
        }

        public async Task<RepairItem> GetByIdWithIncludeAsync(int id, params Expression<Func<RepairItem, object>>[] includes)
        {
            var query = _context.RepairItems.AsQueryable();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<ArchiveRepair> GetArchiveByIdWithIncludeAsync(int id, params Expression<Func<ArchiveRepair, object>>[] includes)
        {
            var query = _context.ArchiveRepairs.AsQueryable();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<IEnumerable<RepairItem>> GetAllRepairsWithImagesAsync()
        {
            return await RepairItems.GetAllAsync(r => r.Personel, r => r.RepairImages);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            _currentTransaction = await _context.Database.BeginTransactionAsync();
            return _currentTransaction;
        }
        public async Task CommitTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync();
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync();
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public async Task<int> CompleteAsync() => await _context.SaveChangesAsync();

        public void Dispose() => _context.Dispose();
    }
}