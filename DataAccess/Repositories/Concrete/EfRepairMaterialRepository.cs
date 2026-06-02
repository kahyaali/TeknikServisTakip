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
    public class EfRepairMaterialRepository: GenericRepository<RepairMaterial>, IRepairMaterialRepository
    {
        private readonly AppDbContext _context;

        public EfRepairMaterialRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<RepairMaterial>> GetMaterialsByRepairIdAsync(int repairId)
        {
            return await _context.RepairMaterials
                .Include(r => r.Product)
                .Where(r => r.RepairId == repairId)
                .OrderByDescending(r => r.UsedAt)
                .ToListAsync();
        }

        public async Task<RepairMaterial?> GetByIdAsync(int id)
        {
            return await _context.RepairMaterials
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task DeleteAsync(int id)
        {
            var material = await _context.RepairMaterials.FindAsync(id);
            if (material != null)
            {
                _context.RepairMaterials.Remove(material);
                await _context.SaveChangesAsync();
            }
        }
    }
}
