using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(
        string firstName,
        string familyName,
        string email,
        string password,
        string role,
        DateTime? dateOfBirth = null,
        bool? parentalConsent = null);

    Task<AuthResponseDto?> LoginAsync(
        string email,
        string password,
        string? twoFactorCode = null);

    Task<bool> RequestOtpAsync(
        string phone);

    Task<AuthResponseDto?> VerifyOtpAsync(
        string phone,
        string code);

    Task<bool> ForgotPasswordAsync(
        string email);
}