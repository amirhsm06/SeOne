using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class WishlistService : IWishlistService
{
    private readonly SeOneDbContext _context;

    public WishlistService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<WishlistItemDto>> GetAsync(
        Guid studentId)
    {
        return await _context.Set<WishlistItem>()
            .Where(x =>
                x.StudentId == studentId &&
                x.Course.IsPublished)
            .OrderByDescending(x => x.AddedAt)
            .Select(x => new WishlistItemDto
            {
                Id = x.Id,
                CourseId = x.CourseId,
                CourseTitle = x.Course.Title,
                CourseImage = x.Course.ImageUrl,
                CoursePrice = x.Course.Price
                    .ToString(CultureInfo.InvariantCulture),
                AddedAt = x.AddedAt
            })
            .ToListAsync();
    }

    public async Task<WishlistItemDto?> AddAsync(
        Guid studentId,
        Guid courseId)
    {
        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(x =>
                x.Id == courseId &&
                x.IsPublished);

        if (course is null)
        {
            return null;
        }

        var existing = await _context.Set<WishlistItem>()
            .FirstOrDefaultAsync(x =>
                x.StudentId == studentId &&
                x.CourseId == courseId);

        if (existing is not null)
        {
            return new WishlistItemDto
            {
                Id = existing.Id,
                CourseId = course.Id,
                CourseTitle = course.Title,
                CourseImage = course.ImageUrl,
                CoursePrice = course.Price
                    .ToString(CultureInfo.InvariantCulture),
                AddedAt = existing.AddedAt
            };
        }

        var item = new WishlistItem
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            AddedAt = DateTime.UtcNow
        };

        _context.Set<WishlistItem>().Add(item);

        await _context.SaveChangesAsync();

        return new WishlistItemDto
        {
            Id = item.Id,
            CourseId = course.Id,
            CourseTitle = course.Title,
            CourseImage = course.ImageUrl,
            CoursePrice = course.Price
                .ToString(CultureInfo.InvariantCulture),
            AddedAt = item.AddedAt
        };
    }

    public async Task<bool> RemoveAsync(
        Guid studentId,
        Guid courseId)
    {
        var item = await _context.Set<WishlistItem>()
            .FirstOrDefaultAsync(x =>
                x.StudentId == studentId &&
                x.CourseId == courseId);

        if (item is null)
        {
            return false;
        }

        _context.Set<WishlistItem>().Remove(item);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ExistsAsync(
        Guid studentId,
        Guid courseId)
    {
        return await _context.Set<WishlistItem>()
            .AnyAsync(x =>
                x.StudentId == studentId &&
                x.CourseId == courseId);
    }
}