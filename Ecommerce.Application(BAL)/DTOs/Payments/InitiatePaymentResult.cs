namespace Ecommerce.Application_BAL_.DTOs.Payments;

public class InitiatePaymentResult
{
    public long PaymentId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ProviderSessionId { get; set; }
}
