using Business.Abstract;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class CategoryManager: ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CategoryManager(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _unitOfWork.Categories.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _unitOfWork.Categories.GetAllAsync();
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            return await _unitOfWork.CategoryRepository.GetActiveCategoriesAsync();
        }

        public async Task<Category> AddAsync(Category category, string userId)
        {
            if (await _unitOfWork.CategoryRepository.IsNameExistsAsync(category.Name))
                throw new Exception($"'{category.Name}' kategorisi zaten mevcut!");

            category.CreatedBy = userId;
            category.CreatedAt = DateTime.Now;

            await _unitOfWork.Categories.AddAsync(category);
            await _unitOfWork.CompleteAsync();

            return category;
        }

        public async Task<Category> UpdateAsync(Category category, string userId)
        {
            var existing = await _unitOfWork.Categories.GetByIdAsync(category.Id);
            if (existing == null)
                throw new Exception("Kategori bulunamadı!");

            // Aynı isimde başka kategori var mı kontrol et
            if (category.Name != existing.Name)
            {
                if (await _unitOfWork.CategoryRepository.IsNameExistsAsync(category.Name, category.Id))
                    throw new Exception($"'{category.Name}' kategorisi zaten mevcut!");
            }

            //  MEVCUT NESNENİN ALANLARINI GÜNCELLE
            existing.Name = category.Name;
            existing.Description = category.Description;
            existing.DisplayOrder = category.DisplayOrder;
            existing.IsActive = category.IsActive;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.Now;

            _unitOfWork.Categories.Update(existing);
            await _unitOfWork.CompleteAsync();

            return existing;
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return false;

            // Bu kategoriye ait ürün var mı kontrol et
            var hasProducts = await _unitOfWork.Products.GetWhereAsync(p => p.CategoryId == id);
            if (hasProducts.Any())
                throw new Exception("Bu kategoriye ait ürünler var. Önce ürünleri silmeli veya taşımalısınız!");

            _unitOfWork.Categories.Delete(category);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> SoftDeleteAsync(int id, string userId)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
                return false;

            category.IsActive = false;
            category.UpdatedAt = DateTime.Now;
            category.UpdatedBy = userId;

            _unitOfWork.Categories.Update(category);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<bool> IsNameExistsAsync(string name, int? excludeId = null)
        {
            return await _unitOfWork.CategoryRepository.IsNameExistsAsync(name, excludeId);
        }
    }
}
