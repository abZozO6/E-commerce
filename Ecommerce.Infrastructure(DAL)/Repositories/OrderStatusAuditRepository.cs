using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

/// <summary>
/// Read-only repository for order status audit records.
/// Writes are performed inline inside OrderRepository transactions.
/// </summary>
public class OrderStatusAuditRepository : IOrderStatusAuditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderStatusAuditRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Returns all audit records for the given order, newest first.
    /// </summary>
    public async Task<IEnumerable<OrderStatusAuditLog>> GetByOrderIdAsync(long orderId)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT
                AuditLogId,
                OrderId,
                AdminUserId,
                CustomerUserId,
                OldStatus,
                NewStatus,
                Reason,
                ChangedAt
            FROM dbo.OrderStatusAuditLogs
            WHERE OrderId = @OrderId
            ORDER BY ChangedAt DESC, AuditLogId DESC;
            """;

        return await connection.QueryAsync<OrderStatusAuditLog>(
            sql, new { OrderId = orderId });
    }
}
