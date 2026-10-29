using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<User?> GetByIdAsync(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT UserId, FullName, Email, PasswordHash, Role,
                   IsActive, IsDeleted, DeletedAt, CreatedAt
            FROM dbo.Users
            WHERE UserId = @UserId
              AND IsDeleted = 0;";

        return await connection.QuerySingleOrDefaultAsync<User>(
            sql,
            new { UserId = userId });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT UserId, FullName, Email, PasswordHash, Role,
                   IsActive, IsDeleted, DeletedAt, CreatedAt
            FROM dbo.Users
            WHERE Email = @Email
              AND IsDeleted = 0;";

        return await connection.QuerySingleOrDefaultAsync<User>(
            sql,
            new { Email = email.Trim() });
    }

    public async Task<IEnumerable<User>> GetAllAsync( bool includeDeleted = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT UserId, FullName, Email, PasswordHash, Role,
                   IsActive, IsDeleted, DeletedAt, CreatedAt
            FROM dbo.Users
            WHERE (@IncludeDeleted = 1 OR IsDeleted = 0)
            ORDER BY CreatedAt DESC;";

        return await connection.QueryAsync<User>(
            sql,
            new { IncludeDeleted = includeDeleted });
    }

    /// <summary>
    /// Checks whether an active, non-deleted account uses the specified email.
    /// An optional user identifier can be excluded during profile updates.
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email,long? excludeUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (excludeUserId.HasValue && excludeUserId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(excludeUserId));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.Users
                WHERE Email = @Email
                  AND IsDeleted = 0
                  AND (@ExcludeId IS NULL OR UserId <> @ExcludeId)
            )
            THEN 1 ELSE 0 END;";

        return await connection.ExecuteScalarAsync<bool>(
            sql,
            new
            {
                Email = email.Trim(),
                ExcludeId = excludeUserId
            });
    }

    /// <summary>
    /// Creates a user and returns the generated identifier.
    /// The password must already be securely hashed.
    /// </summary>
    public async Task<long> CreateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        ArgumentException.ThrowIfNullOrWhiteSpace(user.FullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.PasswordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Role);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO dbo.Users
                (FullName, Email, PasswordHash, Role, IsActive)
            VALUES
                (@FullName, @Email, @PasswordHash, @Role, @IsActive);

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

        return await connection.ExecuteScalarAsync<long>(
            sql,
            new
            {
                FullName = user.FullName.Trim(),
                Email = user.Email.Trim(),
                user.PasswordHash,
                user.Role,
                user.IsActive
            });
    }

    /// <summary>
    /// Updates the basic profile information of a non-deleted user.
    /// This method cannot change the user's role, password, or account status.
    /// </summary>
    public async Task<bool> UpdateProfileAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(user.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.FullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Email);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.Users
            SET FullName = @FullName,
                Email = @Email
            WHERE UserId = @UserId
              AND IsDeleted = 0;";

        int rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                user.UserId,
                FullName = user.FullName.Trim(),
                Email = user.Email.Trim()
            });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Updates the role of a non-deleted user.
    /// The caller must verify administrator authorization.
    /// </summary>
    public async Task<bool> UpdateRoleAsync(long userId, string newRole)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRole);

        var allowedRoles = new[] { "Customer", "Admin" };

        if (!allowedRoles.Contains(newRole, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The specified role is not supported.",
                nameof(newRole));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
        UPDATE dbo.Users
        SET Role = @Role
        WHERE UserId = @UserId
          AND IsDeleted = 0;";

        int rowsAffected = await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            Role = allowedRoles.First(
                role => string.Equals(
                    role,
                    newRole,
                    StringComparison.OrdinalIgnoreCase))
        });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Updates the password hash of a non-deleted user.
    /// The supplied password value must already be securely hashed.
    /// </summary>
    public async Task<bool> UpdatePasswordAsync(long userId,string newPasswordHash)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.Users
            SET PasswordHash = @PasswordHash
            WHERE UserId = @UserId
              AND IsDeleted = 0;";

        int rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                UserId = userId,
                PasswordHash = newPasswordHash
            });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Soft-deletes a user and removes their cart and saved addresses.
    /// All changes are committed together or rolled back on failure.
    /// Historical orders and reviews are preserved.
    /// </summary>
    public async Task<bool> SoftDeleteAsync(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string softDeleteSql = @"
                UPDATE dbo.Users
                SET IsDeleted = 1,
                    DeletedAt = SYSUTCDATETIME()
                WHERE UserId = @UserId
                  AND IsDeleted = 0;";

            int rowsAffected = await connection.ExecuteAsync(
                softDeleteSql,
                new { UserId = userId },
                transaction);

            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return false;
            }

            const string cleanupSql = @"
                DELETE FROM dbo.Carts
                WHERE UserId = @UserId;

                DELETE FROM dbo.Addresses
                WHERE UserId = @UserId;";

            await connection.ExecuteAsync(
                cleanupSql,
                new { UserId = userId },
                transaction);

            transaction.Commit();

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
