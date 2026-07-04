using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Auth;
using GymShop.Application.UseCases.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IRegisterUserUseCase _registerUser;
    private readonly ILoginUserUseCase _loginUser;
    private readonly IGetCurrentUserUseCase _getCurrentUser;

    public AuthController(
        IRegisterUserUseCase registerUser,
        ILoginUserUseCase loginUser,
        IGetCurrentUserUseCase getCurrentUser)
    {
        _registerUser = registerUser;
        _loginUser = loginUser;
        _getCurrentUser = getCurrentUser;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _registerUser.ExecuteAsync(request, cancellationToken));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
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
