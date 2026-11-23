using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

public class PaymentStatusAuditRepository : IPaymentStatusAuditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PaymentStatusAuditRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retrieves all audit records for a payment, newest first.
    /// </summary>
    public async Task<IEnumerable<PaymentStatusAuditLog>> GetByPaymentIdAsync(long paymentId)
    {
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT
                AuditLogId,
                PaymentId,
                AdminUserId,
                OldStatus,
                NewStatus,
                TransactionReference,
                Reason,
                ChangedAt
            FROM dbo.PaymentStatusAuditLogs
            WHERE PaymentId = @PaymentId
            ORDER BY ChangedAt DESC, AuditLogId DESC;
            """;

        return await connection.QueryAsync<PaymentStatusAuditLog>(
            sql, new { PaymentId = paymentId });
    }
}
