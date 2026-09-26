using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const int OtpLength = 6;
    private const int OtpLifetimeMinutes = 5;
    private const int OtpResendDelaySeconds = 60;
    private const int MaxOtpAttempts = 5;

    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly SeOneDbContext _dbContext;
    private readonly IOtpSender _otpSender;
    private readonly IEmailSender _emailSender;

    public AuthService(
        UserManager<User> userManager,
        IJwtTokenService jwtTokenService,
        SeOneDbContext dbContext,
        IOtpSender otpSender,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
        _otpSender = otpSender;
        _emailSender = emailSender;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        string firstName,
        string familyName,
        string email,
        string password,
        string role)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            throw new ArgumentException("Invalid role.");

        if (userRole == UserRole.Admin)
            throw new ArgumentException("Admin accounts cannot be self-registered.");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.");

        if (string.IsNullOrWhiteSpace(familyName))
            throw new ArgumentException("Family name is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.");

        var normalizedEmail = email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (existingUser is not null)
            throw new ArgumentException(
                "A user with this email already exists.");

        var normalizedFirstName = firstName.Trim();
        var normalizedFamilyName = familyName.Trim();

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FirstName = normalizedFirstName,
            FamilyName = normalizedFamilyName,
            FullName = $"{normalizedFirstName} {normalizedFamilyName}",
            Role = userRole
        };

        var result = await _userManager.CreateAsync(
            user,
            password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(x => x.Description));

            throw new ArgumentException(errors);
        }

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponseDto?> LoginAsync(
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(
            email.Trim());

        if (user is null)
            return null;

        if (!string.Equals(user.AccountStatus, "active", StringComparison.OrdinalIgnoreCase))
            return null;

        var validPassword = await _userManager.CheckPasswordAsync(
            user,
            password);

        if (!validPassword)
            return null;

        return CreateAuthResponse(user);
    }

    public async Task<bool> RequestOtpAsync(string phone)
    {
        var normalizedPhone = NormalizePhone(phone);

        if (!IsValidPhone(normalizedPhone))
            return false;

        var now = DateTime.UtcNow;

        var recentRequestExists = await _dbContext.OtpCodes
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.PhoneNumber == normalizedPhone &&
                    x.CreatedAt >
                    now.AddSeconds(-OtpResendDelaySeconds) &&
                    x.ConsumedAt == null);

        if (recentRequestExists)
            return true;

        var user = await _userManager.Users
            .FirstOrDefaultAsync(
                x => x.PhoneNumber == normalizedPhone);

        if (user is null)
            return true;

        var activeOtps = await _dbContext.OtpCodes
            .Where(
                x =>
                    x.PhoneNumber == normalizedPhone &&
                    x.ConsumedAt == null)
            .ToListAsync();

        foreach (var otp in activeOtps)
            otp.ConsumedAt = now;

        var code = GenerateOtpCode();
        var salt = GenerateSalt();
        var hash = HashOtp(
            normalizedPhone,
            code,
            salt);

        var otpCode = new OtpCode
        {
            Id = Guid.NewGuid(),
            PhoneNumber = normalizedPhone,
            CodeHash = hash,
            Salt = salt,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(OtpLifetimeMinutes),
            FailedAttempts = 0
        };

        _dbContext.OtpCodes.Add(otpCode);

        await _dbContext.SaveChangesAsync();

        await _otpSender.SendAsync(
            normalizedPhone,
            code);

        return true;
    }

    public async Task<AuthResponseDto?> VerifyOtpAsync(
        string phone,
        string code)
    {
        var normalizedPhone = NormalizePhone(phone);

        if (!IsValidPhone(normalizedPhone))
            return null;

        if (!IsValidOtp(code))
            return null;

        var now = DateTime.UtcNow;

        var otp = await _dbContext.OtpCodes
            .Where(
                x =>
                    x.PhoneNumber == normalizedPhone &&
                    x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp is null)
            return null;

        if (otp.ExpiresAt <= now)
            return null;

        if (otp.FailedAttempts >= MaxOtpAttempts)
            return null;

        var suppliedHash = HashOtp(
            normalizedPhone,
            code,
            otp.Salt);

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(otp.CodeHash),
                Convert.FromHexString(suppliedHash)))
        {
            otp.FailedAttempts++;

            await _dbContext.SaveChangesAsync();

            return null;
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(
                x => x.PhoneNumber == normalizedPhone);

        if (user is null)
            return null;

        otp.ConsumedAt = now;

        if (!user.PhoneNumberConfirmed)
            user.PhoneNumberConfirmed = true;

        await _dbContext.SaveChangesAsync();

        await _userManager.UpdateAsync(user);

        return CreateAuthResponse(user);
    }

    public async Task<bool> ForgotPasswordAsync(
        string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return true;

        var normalizedEmail = email.Trim();

        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        /*
         * Always return success, even when the email does not exist.
         * This prevents account enumeration through this endpoint.
         */
        if (user is null)
            return true;

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(
            user);

        await _emailSender.SendPasswordResetAsync(
            user.Email ?? normalizedEmail,
            user.FullName,
            resetToken);

        return true;
    }

    private AuthResponseDto CreateAuthResponse(User user)
    {
        return new AuthResponseDto
        {
            Token = _jwtTokenService.GenerateToken(user),
            User = new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                FamilyName = user.FamilyName,
                Email = user.Email ?? string.Empty,
                Role = user.Role.ToString()
            }
        };
    }

    private static string GenerateOtpCode()
    {
        return RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();
    }

    private static string GenerateSalt()
    {
        return Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
    }

    private static string HashOtp(
        string phoneNumber,
        string code,
        string salt)
    {
        var value = $"{phoneNumber}:{code}:{salt}";

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash);
    }

    private static bool IsValidOtp(string code)
    {
        return !string.IsNullOrWhiteSpace(code) &&
               code.Length == OtpLength &&
               code.All(char.IsDigit);
    }

    private static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        return phone.Length is >= 7 and <= 32 &&
               phone.All(c =>
                   char.IsDigit(c) ||
                   c == '+');
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var cleaned = new string(
            phone
                .Trim()
                .Where(c =>
                    char.IsDigit(c) ||
                    c == '+')
                .ToArray());

        if (cleaned.StartsWith("00"))
            cleaned = "+" + cleaned[2..];

        return cleaned;
    }
}