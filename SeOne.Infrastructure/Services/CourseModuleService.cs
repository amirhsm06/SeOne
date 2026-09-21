using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Persistence;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Services;

public class CourseModuleService : ICourseModuleService
{
    private readonly SeOneDbContext _context;

    public CourseModuleService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<CourseModuleDto> CreateAsync(Guid teacherId, Guid courseId, string title, string description, int order)
    {
        var course = await _context.Set<Course>().FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null || course.TeacherId != teacherId)
            return null!; // callers should handle null; keep signature as specified (but returning null will cause runtime NRE if not handled) - adjust to throw or return null via nullable signature? follow spec: return CourseModuleDto, but we'll return null and controller will handle

        var module = new CourseModule
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Title = title,
            Description = description,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<CourseModule>().Add(module);
        await _context.SaveChangesAsync();

        return new CourseModuleDto
        {
            Id = module.Id,
            CourseId = module.CourseId,
            Title = module.Title,
            Description = module.Description,
            Order = module.Order,
            CreatedAt = module.CreatedAt
        };
    }

    public async Task<List<CourseModuleDto>> GetByCourseAsync(Guid courseId, Guid userId, string userRole)
    {
        var course = await _context.Set<Course>().FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return new List<CourseModuleDto>();

        if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            if (!course.IsPublished)
                return new List<CourseModuleDto>();
        }
        else if (string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            if (course.TeacherId != userId)
                return new List<CourseModuleDto>();
        }
        else
        {
            if (!course.IsPublished)
                return new List<CourseModuleDto>();
        }

        return await _context.Set<CourseModule>()
            .Where(m => m.CourseId == courseId)
            .OrderBy(m => m.Order)
            .Select(m => new CourseModuleDto
            {
                Id = m.Id,
                CourseId = m.CourseId,
                Title = m.Title,
                Description = m.Description,
                Order = m.Order,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<CourseModuleDto?> UpdateAsync(Guid moduleId, Guid teacherId, string title, string description, int order)
    {
        var module = await _context.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module is null)
            return null;

        if (module.Course.TeacherId != teacherId)
            return null;

        module.Title = title;
        module.Description = description;
        module.Order = order;

        await _context.SaveChangesAsync();

        return new CourseModuleDto
        {
            Id = module.Id,
            CourseId = module.CourseId,
            Title = module.Title,
            Description = module.Description,
            Order = module.Order,
            CreatedAt = module.CreatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid moduleId, Guid teacherId)
    {
        var module = await _context.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module is null)
            return false;

        if (module.Course.TeacherId != teacherId)
            return false;

        _context.Set<CourseModule>().Remove(module);
        await _context.SaveChangesAsync();

        return true;
    }
}
