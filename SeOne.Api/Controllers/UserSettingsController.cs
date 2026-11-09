using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Authentication;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserSettingsController : ApiControllerBase
{
    private readonly UserManager<User> _users;
    private readonly SeOneDbContext _db;
    private readonly IImageStorageService _imageStorage;

    public UserSettingsController(
        UserManager<User> users,
        SeOneDbContext db,
        IImageStorageService imageStorage)
    {
        _users = users;
        _db = db;
        _imageStorage = imageStorage;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile() => await FindUserResponse();

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (request.FirstName is not null)
            user.FirstName = request.FirstName.Trim();

        if (request.FamilyName is not null)
            user.FamilyName = request.FamilyName.Trim();

        user.FullName = $"{user.FirstName} {user.FamilyName}".Trim();
        if (request.Bio is not null) user.Bio = request.Bio;
        if (request.DateOfBirth is not null && DateTime.TryParse(request.DateOfBirth, out var dob)) user.DateOfBirth = dob.Date;
        if (request.PhoneNumber is not null) user.PhoneNumber = request.PhoneNumber;
        if (request.Country is not null) user.Country = request.Country;
        if (request.Language is not null) user.Language = request.Language;
        if (request.Timezone is not null) user.Timezone = request.Timezone;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        return await FindUserResponse();
    }

    [HttpPost("profile/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_000_000)]
    public Task<IActionResult> UploadAvatar(
        IFormFile? avatar,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        return SaveAvatarAsync(avatar ?? file, cancellationToken);
    }

    [HttpPost("~/api/teacher/profile/avatar")]
    [Authorize(Roles = "Teacher")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_000_000)]
    public Task<IActionResult> UploadTeacherAvatar(
        IFormFile? avatar,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        return SaveAvatarAsync(avatar ?? file, cancellationToken);
    }

    private async Task<IActionResult> SaveAvatarAsync(
        IFormFile? avatar,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync();

        if (user is null)
            return NotFound();

        if (avatar is null || avatar.Length == 0)
        {
            return BadRequest(new
            {
                message = "Avatar file is required."
            });
        }

        if (avatar.Length > 5_000_000)
        {
            return BadRequest(new
            {
                message = "Avatar must be 5 MB or smaller."
            });
        }

        var extension = Path.GetExtension(avatar.FileName)
            .ToLowerInvariant();

        var expectedContentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (expectedContentType is null ||
            !string.Equals(
                avatar.ContentType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Only matching JPG, PNG, and WEBP images are allowed."
            });
        }

        string avatarUrl;

        try
        {
            await using var stream = avatar.OpenReadStream();

            avatarUrl = await _imageStorage.UploadAsync(
                stream,
                avatar.FileName,
                avatar.ContentType,
                "avatars",
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new
            {
                message = "Avatar upload failed validation or storage rejected the file."
            });
        }

        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _users.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "The avatar was uploaded, but the profile update failed."
                });
        }

        return Ok(new
        {
            avatarUrl = user.AvatarUrl
        });
    }

    [HttpDelete("profile/avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        user.AvatarUrl = null; user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user); return NoContent();
    }

    [HttpPost("password/change")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword) return BadRequest(new { message = "New passwords do not match." });
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        var result = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        return NoContent();
    }

    [HttpGet("2fa/status")]
    public async Task<IActionResult> TwoFactorStatus()
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        return Ok(new { enabled = user.TwoFactorEnabled, method = user.TwoFactorMethod ?? "app", phoneNumber = user.PhoneNumber, email = user.Email });
    }

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] EnableTwoFactorRequest request)
    {
        var method = request.Method?.Trim().ToLowerInvariant();
        if (method != "app")
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Only authenticator-app 2FA is available. SMS and email delivery are not configured." });

        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        if (user.TwoFactorEnabled)
            return Conflict(new { message = "Two-factor authentication is already enabled. Disable it before starting a new authenticator setup." });

        var codes = Enumerable.Range(0, 8).Select(_ => GenerateCode(10)).ToArray();
        var secret = GenerateBase32Secret(20);
        user.TwoFactorMethod = "app";
        user.TwoFactorEnabled = false;
        user.TwoFactorBackupCodesJson = JsonSerializer.Serialize(new TwoFactorData
        {
            Secret = secret,
            Codes = codes.Select(TwoFactorCodeVerifier.HashBackupCode).ToArray()
        });
        user.UpdatedAt = DateTime.UtcNow;

        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not save two-factor setup." });

        var issuer = Uri.EscapeDataString("SeOne");
        var label = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id.ToString());
        var otpUri = $"otpauth://totp/{issuer}:{label}?secret={secret}&issuer={issuer}&digits=6&period=30";
        return Ok(new { secret, qrCodeUrl = otpUri, backupCodes = codes });
    }

    [HttpPost("2fa/verify")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (!string.Equals(user.TwoFactorMethod, "app", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Authenticator-app setup is not pending." });

        var code = request.Code?.Trim() ?? string.Empty;
        var data = ReadTwoFactorData(user);
        if (TwoFactorCodeVerifier.VerifyTotp(code, data.Secret))
        {
            user.TwoFactorEnabled = true;
            user.UpdatedAt = DateTime.UtcNow;
            var update = await _users.UpdateAsync(user);
            if (!update.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not activate two-factor authentication." });
            return Ok(new { verified = true, enabled = true });
        }

        // Once enabled, a backup code is accepted here but consumed exactly once.
        if (user.TwoFactorEnabled && TwoFactorCodeVerifier.TryConsumeBackupCode(user, code))
        {
            user.UpdatedAt = DateTime.UtcNow;
            var update = await _users.UpdateAsync(user);
            if (!update.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not update backup-code state." });
            return Ok(new { verified = true, enabled = true });
        }

        return BadRequest(new { message = "Invalid verification code." });
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] PasswordRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        var check = await _users.CheckPasswordAsync(user, request.Password ?? string.Empty);
        if (!check) return Unauthorized();
        user.TwoFactorEnabled = false;
        user.TwoFactorMethod = null;
        user.TwoFactorBackupCodesJson = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        return NoContent();
    }

    [HttpPost("2fa/backup/regenerate")]
    public async Task<IActionResult> RegenerateBackupCodes([FromBody] PasswordRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (!await _users.CheckPasswordAsync(user, request.Password ?? string.Empty)) return Unauthorized();
        if (!user.TwoFactorEnabled || !string.Equals(user.TwoFactorMethod, "app", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Authenticator-app 2FA must be enabled before regenerating backup codes." });

        var data = ReadTwoFactorData(user);
        var codes = Enumerable.Range(0, 8).Select(_ => GenerateCode(10)).ToArray();
        data.Codes = codes.Select(TwoFactorCodeVerifier.HashBackupCode).ToArray();
        user.TwoFactorBackupCodesJson = JsonSerializer.Serialize(data);
        user.UpdatedAt = DateTime.UtcNow;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not save the regenerated backup codes." });
        return Ok(new { backupCodes = codes });
    }

    [HttpGet("notifications/preferences")]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        return Ok(GetPreferences(user.NotificationPreferencesJson));
    }

    [HttpPatch("notifications/preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] JsonElement preferences)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        var current = JsonSerializer.Deserialize<Dictionary<string, object?>>(user.NotificationPreferencesJson ?? "{}") ?? new();
        MergeJson(current, preferences);
        user.NotificationPreferencesJson = JsonSerializer.Serialize(current); user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user); return Ok(GetPreferences(user.NotificationPreferencesJson));
    }

    [HttpPost("account/delete")]
    public async Task<IActionResult> RequestDeletion([FromBody] PasswordRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (!await _users.CheckPasswordAsync(user, request.Password ?? string.Empty)) return Unauthorized();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        user.AccountDeletionToken = token; user.AccountDeletionScheduledAt = DateTime.UtcNow.AddDays(7); user.AccountStatus = "deletion_pending"; user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user);
        return Ok(new { deletionToken = token, scheduledDate = user.AccountDeletionScheduledAt.Value });
    }

    [HttpDelete("account/delete/{token}")]
    public async Task<IActionResult> ConfirmDeletion(string token)
    {
        var user = await _users.FindByIdAsync(RequireUserId().ToString()); if (user is null) return NotFound();
        if (!string.Equals(user.AccountDeletionToken, token, StringComparison.Ordinal)) return BadRequest(new { message = "Invalid deletion token." });
        user.AccountStatus = "deleted"; user.AccountDeletionToken = null; user.AccountDeletionScheduledAt = null; user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user); return NoContent();
    }

    [HttpPost("account/delete/cancel")]
    public async Task<IActionResult> CancelDeletion()
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        user.AccountStatus = "active"; user.AccountDeletionToken = null; user.AccountDeletionScheduledAt = null; user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user); return NoContent();
    }

    private async Task<User?> CurrentUserAsync() => await _users.FindByIdAsync(RequireUserId().ToString());
    private async Task<IActionResult> FindUserResponse() { var u = await CurrentUserAsync(); if (u is null) return NotFound(); return Ok(new { id = u.Id,
        firstName = u.FirstName,
        familyName = u.FamilyName,
        fullName = u.FullName, email = u.Email, avatarUrl = u.AvatarUrl, bio = u.Bio, dateOfBirth = u.DateOfBirth, phoneNumber = u.PhoneNumber, country = u.Country, language = u.Language, timezone = u.Timezone, createdAt = u.CreatedAt, updatedAt = u.UpdatedAt }); }
    private static string[] ReadBackupCodes(User user) => ReadTwoFactorData(user).Codes;
    private static TwoFactorData ReadTwoFactorData(User user)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(user.TwoFactorBackupCodesJson)) return new TwoFactorData();
            var raw = user.TwoFactorBackupCodesJson.TrimStart();
            if (raw.StartsWith("[")) return new TwoFactorData { Codes = JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>() };
            return JsonSerializer.Deserialize<TwoFactorData>(raw) ?? new TwoFactorData();
        }
        catch { return new TwoFactorData(); }
    }
    private static object GetPreferences(string? json)
    {
        var defaults = new Dictionary<string, object?>
        {
            ["email"] = new Dictionary<string, object?>
            {
                ["booking"] = true,
                ["payment"] = true,
                ["assignment"] = true,
                ["message"] = true,
                ["promotion"] = true,
                ["newsletter"] = true
            },
            ["push"] = new Dictionary<string, object?>
            {
                ["booking"] = true,
                ["payment"] = true,
                ["assignment"] = true,
                ["message"] = true,
                ["promotion"] = true
            },
            ["inApp"] = new Dictionary<string, object?>
            {
                ["booking"] = true,
                ["payment"] = true,
                ["assignment"] = true,
                ["message"] = true,
                ["promotion"] = true
            }
        };

        if (string.IsNullOrWhiteSpace(json))
            return defaults;

        try
        {
            using var document = JsonDocument.Parse(json);

            MergePreferenceDefaults(
                defaults,
                document.RootElement);

            return defaults;
        }
        catch
        {
            return defaults;
        }
    }

    private static void MergePreferenceDefaults(
        Dictionary<string, object?> target,
        JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in source.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                if (target.TryGetValue(property.Name, out var existing) &&
                    existing is Dictionary<string, object?> child)
                {
                    MergePreferenceDefaults(child, property.Value);
                }
                else
                {
                    var newChild = new Dictionary<string, object?>();

                    MergePreferenceDefaults(newChild, property.Value);

                    target[property.Name] = newChild;
                }
            }
            else
            {
                target[property.Name] = property.Value.Clone();
            }
        }
    }
    private static void MergeJson(Dictionary<string, object?> target, JsonElement element) { if (element.ValueKind != JsonValueKind.Object) return; foreach (var p in element.EnumerateObject()) { if (p.Value.ValueKind == JsonValueKind.Object) { var child = target.TryGetValue(p.Name, out var o) && o is JsonElement je && je.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText())! : new(); MergeJson(child, p.Value); target[p.Name] = child; } else target[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText()); } }
    private static string GenerateCode(int length) => Convert.ToHexString(RandomNumberGenerator.GetBytes((length + 1) / 2)).ToLowerInvariant()[..length];
    private static string GenerateBase32Secret(int bytes) { const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var data = RandomNumberGenerator.GetBytes(bytes); var sb = new StringBuilder(); var buffer = 0; var bits = 0; foreach (var b in data) { buffer = (buffer << 8) | b; bits += 8; while (bits >= 5) { bits -= 5; sb.Append(alphabet[(buffer >> bits) & 31]); } } if (bits > 0) sb.Append(alphabet[(buffer << (5 - bits)) & 31]); return sb.ToString(); }
    private sealed class TwoFactorData { public string? Secret { get; set; } public string[] Codes { get; set; } = Array.Empty<string>(); }
}

public sealed class UpdateProfileRequest
{
    public string? FirstName { get; set; }

    public string? FamilyName { get; set; }

    public string? Bio { get; set; }

    public string? DateOfBirth { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Country { get; set; }

    public string? Language { get; set; }

    public string? Timezone { get; set; }
}
public sealed class ChangePasswordRequest { public string CurrentPassword { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; public string ConfirmPassword { get; set; } = string.Empty; }
public sealed class EnableTwoFactorRequest { public string Method { get; set; } = "app"; }
public sealed class VerifyTwoFactorRequest { public string Code { get; set; } = string.Empty; }
public sealed class PasswordRequest { public string Password { get; set; } = string.Empty; }
