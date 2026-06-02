using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repositories.Abstract
{
    public interface IRepairMaterialRepository: IGenericRepository<RepairMaterial>
    {
        Task<List<RepairMaterial>> GetMaterialsByRepairIdAsync(int repairId);
        Task<RepairMaterial?> GetByIdAsync(int id);
        Task DeleteAsync(int id);
    }
}
