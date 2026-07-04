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
    private readonly ICreateOrderUseCase _createOrder;
    private readonly IGetMyOrdersUseCase _getMyOrders;
    private readonly IGetOrderByIdUseCase _getOrderById;
    private readonly IGetOrdersUseCase _getOrders;
    private readonly IUpdateOrderStatusUseCase _updateOrderStatus;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(
        ICreateOrderUseCase createOrder,
        IGetMyOrdersUseCase getMyOrders,
        IGetOrderByIdUseCase getOrderById,
        IGetOrdersUseCase getOrders,
        IUpdateOrderStatusUseCase updateOrderStatus,
        ICurrentUserService currentUser)
    {
        _createOrder = createOrder;
        _getMyOrders = getMyOrders;
        _getOrderById = getOrderById;
        _getOrders = getOrders;
        _updateOrderStatus = updateOrderStatus;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _createOrder.ExecuteAsync(_currentUser.UserId, request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : ToErrorResponse(result.Error!);
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
    public async Task<ActionResult<List<OrderSummaryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _getOrders.ExecuteAsync(cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateOrderStatus.ExecuteAsync(id, request, cancellationToken));
    }
}
