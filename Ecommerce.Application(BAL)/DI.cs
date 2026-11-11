using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Application_BAL_.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_
{
    public static class DI
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IBrandService, BrandService>();

            services.AddScoped<ICategoryService, CategoryService>();

            services.AddScoped<IProductService, ProductService>();

            services.AddScoped<IProductImageService, ProductImageService>();

            services.AddScoped<IProductSpecificationService,ProductSpecificationService>();

            services.AddScoped<IProductReviewService, ProductReviewService>();

            services.AddScoped<IAddressService, AddressService>();

            services.AddScoped<ICartService, CartService>();

            services.AddScoped<ICartItemService, CartItemService>();

            services.AddScoped<ICouponService, CouponService>();

            services.AddScoped<IOrderService, OrderService>();

            services.AddScoped<IInventoryTransactionService, InventoryTransactionService>();

            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }


    }
}
