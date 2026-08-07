using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Auth;
using GymShop.Application.UseCases.Auth;
using GymShop.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IRegisterUserUseCase _registerUser;
    private readonly ILoginUserUseCase _loginUser;
    private readonly IGetCurrentUserUseCase _getCurrentUser;
    private readonly IGymShopRequestLimiter _requestLimiter;

    public AuthController(
        IRegisterUserUseCase registerUser,
        ILoginUserUseCase loginUser,
        IGetCurrentUserUseCase getCurrentUser,
        IGymShopRequestLimiter requestLimiter)
    {
        _registerUser = registerUser;
        _loginUser = loginUser;
        _getCurrentUser = getCurrentUser;
        _requestLimiter = requestLimiter;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.RegistrationIp)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var decision = _requestLimiter.Acquire(RateLimitPolicies.RegistrationGlobal, "all");
        if (!decision.IsAllowed) return RateLimitResponse.Create(HttpContext, decision);
        return FromResult(await _registerUser.ExecuteAsync(request, cancellationToken));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.LoginIp)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var accountKey = GymShopRequestLimiter.HashAccount(request.Email);
        var decision = _requestLimiter.Acquire(RateLimitPolicies.LoginAccount, accountKey);
        if (!decision.IsAllowed) return RateLimitResponse.Create(HttpContext, decision);
        return FromResult(await _loginUser.ExecuteAsync(request, cancellationToken));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        return FromResult(await _getCurrentUser.ExecuteAsync(currentUser.UserId, cancellationToken));
    }
}
