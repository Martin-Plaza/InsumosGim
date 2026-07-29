using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Users;
using GymShop.Application.UseCases.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly IGetUsersUseCase _getUsers;
    private readonly ICreateUserUseCase _createUser;
    private readonly IUpdateUserRoleUseCase _updateUserRole;
    private readonly IUpdateUserStatusUseCase _updateUserStatus;
    private readonly ICurrentUserService _currentUser;

    public UsersController(
        IGetUsersUseCase getUsers,
        ICreateUserUseCase createUser,
        IUpdateUserRoleUseCase updateUserRole,
        IUpdateUserStatusUseCase updateUserStatus,
        ICurrentUserService currentUser)
    {
        _getUsers = getUsers;
        _createUser = createUser;
        _updateUserRole = updateUserRole;
        _updateUserStatus = updateUserStatus;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminUserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _getUsers.ExecuteAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _createUser.ExecuteAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), new { id = result.Value!.Id }, result.Value)
            : ToErrorResponse(result.Error!);
    }

    [HttpPatch("{id:int}/role")]
    public async Task<ActionResult> UpdateRole(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateUserRole.ExecuteAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult> UpdateStatus(int id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateUserStatus.ExecuteAsync(id, request, _currentUser.UserId, cancellationToken));
    }
}
