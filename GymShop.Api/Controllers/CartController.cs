using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Carts;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.UseCases.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController : ApiControllerBase
{
    private readonly IGetCartUseCase _getCart;
    private readonly IAddCartItemUseCase _addCartItem;
    private readonly IUpdateCartItemUseCase _updateCartItem;
    private readonly IRemoveCartItemUseCase _removeCartItem;
    private readonly IClearCartUseCase _clearCart;
    private readonly ICheckoutCartUseCase _checkoutCart;
    private readonly ICurrentUserService _currentUser;

    public CartController(
        IGetCartUseCase getCart,
        IAddCartItemUseCase addCartItem,
        IUpdateCartItemUseCase updateCartItem,
        IRemoveCartItemUseCase removeCartItem,
        IClearCartUseCase clearCart,
        ICheckoutCartUseCase checkoutCart,
        ICurrentUserService currentUser)
    {
        _getCart = getCart;
        _addCartItem = addCartItem;
        _updateCartItem = updateCartItem;
        _removeCartItem = removeCartItem;
        _clearCart = clearCart;
        _checkoutCart = checkoutCart;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<CartResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _getCart.ExecuteAsync(_currentUser.UserId, cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(AddCartItemRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _addCartItem.ExecuteAsync(_currentUser.UserId, request, cancellationToken));
    }

    [HttpPut("items/{productId:int}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(int productId, UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateCartItem.ExecuteAsync(_currentUser.UserId, productId, request, cancellationToken));
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<ActionResult<CartResponse>> RemoveItem(int productId, CancellationToken cancellationToken)
    {
        return FromResult(await _removeCartItem.ExecuteAsync(_currentUser.UserId, productId, cancellationToken));
    }

    [HttpDelete]
    public async Task<ActionResult> Clear(CancellationToken cancellationToken)
    {
        return FromResult(await _clearCart.ExecuteAsync(_currentUser.UserId, cancellationToken));
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<OrderResponse>> Checkout(CheckoutCartRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _checkoutCart.ExecuteAsync(_currentUser.UserId, request, cancellationToken));
    }
}
