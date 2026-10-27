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

    public async Task<List<TeacherAvailabilityDto>?> ReplaceAvailabilityAsync(Guid teacherId, IReadOnlyCollection<TeacherAvailabilityReplacementSlotDto> slots)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);
        if (user is null || slots is null)
            return null;

        var dayLength = TimeSpan.FromDays(1);
        if (slots.Any(slot =>
                !Enum.IsDefined(slot.DayOfWeek) ||
                slot.StartTime < TimeSpan.Zero || slot.StartTime >= dayLength ||
                slot.EndTime <= slot.StartTime || slot.EndTime >= dayLength))
            return null;

        if (slots.Where(slot => slot.Id.HasValue).Select(slot => slot.Id).Distinct().Count() != slots.Count(slot => slot.Id.HasValue))
            return null;

        var requestedSlots = slots.ToList();
        for (var i = 0; i < requestedSlots.Count; i++)
        {
            for (var j = i + 1; j < requestedSlots.Count; j++)
            {
                var first = requestedSlots[i];
                var second = requestedSlots[j];
                if (first.DayOfWeek == second.DayOfWeek &&
                    first.StartTime < second.EndTime && second.StartTime < first.EndTime)
                    return null;
            }
        }

        var availability = _context.Set<Domain.Entities.TeacherAvailability>();
        var existingSlots = await availability.Where(slot => slot.TeacherId == teacherId).ToListAsync();
        var existingById = existingSlots.ToDictionary(slot => slot.Id);
        if (requestedSlots.Any(slot => slot.Id.HasValue && !existingById.ContainsKey(slot.Id.Value)))
            return null;

        var retainedIds = requestedSlots.Where(slot => slot.Id.HasValue).Select(slot => slot.Id!.Value).ToHashSet();
        availability.RemoveRange(existingSlots.Where(slot => !retainedIds.Contains(slot.Id)));

        foreach (var slot in requestedSlots)
        {
            if (slot.Id is Guid id)
            {
                var existing = existingById[id];
                existing.DayOfWeek = slot.DayOfWeek;
                existing.StartTime = slot.StartTime;
                existing.EndTime = slot.EndTime;
            }
            else
            {
                availability.Add(new Domain.Entities.TeacherAvailability
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherId,
                    DayOfWeek = slot.DayOfWeek,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        return await availability
            .Where(slot => slot.TeacherId == teacherId)
            .OrderBy(slot => slot.DayOfWeek)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => new TeacherAvailabilityDto
            {
                Id = slot.Id,
                TeacherId = slot.TeacherId,
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                IsAvailable = slot.IsAvailable,
                CreatedAt = slot.CreatedAt
            })
            .ToListAsync();
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
