using Microsoft.AspNetCore.Identity;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;

namespace SeOne.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        UserManager<User> userManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        string fullName,
        string email,
        string password,
        string role)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            throw new ArgumentException("Invalid role.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName,
            Role = userRole
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(x => x.Description));

            throw new ArgumentException(errors);
        }

        return new AuthResponseDto
        {
            Token = _jwtTokenService.GenerateToken(user),
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = user.Role.ToString()
            }
        };
    }

    public async Task<AuthResponseDto?> LoginAsync(
        string email,
        string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
            return null;

        var validPassword = await _userManager.CheckPasswordAsync(
            user,
            password);

        if (!validPassword)
            return null;

        return new AuthResponseDto
        {
            Token = _jwtTokenService.GenerateToken(user),
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = user.Role.ToString()
            }
        };
    }
}