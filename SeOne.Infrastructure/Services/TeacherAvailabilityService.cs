using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class TeacherAvailabilityService : ITeacherAvailabilityService
{
    private readonly SeOneDbContext _context;

    public TeacherAvailabilityService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<TeacherAvailabilityDto>> GetByTeacherAsync(Guid teacherId)
    {
        // Ensure user is a teacher
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);

        if (user is null)
            return new List<TeacherAvailabilityDto>();

        return await _context.Set<Domain.Entities.TeacherAvailability>()
            .Where(a => a.TeacherId == teacherId)
            .OrderBy(a => a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .Select(a => new TeacherAvailabilityDto
            {
                Id = a.Id,
                TeacherId = a.TeacherId,
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                IsAvailable = a.IsAvailable,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<TeacherAvailabilityDto>> GetMyAvailabilityAsync(Guid teacherId)
    {
        return await GetByTeacherAsync(teacherId);
    }

    public async Task<TeacherAvailabilityDto?> CreateAsync(Guid teacherId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime, bool isAvailable)
    {
        if (endTime <= startTime)
            return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);

        if (user is null)
            return null;

        // prevent duplicates
        var exists = await _context.Set<Domain.Entities.TeacherAvailability>()
            .AnyAsync(a => a.TeacherId == teacherId && a.DayOfWeek == dayOfWeek && a.StartTime == startTime && a.EndTime == endTime);

        if (exists)
            return null;

        var avail = new Domain.Entities.TeacherAvailability
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            IsAvailable = isAvailable,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Domain.Entities.TeacherAvailability>().Add(avail);
        await _context.SaveChangesAsync();

        return new TeacherAvailabilityDto
        {
            Id = avail.Id,
            TeacherId = avail.TeacherId,
            DayOfWeek = avail.DayOfWeek,
            StartTime = avail.StartTime,
            EndTime = avail.EndTime,
            IsAvailable = avail.IsAvailable,
            CreatedAt = avail.CreatedAt
        };
    }

    public async Task<TeacherAvailabilityDto?> UpdateAsync(Guid id, Guid teacherId, TimeSpan startTime, TimeSpan endTime, bool isAvailable)
    {
        if (endTime <= startTime)
            return null;

        var avail = await _context.Set<Domain.Entities.TeacherAvailability>()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (avail is null)
            return null;

        if (avail.TeacherId != teacherId)
            return null;

        // prevent duplicates (excluding this record)
        var dup = await _context.Set<Domain.Entities.TeacherAvailability>()
            .AnyAsync(a => a.Id != id && a.TeacherId == teacherId && a.DayOfWeek == avail.DayOfWeek && a.StartTime == startTime && a.EndTime == endTime);

        if (dup)
            return null;

        avail.StartTime = startTime;
        avail.EndTime = endTime;
        avail.IsAvailable = isAvailable;

        await _context.SaveChangesAsync();

        return new TeacherAvailabilityDto
        {
            Id = avail.Id,
            TeacherId = avail.TeacherId,
            DayOfWeek = avail.DayOfWeek,
            StartTime = avail.StartTime,
            EndTime = avail.EndTime,
            IsAvailable = avail.IsAvailable,
            CreatedAt = avail.CreatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid id, Guid teacherId)
    {
        var avail = await _context.Set<Domain.Entities.TeacherAvailability>().FirstOrDefaultAsync(a => a.Id == id);

        if (avail is null)
            return false;

        if (avail.TeacherId != teacherId)
            return false;

        _context.Set<Domain.Entities.TeacherAvailability>().Remove(avail);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task DeleteAllAsync(Guid teacherId)
    {
        var items = await _context.Set<Domain.Entities.TeacherAvailability>().Where(a => a.TeacherId == teacherId).ToListAsync();

        if (items.Any())
        {
            _context.Set<Domain.Entities.TeacherAvailability>().RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }
}
