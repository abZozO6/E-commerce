using Ecommerce.Application_BAL_.DTOs.Orders;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

/// <summary>
/// Handles order retrieval, checkout, status updates, and cancellation.
/// Authorization for administrator-only methods must also be enforced by the API.
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly ICartRepository _cartRepository;
    private readonly ICartItemRepository _cartItemRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICouponRepository _couponRepository;

    public OrderService(
        IOrderRepository orderRepository,
        IOrderItemRepository orderItemRepository,
        IUserRepository userRepository,
        IAddressRepository addressRepository,
        ICartRepository cartRepository,
        ICartItemRepository cartItemRepository,
        IProductRepository productRepository,
        ICouponRepository couponRepository)
    {
        _orderRepository = orderRepository
            ?? throw new ArgumentNullException(nameof(orderRepository));
        _orderItemRepository = orderItemRepository
            ?? throw new ArgumentNullException(nameof(orderItemRepository));
        _userRepository = userRepository
            ?? throw new ArgumentNullException(nameof(userRepository));
        _addressRepository = addressRepository
            ?? throw new ArgumentNullException(nameof(addressRepository));
        _cartRepository = cartRepository
            ?? throw new ArgumentNullException(nameof(cartRepository));
        _cartItemRepository = cartItemRepository
            ?? throw new ArgumentNullException(nameof(cartItemRepository));
        _productRepository = productRepository
            ?? throw new ArgumentNullException(nameof(productRepository));
        _couponRepository = couponRepository
            ?? throw new ArgumentNullException(nameof(couponRepository));
    }

    // Customer: retrieve the current user's orders.
    public async Task<IEnumerable<OrderDto>> GetMyOrdersAsync(long userId)
    {
        ValidateId(userId, nameof(userId));
        await EnsureUserExistsAsync(userId);

        var orders = await _orderRepository.GetByUserIdAsync(userId);
        var result = new List<OrderDto>();

        foreach (var order in orders)
            result.Add(await MapToDtoAsync(order));

        return result;
    }

    // Customer: retrieve one order only when it belongs to the current user.
    public async Task<OrderDto?> GetMyOrderByIdAsync(long orderId, long userId)
    {
        ValidateId(orderId, nameof(orderId));
        ValidateId(userId, nameof(userId));
        await EnsureUserExistsAsync(userId);

        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null || order.UserId != userId)
            return null;

        return await MapToDtoAsync(order);
    }

    // Admin: retrieve all orders, optionally filtered by status.
    // Protect this method with [Authorize(Roles = "Admin")] at the API layer.
    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(string? status = null)
    {
        var orders = await _orderRepository.GetAllAsync(status);
        var result = new List<OrderDto>();

        foreach (var order in orders)
            result.Add(await MapToDtoAsync(order));

        return result;
    }

    // Admin: retrieve any order by ID.
    // Protect this method with [Authorize(Roles = "Admin")] at the API layer.
    public async Task<OrderDto?> GetOrderByIdAsync(long orderId)
    {
        ValidateId(orderId, nameof(orderId));

        var order = await _orderRepository.GetByIdAsync(orderId);
        return order is null ? null : await MapToDtoAsync(order);
    }

    /// <summary>
    /// Creates an order from the authenticated user's cart.
    /// Prices, quantities, product names, SKU, and discount totals are not trusted
    /// from the client; the repository verifies the critical values in one transaction.
    /// </summary>
    public async Task<long> CreateAsync(long userId, CreateOrderDto dto)
    {
        ValidateId(userId, nameof(userId));
        ArgumentNullException.ThrowIfNull(dto);
        ValidateId(dto.AddressId, nameof(dto.AddressId));

        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null || user.IsDeleted || !user.IsActive)
            throw new InvalidOperationException("User does not exist or is inactive.");

        if (string.IsNullOrWhiteSpace(user.FullName))
            throw new InvalidOperationException("The user's full name is required to create an order.");

        // The address must exist and belong to the authenticated user.
        var address = await _addressRepository.GetByIdAsync(dto.AddressId);

        if (address is null || address.UserId != userId)
            throw new KeyNotFoundException("Shipping address was not found.");

        // A customer can check out only the cart associated with their own account.
        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart is null)
            throw new InvalidOperationException("Your cart is empty.");

        var cartItems = (await _cartItemRepository.GetByCartIdAsync(cart.CartId)).ToList();

        if (cartItems.Count == 0)
            throw new InvalidOperationException("Your cart is empty.");

        var orderItems = new List<OrderItem>(cartItems.Count);
        var productRowVersions = new Dictionary<long, byte[]>(cartItems.Count);

        foreach (var cartItem in cartItems)
        {
            if (cartItem.Quantity <= 0)
                throw new InvalidOperationException("The cart contains an invalid quantity.");

            var product = await _productRepository.GetByIdAsync(cartItem.ProductId);

            if (product is null || !product.IsActive)
                throw new InvalidOperationException(
                    $"Product with ID {cartItem.ProductId} is unavailable.");

            if (product.StockQuantity < cartItem.Quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock for product '{product.Name}'.");

            if (product.RowVersion is null || product.RowVersion.Length == 0)
                throw new InvalidOperationException(
                    $"Could not verify the current version of product '{product.Name}'.");

            orderItems.Add(new OrderItem
            {
                ProductId = product.ProductId,
                ProductName = product.Name,
                SKU = product.SKU,
                UnitPrice = product.Price,
                Quantity = cartItem.Quantity,
                LineTotal = product.Price * cartItem.Quantity
            });

            productRowVersions.Add(product.ProductId, product.RowVersion);
        }

        // Resolve the code to an ID. Coupon eligibility, usage limit, and discount
        // calculation are checked again atomically inside CreateOrderWithTransactionAsync.
        long? couponId = null;

        if (!string.IsNullOrWhiteSpace(dto.CouponCode))
        {
            var normalizedCode = dto.CouponCode.Trim().ToUpperInvariant();
            var coupon = await _couponRepository.GetByCodeAsync(normalizedCode);

            if (coupon is null)
                throw new InvalidOperationException("Coupon code is invalid.");

            couponId = coupon.CouponId;
        }

        var order = new Order
        {
            OrderNumber = Guid.NewGuid(),
            UserId = userId,
            CouponId = couponId,
            Status = "Pending",
            PaymentStatus = "Pending",
            Subtotal = orderItems.Sum(x => x.UnitPrice * x.Quantity),
            DiscountAmount = 0,
            ShippingCost = 0, // Replace when the store's shipping policy is defined.
            ShippingFullName = user.FullName,
            ShippingPhone = address.Phone,
            ShippingCity = address.City,
            ShippingStreet = address.Street,
            ShippingBuildingDetails = address.BuildingDetails
        };

        return await _orderRepository.CreateOrderWithTransactionAsync(
            order,
            orderItems,
            productRowVersions,
            cart.CartId);
    }

    // Customer: may cancel only their own Pending order.
    // The repository additionally checks payment state and performs stock restoration atomically.
    public async Task<bool> CancelMyOrderAsync(long orderId, long userId)
    {
        ValidateId(orderId, nameof(orderId));
        ValidateId(userId, nameof(userId));
        await EnsureUserExistsAsync(userId);

        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null || order.UserId != userId)
            return false;

        if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return false;

        return await _orderRepository.CancelOrderWithTransactionAsync(
            orderId,
            userId,
            isAdmin: false);
    }

    // Admin: may cancel eligible Pending, Confirmed, or Processing orders.
    // Protect this method with [Authorize(Roles = "Admin")] at the API layer.
    public async Task<bool> CancelOrderAsync(long orderId)
    {
        ValidateId(orderId, nameof(orderId));

        // userId is ignored by the repository when isAdmin is true.
        return await _orderRepository.CancelOrderWithTransactionAsync(
            orderId,
            userId: 0,
            isAdmin: true);
    }

    // Admin: update status according to the transition rules enforced by the repository.
    // Protect this method with [Authorize(Roles = "Admin")] at the API layer.
    public async Task<bool> UpdateStatusAsync(long orderId, UpdateOrderStatusDto dto)
    {
        ValidateId(orderId, nameof(orderId));
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.NewStatus))
            throw new ArgumentException("New status is required.", nameof(dto.NewStatus));

        var newStatus = dto.NewStatus.Trim();

        if (newStatus.Length > 20)
            throw new ArgumentException("Status cannot exceed 20 characters.", nameof(dto.NewStatus));

        return await _orderRepository.UpdateStatusAsync(orderId, newStatus);
    }

    private async Task EnsureUserExistsAsync(long userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null || user.IsDeleted)
            throw new KeyNotFoundException("User not found.");
    }

    private async Task<OrderDto> MapToDtoAsync(Order order)
    {
        var items = await _orderItemRepository.GetByOrderIdAsync(order.OrderId);

        return new OrderDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            CouponId = order.CouponId,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            ShippingCost = order.ShippingCost,
            TotalAmount = order.TotalAmount,
            ShippingFullName = order.ShippingFullName,
            ShippingPhone = order.ShippingPhone,
            ShippingCity = order.ShippingCity,
            ShippingStreet = order.ShippingStreet,
            ShippingBuildingDetails = order.ShippingBuildingDetails,
            CreatedAt = order.CreatedAt,
            Items = items.Select(item => new OrderItemDto
            {
                OrderItemId = item.OrderItemId,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                SKU = item.SKU,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            }).ToList()
        };
    }

    private static void ValidateId(long id, string parameterName)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "ID must be greater than zero.");
    }
}
