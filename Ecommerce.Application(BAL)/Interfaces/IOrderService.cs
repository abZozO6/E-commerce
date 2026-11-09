using Ecommerce.Application_BAL_.DTOs.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.Interfaces
{
    public interface IOrderService
    {
        // Customer
        Task<IEnumerable<OrderDto>> GetMyOrdersAsync(long userId);

        Task<OrderDto?> GetMyOrderByIdAsync(long orderId,long userId);

        Task<long> CreateAsync(long userId,CreateOrderDto dto);

        Task<bool> CancelMyOrderAsync(long orderId,long userId);


        // Admin
        Task<IEnumerable<OrderDto>> GetAllOrdersAsync(string? status = null);

        Task<OrderDto?> GetOrderByIdAsync(long orderId);

        Task<bool> UpdateStatusAsync(long orderId,UpdateOrderStatusDto dto);

        Task<bool> CancelOrderAsync(long orderId);
    }
}
