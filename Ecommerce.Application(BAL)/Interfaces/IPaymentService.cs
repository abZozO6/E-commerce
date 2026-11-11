using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ecommerce.Application_BAL_.DTOs.Payments;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> GetMyPaymentByIdAsync(long paymentId, long userId);

    Task<IEnumerable<PaymentDto>> GetMyPaymentsByOrderIdAsync(long orderId, long userId);

    Task<long> InitiatePaymentAsync(long orderId, long userId, CreatePaymentDto dto);

    Task<bool> HandleVerifiedCallbackAsync(
        long paymentId,
        string newStatus,
        string? transactionReference = null);
}