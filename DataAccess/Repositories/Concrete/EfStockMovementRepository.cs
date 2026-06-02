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
    public class EfStockMovementRepository: GenericRepository<StockMovement>, IStockMovementRepository
    {
        private readonly AppDbContext _context;

        public EfStockMovementRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<StockMovement>> GetMovementsByProductIdAsync(int productId, int take = 50)
        {
            return await _context.StockMovements
                .Where(m => m.ProductId == productId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<StockMovement>> GetMovementsByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _context.StockMovements
                .Where(m => m.CreatedAt >= start && m.CreatedAt <= end)
                .Include(m => m.Product)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }
    }
}
