using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IPaymentStatusAuditRepository
{
    /// <summary>
    /// Retrieves all audit log records for a given payment, ordered by ChangedAt DESC.
    /// </summary>
    Task<IEnumerable<PaymentStatusAuditLog>> GetByPaymentIdAsync(long paymentId);
}
