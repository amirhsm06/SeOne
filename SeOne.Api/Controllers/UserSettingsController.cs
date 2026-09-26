using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserSettingsController : ApiControllerBase
{
    private readonly UserManager<User> _users;
    private readonly SeOneDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public UserSettingsController(UserManager<User> users, SeOneDbContext db, IWebHostEnvironment environment)
    { _users = users; _db = db; _environment = environment; }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile() => await FindUserResponse();

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (request.FullName is not null) user.FullName = request.FullName.Trim();
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
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? avatar)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (avatar is null || avatar.Length == 0) return BadRequest(new { message = "Avatar file is required." });
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(avatar.ContentType, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Unsupported image type." });
        var extension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".gif") return BadRequest(new { message = "Unsupported image extension." });
        var relativeDir = Path.Combine("uploads", "avatars");
        var absoluteDir = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), relativeDir);
        Directory.CreateDirectory(absoluteDir);
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
        {
            var old = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), user.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
        }
        var fileName = $"{user.Id:N}-{Guid.NewGuid():N}{extension}";
        await using (var stream = System.IO.File.Create(Path.Combine(absoluteDir, fileName))) await avatar.CopyToAsync(stream);
        user.AvatarUrl = $"/uploads/avatars/{fileName}";
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        return Ok(new { avatarUrl = user.AvatarUrl });
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
        return Ok(new { enabled = !string.IsNullOrWhiteSpace(user.TwoFactorMethod), method = user.TwoFactorMethod ?? "app", phoneNumber = user.PhoneNumber, email = user.Email });
    }

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] EnableTwoFactorRequest request)
    {
        var method = request.Method?.Trim().ToLowerInvariant();
        if (method is not ("sms" or "app" or "email")) return BadRequest(new { message = "Method must be sms, app, or email." });
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        user.TwoFactorMethod = method;
        var codes = Enumerable.Range(0, 8).Select(_ => GenerateCode(10)).ToArray();
        string? secret = method == "app" ? GenerateBase32Secret(20) : null;
        user.TwoFactorBackupCodesJson = JsonSerializer.Serialize(new TwoFactorData { Secret = secret, Codes = codes });
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        if (method == "app")
        {
            var issuer = Uri.EscapeDataString("SeOne");
            var label = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id.ToString());
            var otpUri = $"otpauth://totp/{issuer}:{label}?secret={secret}&issuer={issuer}&digits=6&period=30";
            return Ok(new { secret, qrCodeUrl = otpUri, backupCodes = codes });
        }
        return Ok(new { backupCodes = codes });
    }

    [HttpPost("2fa/verify")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(user.TwoFactorMethod)) return BadRequest(new { message = "Two-factor authentication is not enabled." });
        var code = request.Code?.Trim() ?? string.Empty;
        var data = ReadTwoFactorData(user);
        if (data.Codes.Contains(code, StringComparer.OrdinalIgnoreCase)) return Ok(new { verified = true });
        return user.TwoFactorMethod == "app" && VerifyTotpAgainstSecret(code, data.Secret) ? Ok(new { verified = true }) : BadRequest(new { message = "Invalid verification code." });
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] PasswordRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        var check = await _users.CheckPasswordAsync(user, request.Password ?? string.Empty);
        if (!check) return Unauthorized();
        user.TwoFactorMethod = null; user.TwoFactorBackupCodesJson = null; user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user); return NoContent();
    }

    [HttpPost("2fa/backup/regenerate")]
    public async Task<IActionResult> RegenerateBackupCodes([FromBody] PasswordRequest request)
    {
        var user = await CurrentUserAsync(); if (user is null) return NotFound();
        if (!await _users.CheckPasswordAsync(user, request.Password ?? string.Empty)) return Unauthorized();
        var data = ReadTwoFactorData(user); var codes = Enumerable.Range(0, 8).Select(_ => GenerateCode(10)).ToArray(); data.Codes = codes; user.TwoFactorBackupCodesJson = JsonSerializer.Serialize(data); user.UpdatedAt = DateTime.UtcNow; await _users.UpdateAsync(user); return Ok(new { backupCodes = codes });
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
    private async Task<IActionResult> FindUserResponse() { var u = await CurrentUserAsync(); if (u is null) return NotFound(); return Ok(new { id = u.Id, fullName = u.FullName, email = u.Email, avatarUrl = u.AvatarUrl, bio = u.Bio, dateOfBirth = u.DateOfBirth, phoneNumber = u.PhoneNumber, country = u.Country, language = u.Language, timezone = u.Timezone, createdAt = u.CreatedAt, updatedAt = u.UpdatedAt }); }
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
        var defaults = new Dictionary<string, object?> { ["email"] = new Dictionary<string,bool> { ["booking"]=true,["payment"]=true,["assignment"]=true,["message"]=true,["promotion"]=true,["newsletter"]=true }, ["push"] = new Dictionary<string,bool> { ["booking"]=true,["payment"]=true,["assignment"]=true,["message"]=true,["promotion"]=true }, ["inApp"] = new Dictionary<string,bool> { ["booking"]=true,["payment"]=true,["assignment"]=true,["message"]=true,["promotion"]=true } };
        if (string.IsNullOrWhiteSpace(json)) return defaults;
        try { var current = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new(); var clone = JsonSerializer.SerializeToElement(defaults); var merged = JsonSerializer.Deserialize<Dictionary<string, object?>>(clone.GetRawText())!; MergeObjectDictionary(merged, current); return merged; } catch { return defaults; }
    }
    private static void MergeJson(Dictionary<string, object?> target, JsonElement element) { if (element.ValueKind != JsonValueKind.Object) return; foreach (var p in element.EnumerateObject()) { if (p.Value.ValueKind == JsonValueKind.Object) { var child = target.TryGetValue(p.Name, out var o) && o is JsonElement je && je.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText())! : new(); MergeJson(child, p.Value); target[p.Name] = child; } else target[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText()); } }
    private static void MergeObjectDictionary(Dictionary<string, object?> target, Dictionary<string, object?> source) { foreach (var p in source) target[p.Key] = p.Value; }
    private static string GenerateCode(int length) => Convert.ToHexString(RandomNumberGenerator.GetBytes((length + 1) / 2)).ToLowerInvariant()[..length];
    private static string GenerateBase32Secret(int bytes) { const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var data = RandomNumberGenerator.GetBytes(bytes); var sb = new StringBuilder(); var buffer = 0; var bits = 0; foreach (var b in data) { buffer = (buffer << 8) | b; bits += 8; while (bits >= 5) { bits -= 5; sb.Append(alphabet[(buffer >> bits) & 31]); } } if (bits > 0) sb.Append(alphabet[(buffer << (5 - bits)) & 31]); return sb.ToString(); }
    private static bool VerifyTotpAgainstSecret(string code, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || code.Length != 6 || !code.All(char.IsDigit)) return false;
        byte[] secretBytes;
        try { secretBytes = Base32Decode(secret); } catch { return false; }
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (long offset = -1; offset <= 1; offset++)
        {
            var counter = (unix / 30) + offset;
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
            using var hmac = new HMACSHA1(secretBytes);
            var hash = hmac.ComputeHash(counterBytes);
            var index = hash[^1] & 0x0f;
            var binary = ((hash[index] & 0x7f) << 24) | (hash[index + 1] << 16) | (hash[index + 2] << 8) | hash[index + 3];
            if ((binary % 1_000_000).ToString("D6") == code) return true;
        }
        return false;
    }
    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var clean = input.Trim().TrimEnd('='); var output = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var ch in clean) { var val = alphabet.IndexOf(char.ToUpperInvariant(ch)); if (val < 0) throw new FormatException(); buffer = (buffer << 5) | val; bits += 5; if (bits >= 8) { bits -= 8; output.Add((byte)((buffer >> bits) & 255)); } }
        return output.ToArray();
    }
    private sealed class TwoFactorData { public string? Secret { get; set; } public string[] Codes { get; set; } = Array.Empty<string>(); }
}

public sealed class UpdateProfileRequest { public string? FullName { get; set; } public string? Bio { get; set; } public string? DateOfBirth { get; set; } public string? PhoneNumber { get; set; } public string? Country { get; set; } public string? Language { get; set; } public string? Timezone { get; set; } }
public sealed class ChangePasswordRequest { public string CurrentPassword { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; public string ConfirmPassword { get; set; } = string.Empty; }
public sealed class EnableTwoFactorRequest { public string Method { get; set; } = "app"; }
public sealed class VerifyTwoFactorRequest { public string Code { get; set; } = string.Empty; }
public sealed class PasswordRequest { public string Password { get; set; } = string.Empty; }
