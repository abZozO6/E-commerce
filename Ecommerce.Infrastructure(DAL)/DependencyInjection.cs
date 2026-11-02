using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure.Repositories;
using Ecommerce.Infrastructure_DAL_.Data;
using Ecommerce.Infrastructure_DAL_.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services and repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();

        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<ICartItemRepository, CartItemRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
        services.AddScoped<IOrderItemRepository, OrderItemRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<IProductSpecificationRepository, ProductSpecificationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}