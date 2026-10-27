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
                UserId = u.Id,
                Name = u.FullName,
                Email = u.Email ?? string.Empty,
                AvatarUrl = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Avatar).FirstOrDefault() ?? u.AvatarUrl ?? string.Empty,
                Status = u.AccountStatus,
                // join profile if exists
                Avatar = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.Avatar).FirstOrDefault(),
                VideoUrl = _context.Set<Domain.Entities.TeacherProfile>().Where(p => p.TeacherId == u.Id).Select(p => p.VideoUrl).FirstOrDefault(),
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
            UserId = user.Id,
            Name = user.FullName,
            Email = user.Email ?? string.Empty,
            AvatarUrl = profile?.Avatar ?? user.AvatarUrl ?? string.Empty,
            Status = user.AccountStatus,
            Avatar = profile?.Avatar,
            VideoUrl = profile?.VideoUrl,
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

    public async Task<TeacherDto?> UpdateMyProfileAsync(Guid teacherId, string? avatar, string? videoUrl, string? teachingLanguage, string? subject, string? level, string? bio)
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
                VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim(),
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
            if (avatar is not null)
                profile.Avatar = avatar;

            if (videoUrl is not null)
                profile.VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();

            if (teachingLanguage is not null)
                profile.TeachingLanguage = teachingLanguage;

            if (subject is not null)
                profile.Subject = subject;

            if (level is not null)
                profile.Level = level;

            if (bio is not null)
                profile.Bio = bio;

            // rating not set by client
        }

        await _context.SaveChangesAsync();

        return new TeacherDto
        {
            Id = user.Id,
            UserId = user.Id,
            Name = user.FullName,
            Email = user.Email ?? string.Empty,
            AvatarUrl = profile.Avatar ?? user.AvatarUrl ?? string.Empty,
            Status = user.AccountStatus,
            Avatar = profile.Avatar,
            VideoUrl = profile.VideoUrl,
            TeachingLanguage = profile.TeachingLanguage,
            Subject = profile.Subject,
            Level = profile.Level,
            Bio = profile.Bio,
            Rating = profile.Rating
        };
    }
    public async Task<bool> UpdateMyVideoUrlAsync(
    Guid teacherId,
    string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return false;

        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.Id == teacherId && u.Role == UserRole.Teacher);

        if (user is null)
            return false;

        var profile = await _context
            .Set<Domain.Entities.TeacherProfile>()
            .FirstOrDefaultAsync(p => p.TeacherId == teacherId);

        if (profile is null)
        {
            profile = new Domain.Entities.TeacherProfile
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                VideoUrl = videoUrl,
                Rating = 0m
            };

            _context.Set<Domain.Entities.TeacherProfile>().Add(profile);
        }
        else
        {
            profile.VideoUrl = videoUrl;
        }

        await _context.SaveChangesAsync();

        return true;
    }
}
