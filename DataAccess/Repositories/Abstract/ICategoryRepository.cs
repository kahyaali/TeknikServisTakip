using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repositories.Abstract
{
    public interface ICategoryRepository: IGenericRepository<Category>
    {
        Task<List<Category>> GetActiveCategoriesAsync();
        Task<bool> IsNameExistsAsync(string name, int? excludeId = null);
    }
}
