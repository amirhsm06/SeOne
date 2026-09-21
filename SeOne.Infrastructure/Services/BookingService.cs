using Microsoft.EntityFrameworkCore;
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
        if (request.EndTime <= request.StartTime)
            return null;

        if (request.StartTime < DateTime.UtcNow)
            return null;

        // Verify teacher exists and is a teacher
        var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.TeacherId && u.Role == UserRole.Teacher);
        if (teacher is null)
            return null;

        // Check teacher availability: find any availability slot on the same day-of-week where requested times fall within
        var day = request.StartTime.DayOfWeek;
        var timeOfDayStart = request.StartTime.TimeOfDay;
        var timeOfDayEnd = request.EndTime.TimeOfDay;

        var avail = await _context.Set<TeacherAvailability>()
            .Where(a => a.TeacherId == request.TeacherId && a.DayOfWeek == day && a.IsAvailable)
            .AnyAsync(a => a.StartTime <= timeOfDayStart && a.EndTime >= timeOfDayEnd);

        if (!avail)
            return null;

        // Prevent overlapping bookings for teacher
        var teacherOverlap = await _context.Set<Booking>()
            .AnyAsync(b => b.TeacherId == request.TeacherId && b.Status != BookingStatus.Cancelled &&
                !(b.EndTime <= request.StartTime || b.StartTime >= request.EndTime));

        if (teacherOverlap)
            return null;

        // Prevent overlapping bookings for student
        var studentOverlap = await _context.Set<Booking>()
            .AnyAsync(b => b.StudentId == studentId && b.Status != BookingStatus.Cancelled &&
                !(b.EndTime <= request.StartTime || b.StartTime >= request.EndTime));

        if (studentOverlap)
            return null;

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            TeacherId = request.TeacherId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Booking>().Add(booking);
        await _context.SaveChangesAsync();

        return new BookingDto
        {
            Id = booking.Id,
            StudentId = booking.StudentId,
            TeacherId = booking.TeacherId,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Status = booking.Status.ToString(),
            CreatedAt = booking.CreatedAt
        };
    }

    public async Task<List<BookingDto>> GetMyBookingsAsync(Guid studentId)
    {
        return await _context.Set<Booking>()
            .Where(b => b.StudentId == studentId)
            .OrderByDescending(b => b.StartTime)
            .Select(b => new BookingDto
            {
                Id = b.Id,
                StudentId = b.StudentId,
                TeacherId = b.TeacherId,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Status = b.Status.ToString(),
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<bool> CancelMyBookingAsync(Guid bookingId, Guid studentId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
            return false;

        if (booking.StudentId != studentId)
            return false;

        if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
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
            .Select(b => new BookingDto
            {
                Id = b.Id,
                StudentId = b.StudentId,
                TeacherId = b.TeacherId,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Status = b.Status.ToString(),
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<bool> ConfirmBookingAsync(Guid bookingId, Guid teacherId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
            return false;

        if (booking.TeacherId != teacherId)
            return false;

        if (booking.Status != BookingStatus.Pending)
            return false;

        // ensure no overlap with other confirmed bookings for teacher
        var teacherOverlap = await _context.Set<Booking>()
            .AnyAsync(b => b.TeacherId == teacherId && b.Id != bookingId && b.Status == BookingStatus.Confirmed &&
                !(b.EndTime <= booking.StartTime || b.StartTime >= booking.EndTime));

        if (teacherOverlap)
            return false;

        booking.Status = BookingStatus.Confirmed;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelBookingByTeacherAsync(Guid bookingId, Guid teacherId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
            return false;

        if (booking.TeacherId != teacherId)
            return false;

        if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
            return false;

        booking.Status = BookingStatus.Cancelled;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteBookingAsync(Guid bookingId, Guid teacherId)
    {
        var booking = await _context.Set<Booking>().FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
            return false;

        if (booking.TeacherId != teacherId)
            return false;

        if (booking.Status != BookingStatus.Confirmed)
            return false;

        booking.Status = BookingStatus.Completed;
        await _context.SaveChangesAsync();
        return true;
    }
}
