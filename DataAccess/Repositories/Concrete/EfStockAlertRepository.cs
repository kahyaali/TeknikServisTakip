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
    public class EfStockAlertRepository: GenericRepository<StockAlert>, IStockAlertRepository
    {
        private readonly AppDbContext _context;

        public EfStockAlertRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<StockAlert>> GetUnsentAlertsAsync()
        {
            return await _context.StockAlerts
                .Where(a => !a.IsSent)
                .Include(a => a.Product)
                .ToListAsync();
        }

        public async Task MarkAsSentAsync(int alertId)
        {
            var alert = await _context.StockAlerts.FindAsync(alertId);
            if (alert != null)
            {
                alert.IsSent = true;
                alert.SentAt = DateTime.Now;
                _context.StockAlerts.Update(alert);
                await _context.SaveChangesAsync();
            }
        }
    }
}
