using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class BlogService : IBlogService
{
    private readonly SeOneDbContext _context;

    public BlogService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<BlogPostDto> CreateAsync(CreateBlogPostRequest request)
    {
        var entity = new BlogPost
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Excerpt = request.Excerpt.Trim(),
            Content = request.Content,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl)
                ? null
                : request.ImageUrl.Trim(),
            Author = request.Author.Trim(),
            ReadTime = request.ReadTime.Trim(),
            Category = request.Category.Trim(),
            Language = request.Language.Trim().ToLowerInvariant(),
            IsPublished = request.IsPublished,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<BlogPost>().Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<BlogPostDto?> UpdateAsync(
        Guid id,
        UpdateBlogPostRequest request)
    {
        var entity = await _context.Set<BlogPost>()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity is null)
            return null;

        entity.Title = request.Title.Trim();
        entity.Excerpt = request.Excerpt.Trim();
        entity.Content = request.Content;
        entity.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl)
            ? null
            : request.ImageUrl.Trim();
        entity.Author = request.Author.Trim();
        entity.ReadTime = request.ReadTime.Trim();
        entity.Category = request.Category.Trim();
        entity.Language = request.Language.Trim().ToLowerInvariant();
        entity.IsPublished = request.IsPublished;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.Set<BlogPost>()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity is null)
            return false;

        _context.Set<BlogPost>().Remove(entity);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<BlogPostDto>> GetPublishedPostsAsync(
        string language)
    {
        var normalizedLanguage = string.IsNullOrWhiteSpace(language)
            ? "en"
            : language.Trim().ToLowerInvariant();

        return await _context.Set<BlogPost>()
            .AsNoTracking()
            .Where(b =>
                b.IsPublished &&
                b.Language == normalizedLanguage)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BlogPostDto
            {
                Id = b.Id,
                Title = b.Title,
                Excerpt = b.Excerpt,
                ImageUrl = b.ImageUrl,
                Author = b.Author,
                ReadTime = b.ReadTime,
                Category = b.Category,
                Language = b.Language,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<BlogPostDto?> GetPublishedPostByIdAsync(Guid id)
    {
        return await _context.Set<BlogPost>()
            .AsNoTracking()
            .Where(b =>
                b.IsPublished &&
                b.Id == id)
            .Select(b => new BlogPostDto
            {
                Id = b.Id,
                Title = b.Title,
                Excerpt = b.Excerpt,
                ImageUrl = b.ImageUrl,
                Author = b.Author,
                ReadTime = b.ReadTime,
                Category = b.Category,
                Language = b.Language,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                Content = b.Content
            })
            .FirstOrDefaultAsync();
    }

    private static BlogPostDto MapToDto(BlogPost entity)
    {
        return new BlogPostDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Excerpt = entity.Excerpt,
            ImageUrl = entity.ImageUrl,
            Author = entity.Author,
            ReadTime = entity.ReadTime,
            Category = entity.Category,
            Language = entity.Language,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Content = entity.Content
        };
    }
}