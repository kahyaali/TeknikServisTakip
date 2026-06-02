using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repositories.Abstract
{
    public interface IStockAlertRepository: IGenericRepository<StockAlert>
    {
        Task<List<StockAlert>> GetUnsentAlertsAsync();
        Task MarkAsSentAsync(int alertId);
    }
}
