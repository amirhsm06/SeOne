using SeOne.Domain.Entities;

namespace SeOne.Api.Utilities;

/// <summary>
/// CourseClass stores a UTC start date and UTC wall-clock times. EndTime can be
/// earlier than StartTime when a valid class crosses UTC midnight.
/// </summary>
public static class ClassSlotTime
{
    public static DateTime GetStartUtc(CourseClass slot) =>
        GetStartUtc(slot.ScheduledDate, slot.StartTime);

    public static DateTime GetEndUtc(CourseClass slot) =>
        GetEndUtc(slot.ScheduledDate, slot.StartTime, slot.EndTime);

    public static DateTime GetStartUtc(DateTime scheduledDate, TimeSpan startTime) =>
        DateTime.SpecifyKind(scheduledDate.Date + startTime, DateTimeKind.Utc);

    public static DateTime GetEndUtc(DateTime scheduledDate, TimeSpan startTime, TimeSpan endTime)
    {
        var start = scheduledDate.Date + startTime;
        var end = scheduledDate.Date + endTime;
        if (end <= start)
            end = end.AddDays(1);
        return DateTime.SpecifyKind(end, DateTimeKind.Utc);
    }

    public static bool TryGetTimeZone(string? value, out TimeZoneInfo timeZone, out string timeZoneId)
    {
        timeZoneId = string.IsNullOrWhiteSpace(value) ? "UTC" : value.Trim();
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }

    public static bool Overlaps(DateTime firstStartUtc, DateTime firstEndUtc, DateTime secondStartUtc, DateTime secondEndUtc) =>
        firstStartUtc < secondEndUtc && secondStartUtc < firstEndUtc;
}
