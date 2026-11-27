namespace Ecommerce.Core.Interfaces;

/// <summary>
/// Abstraction over an external (or simulated) payment provider.
/// Inject this interface to decouple business logic from any specific provider SDK.
/// Replace <see cref="Ecommerce.Infrastructure_DAL_.Providers.SimulatedPaymentProvider"/>
/// with a real implementation when a provider is selected.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Returns the canonical provider name that is stored in <c>Payments.Provider</c>.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Creates a payment session / checkout URL for the given order.
    /// Returns a <see cref="ProviderSessionResult"/> with a redirect URL the customer
    /// visits to complete payment, and a provider-generated session identifier.
    /// </summary>
    Task<ProviderSessionResult> CreateSessionAsync(
        long orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the cryptographic signature of an inbound webhook notification and
    /// extracts the payment event details.
    /// Must be called BEFORE <see cref="Ecommerce.Application_BAL_.Interfaces.IPaymentService.HandleVerifiedCallbackAsync"/>
    /// to prevent replay or forged webhook attacks.
    /// </summary>
    Task<ProviderNotificationResult> VerifyNotificationAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Instructs the provider to issue a full refund for the given provider reference.
    /// </summary>
    Task<ProviderRefundResult> RefundAsync(
        string providerReference,
        decimal amount,
        CancellationToken cancellationToken = default);
}

/// <summary>Returned by <see cref="IPaymentProvider.CreateSessionAsync"/>.</summary>
public sealed class ProviderSessionResult
{
    /// <summary>Whether the session was created successfully.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// URL the customer should be redirected to in order to complete payment.
    /// <c>null</c> when <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public string? CheckoutUrl { get; init; }

    /// <summary>
    /// Provider-assigned session / payment identifier.
    /// Used to correlate the webhook notification back to this payment record.
    /// <c>null</c> when <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public string? ProviderSessionId { get; init; }

    /// <summary>Human-readable error message when <see cref="Success"/> is <c>false</c>.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Returned by <see cref="IPaymentProvider.VerifyNotificationAsync"/>.</summary>
public sealed class ProviderNotificationResult
{
    /// <summary>Whether the signature was valid and the payload was parsed successfully.</summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Internal PaymentId if included in the webhook payload.
    /// </summary>
    public long? PaymentId { get; init; }

    /// <summary>
    /// Payment identifier from the notification payload (maps to <c>Payments.ProviderReference</c>).
    /// </summary>
    public string? ProviderReference { get; init; }

    /// <summary>
    /// Payment outcome status as reported by the provider.
    /// Expected values: <c>Succeeded</c>, <c>Failed</c>, <c>Cancelled</c>, <c>Refunded</c>.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>Human-readable reason when <see cref="IsVerified"/> is <c>false</c>.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Returned by <see cref="IPaymentProvider.RefundAsync"/>.</summary>
public sealed class ProviderRefundResult
{
    /// <summary>Whether the refund was accepted by the provider.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// Provider-assigned refund reference, if available.
    /// <c>null</c> when <see cref="Success"/> is <c>false</c> or not provided.
    /// </summary>
    public string? RefundReference { get; init; }

    /// <summary>Human-readable error message when <see cref="Success"/> is <c>false</c>.</summary>
    public string? ErrorMessage { get; init; }
}
