using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
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
        var entity = new Domain.Entities.BlogPost
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Excerpt = request.Excerpt,
            Content = request.Content,
            ImageUrl = request.ImageUrl,
            Language = request.Language,
            IsPublished = request.IsPublished,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Domain.Entities.BlogPost>().Add(entity);
        await _context.SaveChangesAsync();

        return new BlogPostDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Excerpt = entity.Excerpt,
            ImageUrl = entity.ImageUrl,
            Language = entity.Language,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<BlogPostDto?> UpdateAsync(Guid id, UpdateBlogPostRequest request)
    {
        var entity = await _context.Set<Domain.Entities.BlogPost>().FirstOrDefaultAsync(b => b.Id == id);
        if (entity is null) return null;

        entity.Title = request.Title;
        entity.Excerpt = request.Excerpt;
        entity.Content = request.Content;
        entity.ImageUrl = request.ImageUrl;
        entity.Language = request.Language;
        entity.IsPublished = request.IsPublished;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new BlogPostDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Excerpt = entity.Excerpt,
            ImageUrl = entity.ImageUrl,
            Language = entity.Language,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Content = entity.Content
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.Set<Domain.Entities.BlogPost>().FirstOrDefaultAsync(b => b.Id == id);
        if (entity is null) return false;

        _context.Set<Domain.Entities.BlogPost>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<BlogPostDto>> GetPublishedPostsAsync(string language)
    {
        var query = _context.Set<Domain.Entities.BlogPost>()
            .Where(b => b.IsPublished && b.Language == language)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BlogPostDto
            {
                Id = b.Id,
                Title = b.Title,
                Excerpt = b.Excerpt,
                ImageUrl = b.ImageUrl,
                Language = b.Language,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            });

        return await query.ToListAsync();
    }

    public async Task<BlogPostDto?> GetPublishedPostByIdAsync(Guid id)
    {
        var post = await _context.Set<Domain.Entities.BlogPost>()
            .Where(b => b.IsPublished && b.Id == id)
            .Select(b => new BlogPostDto
            {
                Id = b.Id,
                Title = b.Title,
                Excerpt = b.Excerpt,
                ImageUrl = b.ImageUrl,
                Language = b.Language,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                Content = b.Content
            })
            .FirstOrDefaultAsync();

        return post;
    }
}
