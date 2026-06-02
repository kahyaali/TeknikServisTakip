using DataAccess.Context;
using DataAccess.Repositories.Abstract;
using Entities.Concrete;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repositories.Concrete
{
    public class EfProductRepository: GenericRepository<Product>, IProductRepository
    {
        private readonly AppDbContext _context;

        public EfProductRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Product?> GetProductWithMovementsAsync(int id)
        {
            return await _context.Products
                .Include(p => p.StockMovements)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Product>> GetLowStockProductsAsync(int threshold = 5)
        {
            return await _context.Products
                .Where(p => p.IsActive && p.Quantity <= threshold && p.Quantity > 0)
                .OrderBy(p => p.Quantity)
                .ToListAsync();
        }

        public async Task<List<Product>> GetCriticalStockProductsAsync()
        {
            return await _context.Products
                .Where(p => p.IsActive && p.Quantity == 0)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
        }

        public async Task<string> GenerateProductCodeAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"DEP-{year}";

            var lastProduct = await _context.Products
                .Where(p => p.ProductCode != null && p.ProductCode.StartsWith(prefix))
                .OrderByDescending(p => p.ProductCode)
                .Select(p => p.ProductCode)
                .FirstOrDefaultAsync();

            int lastNumber = 0;
            if (!string.IsNullOrEmpty(lastProduct))
            {
                var numberPart = lastProduct.Substring(prefix.Length);
                int.TryParse(numberPart, out lastNumber);
            }

            var newNumber = lastNumber + 1;
            return $"{prefix}{newNumber:D6}";
        }

        public async Task<bool> IsSerialNoExistsAsync(string serialNo, int? excludeId = null)
        {
            var query = _context.Products.Where(p => p.SerialNo == serialNo);
            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> IsIMEINoExistsAsync(string imeiNo, int? excludeId = null)
        {
            var query = _context.Products.Where(p => p.IMEINo == imeiNo);
            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<(ICollection<Product> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchTerm = null)
        {
            var query = _context.Products
                 .Include(p => p.Category)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.ProductName.Contains(searchTerm) ||
                    (p.ProductCode != null && p.ProductCode.Contains(searchTerm)) ||
                    (p.SerialNo != null && p.SerialNo.Contains(searchTerm)) ||
                    (p.Brand != null && p.Brand.Contains(searchTerm)) ||
                    (p.Category != null && p.Category.Name.Contains(searchTerm))
                );
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}

