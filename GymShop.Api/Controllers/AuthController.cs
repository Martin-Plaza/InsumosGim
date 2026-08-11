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
    private readonly IVerifyEmailUseCase _verifyEmail;
    private readonly IResendVerificationUseCase _resendVerification;
    private readonly IGoogleLoginUseCase _googleLogin;
    private readonly IGetCurrentUserUseCase _getCurrentUser;
    private readonly IGymShopRequestLimiter _requestLimiter;

    public AuthController(
        IRegisterUserUseCase registerUser,
        ILoginUserUseCase loginUser,
        IVerifyEmailUseCase verifyEmail,
        IResendVerificationUseCase resendVerification,
        IGoogleLoginUseCase googleLogin,
        IGetCurrentUserUseCase getCurrentUser,
        IGymShopRequestLimiter requestLimiter)
    {
        _registerUser = registerUser;
        _loginUser = loginUser;
        _verifyEmail = verifyEmail;
        _resendVerification = resendVerification;
        _googleLogin = googleLogin;
        _getCurrentUser = getCurrentUser;
        _requestLimiter = requestLimiter;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.RegistrationIp)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegistrationPendingResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var decision = _requestLimiter.Acquire(RateLimitPolicies.RegistrationGlobal, "all");
        if (!decision.IsAllowed) return RateLimitResponse.Create(HttpContext, decision);
        return FromResult(await _registerUser.ExecuteAsync(request, cancellationToken));
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitPolicies.RegistrationIp)]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken) =>
        FromResult(await _verifyEmail.ExecuteAsync(request, cancellationToken));

    [HttpPost("resend-verification")]
    [EnableRateLimiting(RateLimitPolicies.RegistrationIp)]
    public async Task<ActionResult<RegistrationPendingResponse>> ResendVerification(ResendVerificationRequest request, CancellationToken cancellationToken) =>
        FromResult(await _resendVerification.ExecuteAsync(request, cancellationToken));

    [HttpPost("google")]
    [EnableRateLimiting(RateLimitPolicies.LoginIp)]
    public async Task<ActionResult<AuthResponse>> Google(GoogleLoginRequest request, CancellationToken cancellationToken) =>
        FromResult(await _googleLogin.ExecuteAsync(request, cancellationToken));

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
