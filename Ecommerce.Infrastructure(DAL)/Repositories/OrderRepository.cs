using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }
    public async Task<Order?> GetByIdAsync(long orderId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderId, UserId, CouponId, Status, Subtotal AS SubTotal,
                   DiscountAmount, ShippingCost, TotalAmount,
                   ShippingFullName, ShippingPhone, ShippingCity,
                   ShippingStreet, ShippingBuildingDetails, CreatedAt
            FROM dbo.Orders
            WHERE OrderId = @OrderId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Order>(
            sql, new { OrderId = orderId });
    }

    public async Task<IEnumerable<Order>> GetByUserIdAsync(long userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderId, UserId, CouponId, Status, Subtotal AS SubTotal,
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
            : status.Trim();

        if (normalizedStatus is not null && !IsAllowedStatus(normalizedStatus))
            throw new ArgumentException("Unsupported order status.", nameof(status));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderId, UserId, CouponId, Status, Subtotal AS SubTotal,
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
                @UserId, @CouponId, @Status, @SubTotal, @DiscountAmount,
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

            // Recalculate monetary totals using database prices, not client-supplied item prices.
            var subtotal = verifiedItems.Sum(x => x.UnitPrice * x.Quantity);

            if (order.DiscountAmount < 0 || order.DiscountAmount > subtotal)
                throw new InvalidOperationException("The discount amount is invalid for this order.");

            if (order.ShippingCost < 0)
                throw new InvalidOperationException("Shipping cost cannot be negative.");

            order.Subtotal = subtotal;
            order.TotalAmount = subtotal - order.DiscountAmount + order.ShippingCost;

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
                    @UserId, @CouponId, @Status, @SubTotal, @DiscountAmount,
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
    /// Updates an order status only when the requested transition is allowed.
    /// Ownership and administrator authorization must be enforced by the service/controller.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(long orderId, string newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus) || !IsAllowedStatus(newStatus.Trim()))
            throw new ArgumentException("Unsupported order status.", nameof(newStatus));

        var status = newStatus.Trim();

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Orders
            SET Status = @Status
            WHERE OrderId = @OrderId
              AND (
                    (Status = 'Pending' AND @Status IN ('Confirmed', 'Processing', 'Cancelled'))
                 OR (Status = 'Confirmed' AND @Status IN ('Processing', 'Cancelled'))
                 OR (Status = 'Processing' AND @Status IN ('Shipped', 'Cancelled'))
                 OR (Status = 'Shipped' AND @Status IN ('Delivered', 'Returned'))
                 OR (Status = 'Delivered' AND @Status = 'Returned')
              );
            """;

        var rowsAffected = await connection.ExecuteAsync(
            sql, new { OrderId = orderId, Status = status });

        return rowsAffected == 1;
    }

    /// <summary>
    /// Cancels an eligible unpaid order, restores stock, and records return movements.
    /// Only the owner or an administrator may cancel; authorization must be based on trusted identity.
    /// Paid orders require a separate payment-refund workflow.
    /// </summary>
    public async Task<bool> CancelOrderWithTransactionAsync(long orderId,long userId,bool isAdmin = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Atomically cancel only eligible orders that have not been paid.
            const string cancelOrderSql = """
                UPDATE dbo.Orders
                SET Status = 'Cancelled'
                WHERE OrderId = @OrderId
                  AND (@IsAdmin = 1 OR UserId = @UserId)
                  AND Status IN ('Pending', 'Processing')
                  AND PaymentStatus IN ('Pending', 'Failed');
                """;

            var rowsAffected = await connection.ExecuteAsync(
                cancelOrderSql,
                new { OrderId = orderId, UserId = userId, IsAdmin = isAdmin },
                transaction);

            if (rowsAffected != 1)
            {
                transaction.Rollback();
                return false;
            }

            // Restore stock and record one positive inventory movement per order item.
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

            transaction.Commit();
            return true;
        }
        catch
        {
            // Roll back the status change and stock restoration together.
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
    /// Checks whether a status is one of the values supported by the database schema.
    /// </summary>
    private static bool IsAllowedStatus(string status) =>
        status is "Pending" or "Confirmed" or "Processing"
            or "Shipped" or "Delivered" or "Cancelled" or "Returned";

    /// <summary>
    /// Stores the product data retrieved from the database during checkout.
    /// This allows us to validate the product's price, stock, active status,
    /// and RowVersion before creating the order and updating inventory.
    /// </summary>
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

    /// <summary>
    /// Stores the product IDs and quantities currently in the cart.
    /// We use it to compare the actual cart contents with the requested order items
    /// and help prevent creating an order with mismatched products or quantities.
    /// </summary>

    private sealed class CartItemSnapshot
    {
        public long ProductId { get; init; }
        public int Quantity { get; init; }
    }

    /// <summary>
    /// Represents an order item after its data has been verified.
    /// It uses trusted product details from the database instead of relying on
    /// client-provided prices, names, or SKUs before inserting the item into the order.
    /// </summary>

    private sealed record VerifiedOrderItem(
        long ProductId,
        string ProductName,
        string SKU,
        decimal UnitPrice,
        int Quantity);

}
