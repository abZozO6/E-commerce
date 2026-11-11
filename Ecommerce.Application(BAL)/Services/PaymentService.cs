using Ecommerce.Application_BAL_.DTOs.Payments;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IOrderRepository orderRepository)
    {
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
    }

    public async Task<PaymentDto?> GetMyPaymentByIdAsync(
        long paymentId,
        long userId)
    {
        ValidateId(paymentId, nameof(paymentId));
        ValidateId(userId, nameof(userId));

        var payment = await _paymentRepository.GetByIdAsync(paymentId);

        if (payment is null)
            return null;

        var order = await _orderRepository.GetByIdAsync(payment.OrderId);

        if (order is null || order.UserId != userId)
            return null;

        return MapToDto(payment);
    }

    public async Task<IEnumerable<PaymentDto>> GetMyPaymentsByOrderIdAsync(
        long orderId,
        long userId)
    {
        ValidateId(orderId, nameof(orderId));
        ValidateId(userId, nameof(userId));

        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null || order.UserId != userId)
            return Enumerable.Empty<PaymentDto>();

        var payments = await _paymentRepository.GetByOrderIdAsync(orderId);

        return payments.Select(MapToDto);
    }

    public async Task<long> InitiatePaymentAsync(
        long orderId,
        long userId,
        CreatePaymentDto dto)
    {
        ValidateId(orderId, nameof(orderId));
        ValidateId(userId, nameof(userId));

        ArgumentNullException.ThrowIfNull(dto);

        var provider = dto.Provider?.Trim();

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Payment provider is required.");

        if (provider.Length > 50)
            throw new ArgumentException("Payment provider cannot exceed 50 characters.");

        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
            throw new InvalidOperationException("Order was not found.");

        if (order.UserId != userId)
            throw new UnauthorizedAccessException(
                "You are not allowed to pay for this order.");

        if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Cannot pay for a cancelled order.");

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "This order has already been paid.");

        if (string.Equals(order.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.PaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "This order has already been refunded.");

        if (order.TotalAmount <= 0)
            throw new InvalidOperationException(
                "Order amount must be greater than zero.");

        var payment = new Payment
        {
            OrderId = orderId,
            Provider = provider,
            Amount = order.TotalAmount,
            Status = "Pending",
            PaidAt = null
        };

        return await _paymentRepository.CreateAsync(payment);
    }

    /// <summary>
    /// Internal/webhook-only handler to apply a provider payment outcome.
    /// Must only be called after cryptographic webhook signature verification at the API layer
    /// (signature verification middleware/endpoint is not yet implemented).
    /// </summary>
    public async Task<bool> HandleVerifiedCallbackAsync(
        long paymentId,
        string newStatus,
        string? transactionReference = null)
    {
        ValidateId(paymentId, nameof(paymentId));

        if (string.IsNullOrWhiteSpace(newStatus))
            throw new ArgumentException("Payment status is required.");

        var allowedCallbackStatuses = new[]
        {
            "Succeeded",
            "Failed",
            "Cancelled",
            "Refunded"
        };

        var normalizedStatus = allowedCallbackStatuses.FirstOrDefault(
            status => status.Equals(
                newStatus.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (normalizedStatus is null)
            throw new ArgumentException("Invalid payment status.");

        return await _paymentRepository.UpdateStatusAsync(
            paymentId,
            normalizedStatus,
            transactionReference);
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            PaymentId = payment.PaymentId,
            OrderId = payment.OrderId,
            Provider = payment.Provider,
            ProviderReference = payment.ProviderReference,
            Amount = payment.Amount,
            Status = payment.Status,
            PaidAt = payment.PaidAt,
            CreatedAt = payment.CreatedAt
        };
    }

    private static void ValidateId(long id, string parameterName)
    {
        if (id <= 0)
            throw new ArgumentException(
                $"{parameterName} must be greater than zero.");
    }
}