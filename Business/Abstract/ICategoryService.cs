using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface ICategoryService
    {
        Task<Category?> GetByIdAsync(int id);
        Task<IEnumerable<Category>> GetAllAsync();
        Task<IEnumerable<Category>> GetActiveCategoriesAsync();
        Task<Category> AddAsync(Category category, string userId);
        Task<Category> UpdateAsync(Category category, string userId);
        Task<bool> DeleteAsync(int id, string userId);
        Task<bool> SoftDeleteAsync(int id, string userId);
        Task<bool> IsNameExistsAsync(string name, int? excludeId = null);
    }
}
