using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long userId);

    Task<User?> GetByEmailAsync(string email);

    Task<IEnumerable<User>> GetAllAsync(bool includeDeleted = false);

    Task<bool> EmailExistsAsync(string email, long? excludeUserId = null);

    Task<long> CreateAsync(User user);

    Task<bool> UpdateProfileAsync(User user);
    Task<bool> UpdateRoleAsync(long userId, string newRole);

    Task<bool> UpdatePasswordAsync(long userId, string newPasswordHash);

    Task<bool> SoftDeleteAsync(long userId);
}
