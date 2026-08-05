using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.UseCases.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ApiControllerBase
{
    private readonly IGetMyOrdersUseCase _getMyOrders;
    private readonly IGetOrderByIdUseCase _getOrderById;
    private readonly IGetOrdersUseCase _getOrders;
    private readonly IUpdateOrderStatusUseCase _updateOrderStatus;
    private readonly ICancelOrderUseCase _cancelOrder;
    private readonly IExpirePendingOrdersUseCase _expirePendingOrders;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(
        IGetMyOrdersUseCase getMyOrders,
        IGetOrderByIdUseCase getOrderById,
        IGetOrdersUseCase getOrders,
        IUpdateOrderStatusUseCase updateOrderStatus,
        ICancelOrderUseCase cancelOrder,
        IExpirePendingOrdersUseCase expirePendingOrders,
        ICurrentUserService currentUser)
    {
        _getMyOrders = getMyOrders;
        _getOrderById = getOrderById;
        _getOrders = getOrders;
        _updateOrderStatus = updateOrderStatus;
        _cancelOrder = cancelOrder;
        _expirePendingOrders = expirePendingOrders;
        _currentUser = currentUser;
    }


    [HttpGet("my")]
    public async Task<ActionResult<List<OrderSummaryResponse>>> GetMyOrders(CancellationToken cancellationToken)
    {
        return Ok(await _getMyOrders.ExecuteAsync(_currentUser.UserId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var canViewAll = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return FromResult(await _getOrderById.ExecuteAsync(id, _currentUser.UserId, canViewAll, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet]
    public async Task<ActionResult<List<OrderSummaryResponse>>> GetAll([FromQuery] string? userEmail, CancellationToken cancellationToken)
    {
        return Ok(await _getOrders.ExecuteAsync(new OrderFilterRequest(userEmail), cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateOrderStatus.ExecuteAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Cancel(int id, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        var canManageAll = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return FromResult(await _cancelOrder.ExecuteAsync(id, _currentUser.UserId, canManageAll, request, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost("expire-pending")]
    public async Task<ActionResult<ExpirePendingOrdersResponse>> ExpirePending(ExpirePendingOrdersRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _expirePendingOrders.ExecuteAsync(request, cancellationToken));
    }
}


