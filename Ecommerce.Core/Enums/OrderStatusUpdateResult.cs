namespace Ecommerce.Core.Enums;

public enum OrderStatusUpdateResult
{
    /// <summary>The order status was updated and an audit record was created.</summary>
    Updated,

    /// <summary>No order with the given ID exists.</summary>
    NotFound,

    /// <summary>The requested new status equals the current status; no change was made.</summary>
    NoChange,

    /// <summary>The requested transition is not permitted from the current status.</summary>
    InvalidTransition,

    /// <summary>
    /// The transition requires the order's payment to be in Paid state,
    /// but the payment has not been completed yet.
    /// </summary>
    PaymentRequired,

    /// <summary>
    /// Cancellation was attempted on a paid or partially refunded order;
    /// a full refund must be issued first via the payment management flow.
    /// </summary>
    RefundRequired
}
