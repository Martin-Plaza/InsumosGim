using GymShop.Application.Common;
using GymShop.Application.DTOs.Users;
using GymShop.Application.UseCases.Users;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;

namespace GymShop.Tests.UseCases;

public class UserAdminUseCaseTests
{
    [Fact]
    public async Task UpdateUserStatus_rejects_self_deactivation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "admin@test.com", "SuperAdmin");

        var useCase = new UpdateUserStatusUseCase(db);
        var result = await useCase.ExecuteAsync(user.Id, new UpdateUserStatusRequest(false), currentUserId: user.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Conflict, result.Error?.Type);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task UpdateUserStatus_deactivation_increments_token_version()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "user@test.com", "User");

        var result = await new UpdateUserStatusUseCase(db)
            .ExecuteAsync(user.Id, new UpdateUserStatusRequest(false), currentUserId: 999);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task UpdateUserRole_change_increments_token_version_but_same_role_does_not()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "admin@test.com", "Admin");
        var useCase = new UpdateUserRoleUseCase(db);

        var unchanged = await useCase.ExecuteAsync(user.Id, new UpdateUserRoleRequest("Admin"));
        var changed = await useCase.ExecuteAsync(user.Id, new UpdateUserRoleRequest("User"));

        Assert.True(unchanged.IsSuccess);
        Assert.True(changed.IsSuccess);
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(db.Roles.Single(x => x.Name == "User").Id, user.RoleId);
    }

    private static async Task<User> SeedUserAsync(GymShop.Infrastructure.Data.GymShopDbContext db, string email, string roleName)
    {
        var role = db.Roles.Single(x => x.Name == roleName);
        var user = new User
        {
            Email = email,
            Name = "Usuario Test",
            PasswordHash = new PasswordHasher().Hash("123456"),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
