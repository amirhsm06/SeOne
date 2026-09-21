using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(
        string fullName,
        string email,
        string password,
        string role);

    Task<AuthResponseDto?> LoginAsync(
        string email,
        string password);
    Task<bool> RequestOtpAsync(string phone);

    Task<AuthResponseDto?> VerifyOtpAsync(string phone, string code);
}