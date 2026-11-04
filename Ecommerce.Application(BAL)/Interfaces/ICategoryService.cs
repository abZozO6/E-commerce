using Ecommerce.Application_BAL_.DTOs.Categories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.Interfaces
{
    public interface ICategoryService
    {
        Task<IEnumerable<CategoryDto>> GetAllActiveAsync();
        Task<IEnumerable<CategoryDto>> GetAllAsync();
        Task<IEnumerable<CategoryDto>> GetRootCategoriesAsync();
        Task<IEnumerable<CategoryDto>> GetSubCategoriesAsync(long parentCategoryId);
        Task<CategoryDto?> GetByIdAsync(long categoryId);
        Task<CategoryDto?> GetBySlugAsync(string slug);
        Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
        Task<bool> UpdateAsync(long categoryId, UpdateCategoryDto dto);
        Task<bool> DeleteAsync(long categoryId);
    }
}
