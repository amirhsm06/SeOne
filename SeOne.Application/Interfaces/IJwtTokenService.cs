using SeOne.Domain.Entities;

namespace SeOne.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}