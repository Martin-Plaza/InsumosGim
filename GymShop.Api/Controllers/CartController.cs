using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Carts;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.UseCases.Carts;
using GymShop.Application.UseCases.Coupons;
using GymShop.Application.DTOs.Coupons;
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
    private readonly IApplyCartCouponUseCase _applyCoupon;
    private readonly IRemoveCartCouponUseCase _removeCoupon;
    private readonly IShippingSettings _shippingSettings;

    public CartController(
        IGetCartUseCase getCart,
        IAddCartItemUseCase addCartItem,
        IUpdateCartItemUseCase updateCartItem,
        IRemoveCartItemUseCase removeCartItem,
        IClearCartUseCase clearCart,
        ICheckoutCartUseCase checkoutCart,
        ICurrentUserService currentUser, IApplyCartCouponUseCase applyCoupon, IRemoveCartCouponUseCase removeCoupon,
        IShippingSettings shippingSettings)
    {
        _getCart = getCart;
        _addCartItem = addCartItem;
        _updateCartItem = updateCartItem;
        _removeCartItem = removeCartItem;
        _clearCart = clearCart;
        _checkoutCart = checkoutCart;
        _currentUser = currentUser;
        _applyCoupon = applyCoupon; _removeCoupon = removeCoupon;
        _shippingSettings = shippingSettings;
    }

    [HttpGet("shipping-options")]
    public ActionResult<ShippingOptionsResponse> GetShippingOptions() => Ok(new ShippingOptionsResponse(
        _shippingSettings.HomeDeliveryCost,
        _shippingSettings.PickupAddress,
        _shippingSettings.PickupInstructions,
        _shippingSettings.PickupHours));

    [HttpPost("coupon")]
    public async Task<ActionResult<CartResponse>> ApplyCoupon(ApplyCouponRequest request, CancellationToken cancellationToken)
    {
        var result = await _applyCoupon.ExecuteAsync(_currentUser.UserId, request, cancellationToken);
        return result.IsSuccess ? Ok(await _getCart.ExecuteAsync(_currentUser.UserId, cancellationToken)) : ToErrorResponse(result.Error!);
    }

    [HttpDelete("coupon")]
    public async Task<ActionResult<CartResponse>> RemoveCoupon(CancellationToken cancellationToken)
    {
        var result = await _removeCoupon.ExecuteAsync(_currentUser.UserId, cancellationToken);
        return result.IsSuccess ? Ok(await _getCart.ExecuteAsync(_currentUser.UserId, cancellationToken)) : ToErrorResponse(result.Error!);
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
