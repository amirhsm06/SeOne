using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Services;

public sealed record TeacherWithdrawalBalance(
    decimal AvailableBalance,
    string Currency,
    bool PolicyConfigured,
    bool HasMixedCurrencies);

/// <summary>
/// Uses the same completed-booking gross basis as the existing teacher earnings projection,
/// but does not assume a teacher share. A share must be explicitly configured per deployment.
/// </summary>
public static class TeacherWithdrawalLedger
{
    public const string SharePercentEnvironmentVariable = "SEONE_TEACHER_EARNINGS_SHARE_PERCENT";

    public static async Task<TeacherWithdrawalBalance> CalculateAsync(
        SeOneDbContext db,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        var completedBookings = await db.Bookings
            .AsNoTracking()
            .Include(b => b.Course)
            .Where(b => b.TeacherId == teacherId && b.Status == BookingStatus.Completed)
            .ToListAsync(cancellationToken);

        // Match the existing earnings calculation: each completed booking uses its Course.Price.
        var bookings = completedBookings
            .Select(b => new
            {
                Amount = b.Course?.Price ?? 0m,
                Currency = b.Course?.Currency
            })
            .ToList();

        var currencies = bookings
            .Select(b => string.IsNullOrWhiteSpace(b.Currency) ? "IRR" : b.Currency.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mixedCurrencies = currencies.Count > 1;
        var currency = currencies.Count == 1 ? currencies[0] : "IRR";
        var rawShare = Environment.GetEnvironmentVariable(SharePercentEnvironmentVariable);
        var policyConfigured = decimal.TryParse(
                rawShare,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var sharePercent) &&
            sharePercent > 0m && sharePercent <= 100m;

        // We cannot safely aggregate a balance across currencies or invent a commission rate.
        if (mixedCurrencies || !policyConfigured)
        {
            return new TeacherWithdrawalBalance(0m, currency, policyConfigured, mixedCurrencies);
        }

        var grossCompletedBookings = bookings
            .Where(b => (string.IsNullOrWhiteSpace(b.Currency) ? "IRR" : b.Currency.Trim().ToUpperInvariant()) == currency)
            .Where(b => b.Amount > 0m)
            .Sum(b => b.Amount);

        var reservedOrApproved = await db.TeacherWithdrawals
            .AsNoTracking()
            .Where(w => w.TeacherId == teacherId &&
                        w.Currency == currency &&
                        (w.Status == "pending" || w.Status == "approved"))
            .Select(w => (decimal?)w.Amount)
            .SumAsync(cancellationToken) ?? 0m;

        var earnedAmount = decimal.Round(
            grossCompletedBookings * sharePercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new TeacherWithdrawalBalance(
            Math.Max(0m, earnedAmount - reservedOrApproved),
            currency,
            true,
            false);
    }

    public static async Task<decimal> GetAvailableBalanceAsync(
        SeOneDbContext db,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        var result = await CalculateAsync(db, teacherId, cancellationToken);
        return result.AvailableBalance;
    }
}