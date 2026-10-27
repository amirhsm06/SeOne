using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Api.DTOs;
using SeOne.Api.Services;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teacher/dashboard/withdrawals")]
[Authorize(Roles = "Teacher")]
public sealed class TeacherWithdrawalsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public TeacherWithdrawalsController(SeOneDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> RequestWithdrawal(
        [FromBody] CreateTeacherWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var teacherId = GetUserId();
        if (teacherId is null) return Unauthorized();

        if (request.Amount <= 0m || request.Amount != decimal.Round(request.Amount, 2))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["amount"] = ["Amount must be greater than zero and have at most two decimal places."]
            }));
        }

        var iban = NormalizeIban(request.Iban);
        if (!IsValidIranianIban(iban))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["iban"] = ["Enter a valid Iranian IBAN: IR followed by 24 digits, with a valid checksum."]
            }));
        }

        if (!_db.Database.IsNpgsql())
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Withdrawal reservations require the PostgreSQL transaction-locking provider.");
        }

        // ReadCommitted gives each post-lock query a fresh snapshot after a competing reservation commits.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        await LockTeacherLedgerAsync(_db, teacherId.Value, cancellationToken);

        var balance = await TeacherWithdrawalLedger.CalculateAsync(
            _db, teacherId.Value, cancellationToken);

        if (!balance.PolicyConfigured)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: $"Withdrawals are disabled until {TeacherWithdrawalLedger.SharePercentEnvironmentVariable} is configured to an authorized value between 0 and 100.");
        }

        if (balance.HasMixedCurrencies)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "The teacher has completed bookings in multiple currencies. A single-currency withdrawal cannot be calculated safely.");
        }

        if (request.Amount > balance.AvailableBalance)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"Requested amount exceeds the current available balance of {balance.AvailableBalance} {balance.Currency}.");
        }

        var now = DateTime.UtcNow;
        var withdrawal = new TeacherWithdrawal
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            Amount = request.Amount,
            Currency = balance.Currency,
            Iban = iban,
            Status = "pending",
            RequestedAt = now
        };

        _db.TeacherWithdrawals.Add(withdrawal);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            id = withdrawal.Id,
            status = withdrawal.Status,
            amount = withdrawal.Amount,
            currency = withdrawal.Currency,
            requestedAt = withdrawal.RequestedAt
        });
    }

    private Guid? GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    internal static async Task LockTeacherLedgerAsync(
        SeOneDbContext db,
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        // Advisory locks are transaction-scoped. Reservations and admin decisions use the same key.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({teacherId.ToString()})::bigint)",
            cancellationToken);
    }

    private static string NormalizeIban(string? value) =>
        Regex.Replace(value ?? string.Empty, @"[\s-]", string.Empty).ToUpperInvariant();

    private static bool IsValidIranianIban(string value)
    {
        if (!Regex.IsMatch(value, @"^IR\d{24}$", RegexOptions.CultureInvariant)) return false;

        var rearranged = value.Substring(4) + value.Substring(0, 4);
        var remainder = 0;
        foreach (var character in rearranged)
        {
            if (character is >= '0' and <= '9')
            {
                remainder = (remainder * 10 + character - '0') % 97;
                continue;
            }

            if (character is >= 'A' and <= 'Z')
            {
                var numeric = character - 'A' + 10;
                remainder = (remainder * 10 + numeric / 10) % 97;
                remainder = (remainder * 10 + numeric % 10) % 97;
                continue;
            }

            return false;
        }

        return remainder == 1;
    }
}