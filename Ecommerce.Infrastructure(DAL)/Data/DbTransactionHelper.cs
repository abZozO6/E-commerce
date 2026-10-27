
using System.Data.Common;

namespace Ecommerce.Infrastructure_DAL_.Data;

public static class DbTransactionHelper
{
    public static async Task<T> ExecuteAsync<T>(
        DbConnection connection,
        Func<DbConnection, DbTransaction, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);

        if (connection.State != System.Data.ConnectionState.Closed &&
            connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException(
                "The database connection is in an invalid state.");
        }

        if (connection.State == System.Data.ConnectionState.Closed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await action(
                connection, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            try
            {
                // Try to roll back without letting a rollback error
                // hide the original exception.
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }
    }
}
