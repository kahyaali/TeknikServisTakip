using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repositories.Abstract
{
    public interface IStockMovementRepository: IGenericRepository<StockMovement>
    {
        Task<List<StockMovement>> GetMovementsByProductIdAsync(int productId, int take = 50);
        Task<List<StockMovement>> GetMovementsByDateRangeAsync(DateTime start, DateTime end);
    }
}
