using GymShop.Domain.Entities;

namespace GymShop.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateToken(User user);
}
