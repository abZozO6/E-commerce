using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Enums;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }
    /// <summary>
    /// Retrieves an order by its unique identifier.
    /// Returns null if the order does not exist.
    /// </summary>
    public async Task<Order?> GetByIdAsync(long orderId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        SELECT OrderId, OrderNumber, UserId, CouponId,
               Status, PaymentStatus, Subtotal,
               DiscountAmount, ShippingCost, TotalAmount,
               ShippingFullName, ShippingPhone, ShippingCity,
               ShippingStreet, ShippingBuildingDetails, CreatedAt
        FROM dbo.Orders
        WHERE OrderId = @OrderId;
        """;

        return await connection.QuerySingleOrDefaultAsync<Order>(
            sql,
            new { OrderId = orderId });
    }

    public async Task<IEnumerable<Order>> GetByUserIdAsync(long userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderId, OrderNumber, UserId, CouponId,
            Status, PaymentStatus, Subtotal,
            DiscountAmount, ShippingCost, TotalAmount,
            ShippingFullName, ShippingPhone, ShippingCity,
            ShippingStreet, ShippingBuildingDetails, CreatedAt
            FROM dbo.Orders
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC;
            """;

        return await connection.QueryAsync<Order>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<Order>> GetAllAsync(string? status = null)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : NormalizeStatus(status);

        if (normalizedStatus is not null && !IsAllowedStatus(normalizedStatus))
            throw new ArgumentException("Unsupported order status.", nameof(status));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderId, OrderNumber, UserId, CouponId,
            Status, PaymentStatus, Subtotal,
            DiscountAmount, ShippingCost, TotalAmount,
            ShippingFullName, ShippingPhone, ShippingCity,
            ShippingStreet, ShippingBuildingDetails, CreatedAt
            FROM dbo.Orders
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC;
            """;

        return await connection.QueryAsync<Order>(
            sql, new { Status = normalizedStatus });
    }

    /// <summary>
    /// Inserts an order header and returns its generated ID.
    /// This method does not create order items or reserve stock, so checkout
    /// should use CreateOrderWithTransactionAsync instead.
    /// </summary>
    public async Task<long> CreateAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        ValidateOrderHeader(order);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.Orders (
                UserId, CouponId, Status, Subtotal, DiscountAmount,
                ShippingCost, TotalAmount, ShippingFullName, ShippingPhone,
                ShippingCity, ShippingStreet, ShippingBuildingDetails
            )
            VALUES (
                @UserId, @CouponId, @Status, @Subtotal, @DiscountAmount,
                @ShippingCost, @TotalAmount, @ShippingFullName, @ShippingPhone,
                @ShippingCity, @ShippingStreet, @ShippingBuildingDetails
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, order);
    }

    /// <summary>
    /// Creates an order and its items, validates current product data, reserves stock,
    /// records inventory movements, and optionally clears the matching user's cart.
    /// All database changes are committed together or rolled back together.
    /// </summary>
    public async Task<long> CreateOrderWithTransactionAsync(
        Order order,
        IEnumerable<OrderItem> items,
        IDictionary<long, byte[]> productRowVersions,
        long? cartIdToClear = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(productRowVersions);

        var requestedItems = items.ToList();

        ValidateOrderHeader(order);

        if (requestedItems.Count == 0)
            throw new ArgumentException("An order must contain at least one item.", nameof(items));

        if (requestedItems.Any(x => x is null))
            throw new ArgumentException("Order items cannot contain null entries.", nameof(items));

        if (requestedItems.Any(x => x.Quantity <= 0))
            throw new ArgumentException("Every item quantity must be greater than zero.", nameof(items));

        if (requestedItems.Select(x => x.ProductId).Distinct().Count() != requestedItems.Count)
            throw new ArgumentException(
                "Each product must appear only once. Combine quantities for duplicate products.",
                nameof(items));

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Lock and verify the cart, then ensure the submitted items match its current contents.
            if (cartIdToClear.HasValue)
            {
                const string cartSql = """
                    SELECT CartId
                    FROM dbo.Carts WITH (UPDLOCK, HOLDLOCK)
                    WHERE CartId = @CartId AND UserId = @UserId;
                    """;

                var ownedCartId = await connection.QuerySingleOrDefaultAsync<long?>(
                    cartSql,
                    new { CartId = cartIdToClear.Value, UserId = order.UserId },
                    transaction);

                if (!ownedCartId.HasValue)
                    throw new InvalidOperationException(
                        "The cart does not exist or does not belong to the order user.");

                const string cartItemsSql = """
                    SELECT ProductId, Quantity
                    FROM dbo.CartItems WITH (UPDLOCK, HOLDLOCK)
                    WHERE CartId = @CartId;
                    """;

                var cartItems = (await connection.QueryAsync<CartItemSnapshot>(
                    cartItemsSql,
                    new { CartId = cartIdToClear.Value },
                    transaction)).ToDictionary(x => x.ProductId, x => x.Quantity);

                var requestedQuantities = requestedItems.ToDictionary(x => x.ProductId, x => x.Quantity);

                if (cartItems.Count != requestedQuantities.Count ||
                    cartItems.Any(x => !requestedQuantities.TryGetValue(x.Key, out var quantity)
                                       || quantity != x.Value))
                {
                    throw new InvalidOperationException(
                        "The cart contents changed or do not match the requested order items. Refresh the cart and try again.");
                }
            }

            // Read current database prices and lock product rows until the transaction completes.
            var verifiedItems = new List<VerifiedOrderItem>(requestedItems.Count);

            foreach (var requested in requestedItems.OrderBy(x => x.ProductId))
            {
                const string productSql = """
                    SELECT ProductId, Name AS ProductName, SKU, Price AS UnitPrice,
                           StockQuantity, IsActive, RowVersion
                    FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
                    WHERE ProductId = @ProductId;
                    """;

                var product = await connection.QuerySingleOrDefaultAsync<ProductSnapshot>(
                    productSql,
                    new { requested.ProductId },
                    transaction);

                if (product is null)
                    throw new InvalidOperationException(
                        $"Product {requested.ProductId} does not exist.");

                if (!product.IsActive)
                    throw new InvalidOperationException(
                        $"Product '{product.ProductName}' is not available for purchase.");

                if (product.StockQuantity < requested.Quantity)
                    throw new InvalidOperationException(
                        $"Insufficient stock for product '{product.ProductName}'.");

                // Keep the existing interface contract and reject stale versions if supplied.
                if (!productRowVersions.TryGetValue(product.ProductId, out var expectedVersion) ||
                    expectedVersion is null ||
                    !expectedVersion.SequenceEqual(product.RowVersion))
                {
                    throw new InvalidOperationException(
                        $"Product '{product.ProductName}' changed after it was read. Refresh the product data and retry.");
                }

                verifiedItems.Add(new VerifiedOrderItem(
                    product.ProductId,
                    product.ProductName,
                    product.SKU,
                    product.UnitPrice,
                    requested.Quantity));
            }

            // Recalculate monetary totals using trusted database prices.
            // Never trust a discount amount supplied by the caller.
            var subtotal = verifiedItems.Sum(x => x.UnitPrice * x.Quantity);

            if (order.ShippingCost < 0)
                throw new InvalidOperationException("Shipping cost cannot be negative.");

            decimal discountAmount = 0;

            if (order.CouponId.HasValue)
            {
                // Lock the coupon row first. Concurrent checkouts using the same
                // coupon must wait until this transaction commits or rolls back.
                const string couponSql = """
                    SELECT
                        CouponId,
                        DiscountType,
                        DiscountValue,
                        MinOrderAmount,
                        MaxDiscountAmount,
                        StartsAt,
                        ExpiresAt,
                        UsageLimit,
                        IsActive
                    FROM dbo.Coupons WITH (UPDLOCK, HOLDLOCK)
                    WHERE CouponId = @CouponId;
                    """;

                var coupon = await connection.QuerySingleOrDefaultAsync<CouponSnapshot>(
                    couponSql,
                    new { CouponId = order.CouponId.Value },
                    transaction);

                if (coupon is null)
                    throw new InvalidOperationException("Coupon does not exist.");

                if (!coupon.IsActive)
                    throw new InvalidOperationException("Coupon is inactive.");

                // SQL Server DATETIME2 does not preserve DateTimeKind.
                // The application stores coupon dates in UTC, so mark the
                // values returned by Dapper as UTC before comparing them.
                var couponStartsAtUtc = DateTime.SpecifyKind(
                    coupon.StartsAt,
                    DateTimeKind.Utc);

                var couponExpiresAtUtc = DateTime.SpecifyKind(
                    coupon.ExpiresAt,
                    DateTimeKind.Utc);

                var now = DateTime.UtcNow;

                if (now < couponStartsAtUtc || now > couponExpiresAtUtc)
                    throw new InvalidOperationException("Coupon is outside its validity period.");

                if (subtotal < coupon.MinOrderAmount)
                    throw new InvalidOperationException(
                        "Order amount does not meet the coupon minimum.");

                // Count every order using this coupon except cancelled orders.
                // This query runs after acquiring the coupon row lock.
                const string usageCountSql = """
                    SELECT COUNT_BIG(*)
                    FROM dbo.Orders
                    WHERE CouponId = @CouponId
                      AND Status <> 'Cancelled';
                    """;

                var usageCount = await connection.ExecuteScalarAsync<long>(
                    usageCountSql,
                    new { CouponId = coupon.CouponId },
                    transaction);

                if (coupon.UsageLimit.HasValue && usageCount >= coupon.UsageLimit.Value)
                    throw new InvalidOperationException("Coupon usage limit has been reached.");

                if (coupon.DiscountValue <= 0)
                    throw new InvalidOperationException("Coupon discount value must be greater than zero.");

                if (coupon.DiscountType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
                {
                    if (coupon.DiscountValue > 100)
                        throw new InvalidOperationException("Percentage discount cannot exceed 100.");

                    discountAmount = subtotal * coupon.DiscountValue / 100m;
                }
                else if (coupon.DiscountType.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
                {
                    discountAmount = coupon.DiscountValue;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported coupon discount type.");
                }

                if (coupon.MaxDiscountAmount.HasValue)
                {
                    if (coupon.MaxDiscountAmount.Value <= 0)
                        throw new InvalidOperationException("Maximum discount amount must be greater than zero.");

                    discountAmount = Math.Min(discountAmount, coupon.MaxDiscountAmount.Value);
                }

                // A discount cannot exceed the order subtotal.
                discountAmount = Math.Min(discountAmount, subtotal);
                discountAmount = decimal.Round(
                    discountAmount,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            // Without a coupon, the discount is always zero.
            order.Subtotal = subtotal;
            order.DiscountAmount = discountAmount;
            order.TotalAmount = subtotal - discountAmount + order.ShippingCost;

            // New orders must start in Pending state; callers cannot choose an arbitrary initial status.
            order.Status = "Pending";

            // Insert the order header after product and cart validation.
            const string insertOrderSql = """
                INSERT INTO dbo.Orders (
                    UserId, CouponId, Status, Subtotal, DiscountAmount,
                    ShippingCost, TotalAmount, ShippingFullName, ShippingPhone,
                    ShippingCity, ShippingStreet, ShippingBuildingDetails
                )
                VALUES (
                    @UserId, @CouponId, @Status, @Subtotal, @DiscountAmount,
                    @ShippingCost, @TotalAmount, @ShippingFullName, @ShippingPhone,
                    @ShippingCity, @ShippingStreet, @ShippingBuildingDetails
                );

                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
                """;

            var orderId = await connection.ExecuteScalarAsync<long>(
                insertOrderSql, order, transaction);

            // Save each verified item, deduct its stock, and record the inventory movement.
            foreach (var item in verifiedItems)
            {
                var lineTotal = item.UnitPrice * item.Quantity;

                const string insertItemSql = """
                    INSERT INTO dbo.OrderItems (
                        OrderId, ProductId, ProductName, SKU,
                        UnitPrice, Quantity, LineTotal
                    )
                    VALUES (
                        @OrderId, @ProductId, @ProductName, @SKU,
                        @UnitPrice, @Quantity, @LineTotal
                    );
                    """;

                await connection.ExecuteAsync(
                    insertItemSql,
                    new
                    {
                        OrderId = orderId,
                        item.ProductId,
                        item.ProductName,
                        item.SKU,
                        item.UnitPrice,
                        item.Quantity,
                        LineTotal = lineTotal
                    },
                    transaction);

                // The conditional update is atomic; SQL Server updates RowVersion automatically.
                const string deductStockSql = """
                    UPDATE dbo.Products
                    SET StockQuantity = StockQuantity - @Quantity
                    WHERE ProductId = @ProductId
                      AND IsActive = 1
                      AND StockQuantity >= @Quantity
                      AND RowVersion = @RowVersion;
                    """;

                var rowsUpdated = await connection.ExecuteAsync(
                    deductStockSql,
                    new
                    {
                        item.ProductId,
                        item.Quantity,
                        RowVersion = productRowVersions[item.ProductId]
                    },
                    transaction);

                if (rowsUpdated != 1)
                    throw new InvalidOperationException(
                        $"Could not reserve stock for product '{item.ProductName}'. Retry the checkout.");

                // Use the actual column names from the database schema.
                const string insertInventorySql = """
                    INSERT INTO dbo.InventoryTransactions (
                        ProductId, ReferenceOrderId, QuantityChange,
                        TransactionType, Notes
                    )
                    VALUES (
                        @ProductId, @OrderId, @QuantityChange,
                        'Sale', @Notes
                    );
                    """;

                await connection.ExecuteAsync(
                    insertInventorySql,
                    new
                    {
                        item.ProductId,
                        OrderId = orderId,
                        QuantityChange = -item.Quantity,
                        Notes = $"Stock deducted for order {orderId}."
                    },
                    transaction);
            }

            // Clear only the verified cart belonging to this order's user.
            if (cartIdToClear.HasValue)
            {
                const string clearCartSql = """
                    DELETE FROM dbo.CartItems
                    WHERE CartId = @CartId;

                    UPDATE dbo.Carts
                    SET UpdatedAt = SYSUTCDATETIME()
                    WHERE CartId = @CartId AND UserId = @UserId;
                    """;

                await connection.ExecuteAsync(
                    clearCartSql,
                    new { CartId = cartIdToClear.Value, UserId = order.UserId },
                    transaction);
            }

            transaction.Commit();
            return orderId;
        }
        catch
        {
            // Roll back all changes and preserve the original exception.
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Do not hide the original exception if rollback also fails.
            }

            throw;
        }
    }

    /// <summary>
    /// Customer cancellation: cancels an eligible unpaid Pending order belonging to the customer,
    /// restores stock, records inventory return movements, and writes a customer audit log record atomically.
    /// Administrator cancellation must use AdminUpdateStatusAsync.
    /// </summary>
    public async Task<bool> CancelOrderWithTransactionAsync(long orderId, long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Atomically cancel only eligible unpaid orders belonging to this customer in Pending status.
            const string cancelOrderSql = """
                UPDATE dbo.Orders
                SET Status = 'Cancelled'
                WHERE OrderId = @OrderId
                  AND UserId = @UserId
                  AND Status = 'Pending'
                  AND PaymentStatus IN ('Pending', 'Failed');
                """;

            var rowsAffected = await connection.ExecuteAsync(
                cancelOrderSql,
                new { OrderId = orderId, UserId = userId },
                transaction);

            if (rowsAffected != 1)
            {
                transaction.Rollback();
                return false;
            }

            // Restore stock and record one positive inventory return movement per order item.
            const string restoreStockSql = """
                UPDATE p
                SET p.StockQuantity = p.StockQuantity + oi.Quantity
                FROM dbo.Products AS p
                INNER JOIN dbo.OrderItems AS oi
                    ON oi.ProductId = p.ProductId
                WHERE oi.OrderId = @OrderId;

                INSERT INTO dbo.InventoryTransactions (
                    ProductId, ReferenceOrderId, QuantityChange,
                    TransactionType, Notes
                )
                SELECT
                    ProductId, OrderId, Quantity,
                    'Return', @Notes
                FROM dbo.OrderItems
                WHERE OrderId = @OrderId;
                """;

            await connection.ExecuteAsync(
                restoreStockSql,
                new
                {
                    OrderId = orderId,
                    Notes = $"Stock restored after cancellation of order {orderId}."
                },
                transaction);

            // Record customer cancellation audit log in the same transaction.
            const string auditSql = """
                INSERT INTO dbo.OrderStatusAuditLogs
                    (OrderId, AdminUserId, CustomerUserId, OldStatus, NewStatus, Reason)
                VALUES
                    (@OrderId, NULL, @CustomerUserId, 'Pending', 'Cancelled', @Reason);
                """;

            await connection.ExecuteAsync(
                auditSql,
                new
                {
                    OrderId = orderId,
                    CustomerUserId = userId,
                    Reason = "Cancelled by customer."
                },
                transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }
    }

    /// <summary>
    /// Validates the order fields that must satisfy the database constraints.
    /// Coupon eligibility and shipping-price calculation must be validated by the service.
    /// </summary>
    private static void ValidateOrderHeader(Order order)
    {
        if (order.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(order.UserId));

        if (order.DiscountAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(order.DiscountAmount));

        if (order.ShippingCost < 0)
            throw new ArgumentOutOfRangeException(nameof(order.ShippingCost));

        if (string.IsNullOrWhiteSpace(order.ShippingFullName))
            throw new ArgumentException("Shipping name is required.");

        if (string.IsNullOrWhiteSpace(order.ShippingPhone))
            throw new ArgumentException("Shipping phone is required.");

        if (string.IsNullOrWhiteSpace(order.ShippingCity))
            throw new ArgumentException("Shipping city is required.");

        if (string.IsNullOrWhiteSpace(order.ShippingStreet))
            throw new ArgumentException("Shipping street is required.");
    }

    /// <summary>
    /// Normalizes incoming status strings to canonical PascalCase values.
    /// Unrecognized values are returned trimmed for subsequent validation.
    /// </summary>
    public static string NormalizeStatus(string status) => Order.NormalizeStatus(status);


    private static bool IsAllowedStatus(string status) => Order.IsAllowedStatus(status);

    private sealed class ProductSnapshot
    {
        public long ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string SKU { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public int StockQuantity { get; init; }
        public bool IsActive { get; init; }
        public byte[] RowVersion { get; init; } = [];
    }


    private sealed class CartItemSnapshot
    {
        public long ProductId { get; init; }
        public int Quantity { get; init; }
    }

    private sealed record VerifiedOrderItem(
        long ProductId,
        string ProductName,
        string SKU,
        decimal UnitPrice,
        int Quantity);

    private sealed class CouponSnapshot
    {
        public long CouponId { get; init; }
        public string DiscountType { get; init; } = string.Empty;
        public decimal DiscountValue { get; init; }
        public decimal MinOrderAmount { get; init; }
        public decimal? MaxDiscountAmount { get; init; }
        public DateTime StartsAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public int? UsageLimit { get; init; }
        public bool IsActive { get; init; }
    }



    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Confirmed", "Cancelled" },
            ["Confirmed"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Processing", "Cancelled" },
            ["Processing"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Shipped", "Cancelled" },
            ["Shipped"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Delivered", "Returned" },
            ["Delivered"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Returned" },
            ["Cancelled"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { },
            ["Returned"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { },
        };

    /// <summary>
    /// Status transitions that require PaymentStatus = 'Succeeded'.
    /// </summary>
    private static readonly IReadOnlySet<string> PaymentRequiredTargets =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Confirmed", "Processing", "Shipped", "Delivered"
        };

    public async Task<OrderStatusUpdateResult> AdminUpdateStatusAsync(
        long orderId,
        string newStatus,
        string reason,
        long adminUserId)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        if (string.IsNullOrWhiteSpace(newStatus))
            throw new ArgumentException("New status is required.", nameof(newStatus));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        if (adminUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(adminUserId));

        var normalizedNew = NormalizeStatus(newStatus);

        if (!IsAllowedStatus(normalizedNew))
            throw new ArgumentException("Unsupported order status.", nameof(newStatus));

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Lock the order row for the duration of this transaction.
            const string selectSql = """
                SELECT OrderId, Status, PaymentStatus
                FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK)
                WHERE OrderId = @OrderId;
                """;

            var snapshot = await connection.QuerySingleOrDefaultAsync<OrderSnapshot>(
                selectSql,
                new { OrderId = orderId },
                transaction);

            if (snapshot is null)
            {
                transaction.Rollback();
                return OrderStatusUpdateResult.NotFound;
            }

            var currentStatus = snapshot.Status;
            var paymentStatus = snapshot.PaymentStatus;

            // No-op when the requested status equals the current status.
            if (string.Equals(currentStatus, normalizedNew, StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return OrderStatusUpdateResult.NoChange;
            }

            // Validate that the transition is allowed from the current status.
            if (!AllowedTransitions.TryGetValue(currentStatus, out var allowed)
                || !allowed.Contains(normalizedNew))
            {
                transaction.Rollback();
                return OrderStatusUpdateResult.InvalidTransition;
            }

            if (PaymentRequiredTargets.Contains(normalizedNew)
                && !string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return OrderStatusUpdateResult.PaymentRequired;
            }

            if (string.Equals(normalizedNew, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(paymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase)))
            {
                transaction.Rollback();
                return OrderStatusUpdateResult.RefundRequired;
            }

            if (string.Equals(normalizedNew, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                const string restoreStockSql = """
                    UPDATE p
                    SET p.StockQuantity = p.StockQuantity + oi.Quantity
                    FROM dbo.Products AS p
                    INNER JOIN dbo.OrderItems AS oi
                        ON oi.ProductId = p.ProductId
                    WHERE oi.OrderId = @OrderId;

                    INSERT INTO dbo.InventoryTransactions (
                        ProductId, ReferenceOrderId, QuantityChange,
                        TransactionType, Notes
                    )
                    SELECT
                        ProductId, OrderId, Quantity,
                        'Return', @Notes
                    FROM dbo.OrderItems
                    WHERE OrderId = @OrderId;
                    """;

                await connection.ExecuteAsync(
                    restoreStockSql,
                    new
                    {
                        OrderId = orderId,
                        Notes = $"Stock restored after cancellation of order {orderId}."
                    },
                    transaction);
            }

            // Apply the status change.
            const string updateSql = """
                UPDATE dbo.Orders
                SET Status = @NewStatus
                WHERE OrderId = @OrderId;
                """;

            await connection.ExecuteAsync(
                updateSql,
                new { NewStatus = normalizedNew, OrderId = orderId },
                transaction);

            // Insert the audit record in the same transaction.
            const string auditSql = """
                INSERT INTO dbo.OrderStatusAuditLogs
                    (OrderId, AdminUserId, CustomerUserId, OldStatus, NewStatus, Reason)
                VALUES
                    (@OrderId, @AdminUserId, NULL, @OldStatus, @NewStatus, @Reason);
                """;

            await connection.ExecuteAsync(
                auditSql,
                new
                {
                    OrderId = orderId,
                    AdminUserId = adminUserId,
                    OldStatus = currentStatus,
                    NewStatus = normalizedNew,
                    Reason = trimmedReason
                },
                transaction);

            transaction.Commit();
            return OrderStatusUpdateResult.Updated;
        }
        catch
        {
            try { transaction.Rollback(); } catch { /* Preserve original exception. */ }
            throw;
        }
    }

    private sealed class OrderSnapshot
    {
        public long OrderId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
    }

}
