using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(long paymentId);

    Task<IEnumerable<Payment>> GetByOrderIdAsync(long orderId);

    Task<Payment?> GetByTransactionReferenceAsync(string transactionReference);

    Task<IEnumerable<Payment>> GetAllAsync(string? status = null);

    Task<long> CreateAsync(Payment payment);

    Task<bool> UpdateStatusAsync(long paymentId, string newStatus, string? transactionReference = null);
}
