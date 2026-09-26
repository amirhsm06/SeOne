using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly SeOneDbContext _context;

    public BookingService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<BookingDto?> CreateAsync(Guid studentId, CreateBookingRequest request)
    {
        var teacher = await _context.Users.FirstOrDefaultAsync(u =>
            u.Id == request.TeacherId &&
            u.Role == UserRole.Teacher &&
            u.AccountStatus == "active");

        if (teacher is null)
            return null;

        if (request.SlotId.HasValue)
        {
            var slot = await _context.Set<TeacherAvailability>()
                .FirstOrDefaultAsync(a =>
                    a.Id == request.SlotId.Value &&
                    a.TeacherId == request.TeacherId &&
                    a.IsAvailable);

            if (slot is null)
                return null;

            var next = FindNextOccurrence(DateTime.UtcNow, slot.DayOfWeek, slot.StartTime);
            request.StartTime = next;
            request.EndTime = next.Date.Add(slot.EndTime);
        }

        if (request.EndTime <= request.StartTime || request.StartTime <= DateTime.UtcNow)
            return null;

        var day = request.StartTime.DayOfWeek;
        var startTime = request.StartTime.TimeOfDay;
        var endTime = request.EndTime.TimeOfDay;

        var availabilityExists = await _context.Set<TeacherAvailability>()
            .AnyAsync(a =>
                a.TeacherId == request.TeacherId &&
                a.DayOfWeek == day &&
                a.IsAvailable &&
                a.StartTime <= startTime &&
                a.EndTime >= endTime);

        if (!availabilityExists)
            return null;

        var teacherOverlap = await _context.Set<Booking>().AnyAsync(b =>
            b.TeacherId == request.TeacherId &&
            b.Status != BookingStatus.Cancelled &&
            !(b.EndTime <= request.StartTime || b.StartTime >= request.EndTime));

        if (teacherOverlap)
            return null;

        var studentOverlap = await _context.Set<Booking>().AnyAsync(b =>
            b.StudentId == studentId &&
            b.Status != BookingStatus.Cancelled &&
            !(b.EndTime <= request.StartTime || b.StartTime >= request.EndTime));

        if (studentOverlap)
            return null;

        Guid? courseId = request.CourseId;

        if (courseId.HasValue)
        {
            var validCourse = await _context.Set<Course>().AnyAsync(c =>
                c.Id == courseId.Value &&
                c.TeacherId == request.TeacherId &&
                c.IsPublished);

            if (!validCourse)
                return null;

            var enrolled = await _context.Set<Enrollment>().AnyAsync(e =>
                e.StudentId == studentId && e.CourseId == courseId.Value);

            if (!enrolled)
                return null;
        }
        else
        {
            courseId = await _context.Set<Enrollment>()
                .Where(e =>
                    e.StudentId == studentId &&
                    e.Course.TeacherId == request.TeacherId &&
                    e.Course.IsPublished)
                .OrderByDescending(e => e.EnrolledAt)
                .Select(e => (Guid?)e.CourseId)
                .FirstOrDefaultAsync();
        }

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            TeacherId = request.TeacherId,
            CourseId = courseId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Booking>().Add(booking);
        await _context.SaveChangesAsync();

        return await ToDtoAsync(booking.Id);
    }

    public async Task<List<BookingDto>> GetMyBookingsAsync(Guid studentId)
    {
        return await _context.Set<Booking>()
            .Where(b => b.StudentId == studentId)
            .OrderByDescending(b => b.StartTime)
            .Select(MapProjection())
            .ToListAsync();
    }

    public async Task<bool> CancelMyBookingAsync(Guid bookingId, Guid studentId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId && b.StudentId == studentId);

        if (booking is null || booking.Status is BookingStatus.Cancelled or BookingStatus.Completed)
            return false;

        booking.Status = BookingStatus.Cancelled;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<BookingDto>> GetTeacherBookingsAsync(Guid teacherId)
    {
        return await _context.Set<Booking>()
            .Where(b => b.TeacherId == teacherId)
            .OrderByDescending(b => b.StartTime)
            .Select(MapProjection())
            .ToListAsync();
    }

    public async Task<bool> ConfirmBookingAsync(Guid bookingId, Guid teacherId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId && b.TeacherId == teacherId);

        if (booking is null || booking.Status != BookingStatus.Pending)
            return false;

        var overlap = await _context.Set<Booking>().AnyAsync(b =>
            b.Id != booking.Id &&
            b.TeacherId == teacherId &&
            b.Status == BookingStatus.Confirmed &&
            !(b.EndTime <= booking.StartTime || b.StartTime >= booking.EndTime));

        if (overlap)
            return false;

        booking.Status = BookingStatus.Confirmed;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelBookingByTeacherAsync(Guid bookingId, Guid teacherId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId && b.TeacherId == teacherId);

        if (booking is null || booking.Status is BookingStatus.Cancelled or BookingStatus.Completed)
            return false;

        booking.Status = BookingStatus.Cancelled;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteBookingAsync(Guid bookingId, Guid teacherId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId && b.TeacherId == teacherId);

        if (booking is null || booking.Status != BookingStatus.Confirmed)
            return false;

        booking.Status = BookingStatus.Completed;
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<BookingDto?> ToDtoAsync(Guid bookingId)
    {
        return await _context.Set<Booking>()
            .Where(b => b.Id == bookingId)
            .Select(MapProjection())
            .FirstOrDefaultAsync();
    }

    private static Expression<Func<Booking, BookingDto>> MapProjection()
    {
        return b => new BookingDto
        {
            Id = b.Id,
            StudentId = b.StudentId,
            TeacherId = b.TeacherId,
            CourseId = b.CourseId,
            CourseTitle = b.Course == null ? string.Empty : b.Course.Title,
            TeacherName = b.Teacher.FullName,
            StudentName = b.Student.FullName,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            Status = b.Status.ToString(),
            CreatedAt = b.CreatedAt
        };
    }

    private static DateTime FindNextOccurrence(DateTime fromUtc, DayOfWeek day, TimeSpan time)
    {
        var date = fromUtc.Date.AddDays(((int)day - (int)fromUtc.DayOfWeek + 7) % 7);
        var candidate = date.Add(time);
        if (candidate <= fromUtc)
            candidate = candidate.AddDays(7);
        return DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
    }
}
