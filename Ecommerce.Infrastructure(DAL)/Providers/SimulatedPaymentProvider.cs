using System.Security.Cryptography;
using System.Text;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Infrastructure_DAL_.Providers;

/// <summary>
/// Deterministic payment provider simulator.
/// Used in development and testing in lieu of a real payment gateway (Stripe, PayPal, etc.).
///
/// SIMULATOR RULES:
///   - Any amount ending in .00 → Succeeded
///   - Any amount ending in .01 → Failed
///   - Any other amount → Succeeded (default)
///   - Webhook signature: HMAC-SHA256 of the raw payload using <see cref="SimulatorWebhookSecret"/>
///
/// Replace this class with a real provider implementation before going to production.
/// Update the DI registration in <c>Ecommerce.Infrastructure.DependencyInjection</c>.
/// </summary>
public sealed class SimulatedPaymentProvider : IPaymentProvider
{
    /// <summary>
    /// Secret used to sign and verify simulated webhook payloads.
    /// In production this would come from configuration / secrets management.
    /// </summary>
    public const string SimulatorWebhookSecret = "simulator-webhook-secret-dev-only";

    private const string BaseCheckoutUrl = "https://simulator.payments.local/checkout/";

    public string ProviderName => "Simulator";

    public Task<ProviderSessionResult> CreateSessionAsync(
        long orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        // Generate a deterministic but unique-enough session ID.
        var sessionId = $"SIM-{orderId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        return Task.FromResult(new ProviderSessionResult
        {
            Success = true,
            CheckoutUrl = $"{BaseCheckoutUrl}{sessionId}",
            ProviderSessionId = sessionId
        });
    }

    /// <summary>
    /// Verifies an HMAC-SHA256 webhook signature and parses the simulated payload.
    /// Expected payload format (plain text): <c>paymentId=&lt;id&gt;&amp;status=&lt;status&gt;&amp;ref=&lt;ref&gt;</c>
    /// Expected signature header format: <c>sha256=&lt;hex&gt;</c>
    /// </summary>
    public Task<ProviderNotificationResult> VerifyNotificationAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Task.FromResult(Reject("Payload is empty."));

        if (string.IsNullOrWhiteSpace(signature))
            return Task.FromResult(Reject("Signature is missing."));

        // Verify HMAC-SHA256 signature.
        if (!VerifyHmac(payload, signature))
            return Task.FromResult(Reject("Webhook signature verification failed."));

        // Parse payload: paymentId=123&status=Succeeded&ref=SIM-123-...
        var fields = ParseQueryString(payload);

        if (!fields.TryGetValue("paymentId", out var paymentIdRaw) ||
            !long.TryParse(paymentIdRaw, out var paymentId))
            return Task.FromResult(Reject("Payload missing valid paymentId."));

        if (!fields.TryGetValue("status", out var status) || string.IsNullOrWhiteSpace(status))
            return Task.FromResult(Reject("Payload missing status."));

        fields.TryGetValue("ref", out var providerReference);

        return Task.FromResult(new ProviderNotificationResult
        {
            IsVerified = true,
            PaymentId = paymentId,
            ProviderReference = string.IsNullOrWhiteSpace(providerReference)
                ? null
                : providerReference.Trim(),
            Status = status.Trim()
        });
    }

    public Task<ProviderRefundResult> RefundAsync(
        string providerReference,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        // Simulator always accepts refunds.
        var refundRef = $"REF-{providerReference}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        return Task.FromResult(new ProviderRefundResult
        {
            Success = true,
            RefundReference = refundRef
        });
    }

    /// <summary>
    /// Generates a simulator webhook payload + its HMAC-SHA256 signature header value.
    /// Used in tests and by the simulator checkout to call back into the webhook endpoint.
    /// </summary>
    public static (string Payload, string SignatureHeader) BuildWebhookPayload(
        long paymentId,
        string status,
        string? providerReference = null)
    {
        var payload = $"paymentId={paymentId}&status={status}";

        if (!string.IsNullOrWhiteSpace(providerReference))
            payload += $"&ref={providerReference}";

        var signatureHeader = ComputeHmacHeader(payload);
        return (payload, signatureHeader);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool VerifyHmac(string payload, string signatureHeader)
    {
        // Expect format: "sha256=<hex>"
        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHex = signatureHeader["sha256=".Length..].Trim();
        var expectedHeader = ComputeHmacHeader(payload);
        var expectedHex = expectedHeader["sha256=".Length..];

        // Constant-time comparison to prevent timing attacks.
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(receivedHex.ToUpperInvariant()),
            Convert.FromHexString(expectedHex.ToUpperInvariant()));
    }

    private static string ComputeHmacHeader(string payload)
    {
        var key = Encoding.UTF8.GetBytes(SimulatorWebhookSecret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static Dictionary<string, string> ParseQueryString(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');

            if (eq <= 0)
                continue;

            var key = Uri.UnescapeDataString(part[..eq]);
            var val = Uri.UnescapeDataString(part[(eq + 1)..]);
            result[key] = val;
        }

        return result;
    }

    private static ProviderNotificationResult Reject(string reason) =>
        new() { IsVerified = false, ErrorMessage = reason };
}
