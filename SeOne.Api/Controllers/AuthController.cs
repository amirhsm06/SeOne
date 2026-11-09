using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Application.Exceptions;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly UserManager<User> _userManager;
    private readonly IOtpSender _otpSender;
    private readonly IEmailSender _emailSender;

    public AuthController(
        IAuthService authService,
        UserManager<User> userManager,
        IOtpSender otpSender,
        IEmailSender emailSender)
    {
        _authService = authService;
        _userManager = userManager;
        _otpSender = otpSender;
        _emailSender = emailSender;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(
                request.FirstName,
                request.FamilyName,
                request.Email,
                request.Password,
                request.Role,
                request.DateOfBirth,
                request.ParentalConsent);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequest request)
    {
        var twoFactorCode = request.TwoFactorCode ?? request.Code;
        var result = await _authService.LoginAsync(
            request.Email,
            request.Password,
            twoFactorCode);

        if (result is null)
        {
            var email = request.Email?.Trim();
            var candidate = string.IsNullOrWhiteSpace(email)
                ? null
                : await _userManager.FindByEmailAsync(email);
            if (candidate is not null &&
                string.Equals(candidate.AccountStatus, "active", StringComparison.OrdinalIgnoreCase) &&
                await _userManager.CheckPasswordAsync(candidate, request.Password ?? string.Empty) &&
                candidate.TwoFactorEnabled &&
                string.IsNullOrWhiteSpace(twoFactorCode))
            {
                return Unauthorized(new
                {
                    message = "A two-factor authentication code is required.",
                    requiresTwoFactor = true,
                    method = candidate.TwoFactorMethod
                });
            }

            return Unauthorized(new
            {
                message = "Invalid email, password, or two-factor code."
            });
        }

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(
                ClaimTypes.Name);

        if (userId is null ||
            !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(
            id.ToString());

        if (user is null)
            return Unauthorized();

        return Ok(new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            FamilyName = user.FamilyName,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString()
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        return NoContent();
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpRequest request)
    {
        if (!_otpSender.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "SMS verification is not configured yet." });

        bool result;
        try
        {
            result = await _authService.RequestOtpAsync(request.Phone);
        }
        catch (DeliveryNotConfiguredException)
        {
            // Keep the response generic to avoid disclosing whether a phone number belongs to an account.
            return NoContent();
        }

        if (!result)
        {
            return BadRequest(new
            {
                message = "Invalid phone number."
            });
        }

        return NoContent();
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<AuthResponseDto>> VerifyOtp(
        [FromBody] VerifyOtpRequest request)
    {
        var result = await _authService.VerifyOtpAsync(
            request.Phone,
            request.Code);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid or expired verification code."
            });
        }

        return Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request)
    {
        // Return the same configuration status for all addresses; do not query the account first.
        if (!_emailSender.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Password-reset email delivery is not configured yet." });

        try
        {
            await _authService.ForgotPasswordAsync(request.Email);
        }
        catch (DeliveryNotConfiguredException)
        {
            // A misconfigured provider must not reveal whether the account exists.
        }

        return NoContent();
    }
}

public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string FamilyName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public bool? ParentalConsent { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? TwoFactorCode { get; set; }

    // Alias accepted for clients that call the second factor simply "code".
    public string? Code { get; set; }
}

public class RequestOtpRequest
{
    public string Phone { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    public string Phone { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}