using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllActiveAsync();

    Task<IEnumerable<Category>> GetAllAsync();

    Task<IEnumerable<Category>> GetRootCategoriesAsync();

    Task<IEnumerable<Category>> GetSubCategoriesAsync(long parentCategoryId);

    Task<Category?> GetByIdAsync(long categoryId);

    Task<Category?> GetBySlugAsync(string slug);

    // before update or delete check slug 
    Task<bool> ExistsBySlugAsync(string slug, long? excludeCategoryId = null);

    Task<long> CreateAsync(Category category);

    Task<bool> UpdateAsync(Category category);

    Task<bool> DeleteAsync(long categoryId);
}
