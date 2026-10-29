using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Api.DTOs;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/admin/withdrawals")]
[Authorize(Roles = "Admin")]
public sealed class AdminWithdrawalsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public AdminWithdrawalsController(SeOneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string status = "pending",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        status = (status ?? "pending").Trim().ToLowerInvariant();
        if (status is not ("all" or "pending" or "approved" or "rejected"))
        {
            return BadRequest(new { message = "Status must be pending, approved, rejected, or all." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from withdrawal in _db.TeacherWithdrawals.AsNoTracking()
            join teacher in _db.Users.AsNoTracking() on withdrawal.TeacherId equals teacher.Id
            select new { withdrawal, teacher };

        if (status != "all") query = query.Where(x => x.withdrawal.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.withdrawal.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.withdrawal.Id,
                teacherId = x.withdrawal.TeacherId,
                teacherName = string.IsNullOrWhiteSpace(x.teacher.FullName)
                    ? (x.teacher.FirstName + " " + x.teacher.FamilyName).Trim()
                    : x.teacher.FullName,
                teacherEmail = x.teacher.Email ?? string.Empty,
                amount = x.withdrawal.Amount,
                currency = x.withdrawal.Currency,
                iban = x.withdrawal.Iban,
                status = x.withdrawal.Status,
                requestedAt = x.withdrawal.RequestedAt,
                decidedAt = x.withdrawal.DecidedAt,
                decidedById = x.withdrawal.DecidedById
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items, total, page, pageSize, totalCount = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpPatch("{withdrawalId:guid}/status")]
    public async Task<IActionResult> Decide(
        Guid withdrawalId,
        [FromBody] DecideTeacherWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        if (adminId is null) return Unauthorized();

        var targetStatus = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (targetStatus is not ("approved" or "rejected"))
        {
            return BadRequest(new { message = "Status must be approved or rejected." });
        }

        if (!_db.Database.IsNpgsql())
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Withdrawal decisions require the PostgreSQL transaction-locking provider.");
        }

        var preliminary = await _db.TeacherWithdrawals
            .AsNoTracking()
            .Where(x => x.Id == withdrawalId)
            .Select(x => new { x.TeacherId })
            .SingleOrDefaultAsync(cancellationToken);

        if (preliminary is null) return NotFound();

        // ReadCommitted gives each post-lock query a fresh snapshot after a competing reservation commits.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        await TeacherWithdrawalsController.LockTeacherLedgerAsync(
            _db, preliminary.TeacherId, cancellationToken);

        var withdrawal = await _db.TeacherWithdrawals
            .SingleOrDefaultAsync(x => x.Id == withdrawalId, cancellationToken);

        if (withdrawal is null) return NotFound();
        if (withdrawal.Status != "pending")
        {
            return Conflict(new { message = "This withdrawal request has already been decided." });
        }

        var teacher = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == withdrawal.TeacherId, cancellationToken);

        if (teacher is null) return Conflict(new { message = "The teacher account no longer exists." });

        var now = DateTime.UtcNow;
        withdrawal.Status = targetStatus;
        withdrawal.DecidedAt = now;
        withdrawal.DecidedById = adminId.Value;

        var fa = string.Equals(teacher.Language, "fa", StringComparison.OrdinalIgnoreCase);
        var approved = targetStatus == "approved";
        var title = fa
            ? (approved ? "ط¯ط±ط®ظˆط§ط³طھ ط¨ط±ط¯ط§ط´طھ طھط£غŒغŒط¯ ط´ط¯" : "ط¯ط±ط®ظˆط§ط³طھ ط¨ط±ط¯ط§ط´طھ ط±ط¯ ط´ط¯")
            : (approved ? "Withdrawal request approved" : "Withdrawal request rejected");
        var amountText = withdrawal.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        var message = fa
            ? (approved
                ? $"ط¯ط±ط®ظˆط§ط³طھ ط¨ط±ط¯ط§ط´طھ ط´ظ…ط§ ط¨ظ‡ ظ…ط¨ظ„ط؛ {amountText} {withdrawal.Currency} طھط£غŒغŒط¯ ط´ط¯. ط§غŒظ† ظ¾غŒط§ظ… طھط£غŒغŒط¯ ط§ط³طھ ظˆ ط¨ظ‡â€Œظ…ط¹ظ†ط§غŒ ط§ظ†ط¬ط§ظ… ط§ظ†طھظ‚ط§ظ„ ط¨ط§ظ†ع©غŒ ظ†غŒط³طھ."
                : $"ط¯ط±ط®ظˆط§ط³طھ ط¨ط±ط¯ط§ط´طھ ط´ظ…ط§ ط¨ظ‡ ظ…ط¨ظ„ط؛ {amountText} {withdrawal.Currency} ط±ط¯ ط´ط¯ط› ظ…ط¨ظ„ط؛ ط±ط²ط±ظˆط´ط¯ظ‡ ط¯ظˆط¨ط§ط±ظ‡ ط¯ط± ظ…ظˆط¬ظˆط¯غŒ ظ‚ط§ط¨ظ„ ط¨ط±ط¯ط§ط´طھ ظ…ط­ط§ط³ط¨ظ‡ ظ…غŒâ€Œط´ظˆط¯.")
            : (approved
                ? $"Your withdrawal request for {amountText} {withdrawal.Currency} was approved. This approval does not confirm that a bank transfer has occurred."
                : $"Your withdrawal request for {amountText} {withdrawal.Currency} was rejected; the reserved amount is available again.");

        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = withdrawal.TeacherId,
            Type = NotificationType.Payment,
            Title = title,
            Message = message,
            DataJson = JsonSerializer.Serialize(new
            {
                withdrawalId = withdrawal.Id,
                status = targetStatus,
                amount = withdrawal.Amount,
                currency = withdrawal.Currency
            }),
            IsRead = false,
            CreatedAt = now,
            ActionUrl = "/teacher",
            ActionLabel = fa ? "ظ…ط´ط§ظ‡ط¯ظ‡ ط¯ط§ط´ط¨ظˆط±ط¯ ظ…ط¯ط±ط³" : "View teacher dashboard"
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            id = withdrawal.Id,
            teacherId = withdrawal.TeacherId,
            teacherName = string.IsNullOrWhiteSpace(teacher.FullName)
                ? (teacher.FirstName + " " + teacher.FamilyName).Trim()
                : teacher.FullName,
            teacherEmail = teacher.Email ?? string.Empty,
            amount = withdrawal.Amount,
            currency = withdrawal.Currency,
            iban = withdrawal.Iban,
            status = withdrawal.Status,
            requestedAt = withdrawal.RequestedAt,
            decidedAt = withdrawal.DecidedAt,
            decidedById = withdrawal.DecidedById
        });
    }

    private Guid? GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}