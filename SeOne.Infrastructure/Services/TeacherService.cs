using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class TeacherService : ITeacherService
{
    private readonly SeOneDbContext _context;

    public TeacherService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<TeacherDto>> GetAllTeachersAsync()
    {
        return await _context.Users
            .Where(u => u.Role == UserRole.Teacher)
            .Select(u => new TeacherDto
            {
                Id = u.Id,
                Name = u.FullName,
                // join profile if exists
                Avatar = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Avatar).FirstOrDefault(),
                TeachingLanguage = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.TeachingLanguage).FirstOrDefault(),
                Subject = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Subject).FirstOrDefault(),
                Level = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Level).FirstOrDefault(),
                Bio = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Bio).FirstOrDefault(),
                Rating = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Rating).FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<TeacherDto?> GetTeacherByIdAsync(Guid teacherId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);

        if (user is null)
            return null;

        var profile = await _context.Set<Domain.Entities.TeacherProfile>().FirstOrDefaultAsync(p => p.TeacherId == teacherId);

        return new TeacherDto
        {
            Id = user.Id,
            Name = user.FullName,
            Avatar = profile?.Avatar,
            TeachingLanguage = profile?.TeachingLanguage,
            Subject = profile?.Subject,
            Level = profile?.Level,
            Bio = profile?.Bio,
            Rating = profile?.Rating ?? 0m
        };
    }

    public async Task<TeacherDto?> GetMyProfileAsync(Guid teacherId)
    {
        return await GetTeacherByIdAsync(teacherId);
    }

    public async Task<TeacherDto?> UpdateMyProfileAsync(Guid teacherId, string? avatar, string? teachingLanguage, string? subject, string? level, string? bio)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);

        if (user is null)
            return null;

        var profile = await _context.Set<Domain.Entities.TeacherProfile>().FirstOrDefaultAsync(p => p.TeacherId == teacherId);

        if (profile is null)
        {
            profile = new Domain.Entities.TeacherProfile
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                Avatar = avatar,
                TeachingLanguage = teachingLanguage,
                Subject = subject,
                Level = level,
                Bio = bio,
                Rating = 0m
            };

            _context.Set<Domain.Entities.TeacherProfile>().Add(profile);
        }
        else
        {
            profile.Avatar = avatar;
            profile.TeachingLanguage = teachingLanguage;
            profile.Subject = subject;
            profile.Level = level;
            profile.Bio = bio;
            // rating not set by client
        }

        await _context.SaveChangesAsync();

        return new TeacherDto
        {
            Id = user.Id,
            Name = user.FullName,
            Avatar = profile.Avatar,
            TeachingLanguage = profile.TeachingLanguage,
            Subject = profile.Subject,
            Level = profile.Level,
            Bio = profile.Bio,
            Rating = profile.Rating
        };
    }
}
