using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using System.Linq.Expressions;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/courses")]
public class SearchController : ControllerBase
{
    private readonly SeOneDbContext _db;
    public SearchController(SeOneDbContext db) => _db = db;

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? level, [FromQuery] string? category, [FromQuery] string? language, [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] Guid? teacherId, [FromQuery] bool? hasDiscount, [FromQuery] bool? featured, [FromQuery] decimal? minRating, [FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string sort = "relevance", [FromQuery] string? lang = null)
    {
        page = Math.Clamp(page, 1, 10000); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Set<Course>().AsNoTracking().Where(c => c.IsPublished);
        if (!string.IsNullOrWhiteSpace(lang)) query = query.Where(c => c.Language == lang || c.Language == "en");
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.Title.Contains(q) || c.Description.Contains(q) || c.Category.Contains(q));
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(c => c.Level == level);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(c => c.Category == category);
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(c => c.Language == language);
        if (minPrice.HasValue) query = query.Where(c => c.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(c => c.Price <= maxPrice.Value);
        if (teacherId.HasValue)
        {
            query = query.Where(c =>
                _db.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId.Value &&
                        t.CourseInstance.CourseId == c.Id));
        }
        if (hasDiscount == true) query = query.Where(c => c.DiscountPercent > 0);
        if (featured == true) query = query.Where(c => c.IsFeatured);
        if (minRating.HasValue) query = query.Where(c => _db.Set<Review>().Where(r => r.CourseId == c.Id).Select(r => (double?)r.Rating).Average() >= (double)minRating.Value);

        query = sort.ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(c => c.Price * (1m - c.DiscountPercent / 100m)),
            "price_desc" => query.OrderByDescending(c => c.Price * (1m - c.DiscountPercent / 100m)),
            "rating" => query.OrderByDescending(c => _db.Set<Review>().Where(r => r.CourseId == c.Id).Select(r => (double?)r.Rating).Average() ?? 0),
            "popularity" => query.OrderByDescending(c => _db.Set<Enrollment>().Count(e => e.CourseId == c.Id)),
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            _ => query.OrderByDescending(c => c.IsFeatured).ThenByDescending(c => c.CreatedAt)
        };

        var total = await query.CountAsync();
        var courses = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(c => new
        {
            id = c.Id, title = c.Title, level = c.Level, desc = c.Description,
            lessons = _db.Set<Lesson>().Count(l => l.CourseModule.CourseId == c.Id),
            students = _db.Set<Enrollment>().Count(e => e.CourseId == c.Id),
            basePrice = c.Price, price = c.Price * (1m - c.DiscountPercent / 100m), currency = c.Currency,
            duration = c.Duration, image = c.ImageUrl, category = c.Category, language = c.Language,
            discountPercent = c.DiscountPercent, featured = c.IsFeatured,
            teacher = _db.Set<CourseInstanceTeacher>()
    .Where(t => t.CourseInstance.CourseId == c.Id)
    .OrderBy(t => t.CourseInstance.CreatedAt)
    .ThenBy(t => t.CreatedAt)
    .Select(t => new
    {
        id = t.TeacherId,
        name = t.Teacher.FullName,
        avatar = t.Teacher.AvatarUrl
    })
    .FirstOrDefault(),
            rating = _db.Set<Review>().Where(r => r.CourseId == c.Id).Select(r => (double?)r.Rating).Average() ?? 0,
            reviewCount = _db.Set<Review>().Count(r => r.CourseId == c.Id)
        }).ToListAsync();

        var levels = await _db.Set<Course>().Where(c => c.IsPublished).GroupBy(c => c.Level).Select(g => new { value = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync();
        var categories = await _db.Set<Course>().Where(c => c.IsPublished).GroupBy(c => c.Category).Select(g => new { value = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync();
        var languages = await _db.Set<Course>().Where(c => c.IsPublished).GroupBy(c => c.Language).Select(g => new { value = g.Key, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync();
        var facets = new { levels, categories, languages, priceRanges = new[] { new { label = "Free", min = 0m, max = 0m, count = 0 }, new { label = "Under 1M", min = 1m, max = 999999m, count = 0 }, new { label = "1M+", min = 1000000m, max = decimal.MaxValue, count = 0 } } };
        return Ok(new { courses, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize), facets });
    }

    [HttpGet("suggestions")]
    [AllowAnonymous]
    public async Task<IActionResult> Suggestions([FromQuery] string? q, [FromQuery] string? lang)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new { courses = Array.Empty<object>(), queries = Array.Empty<string>() });
        var query = _db.Set<Course>().AsNoTracking().Where(c => c.IsPublished && (c.Title.Contains(q) || c.Category.Contains(q)));
        if (!string.IsNullOrWhiteSpace(lang)) query = query.Where(c => c.Language == lang || c.Language == "en");
        var courses = await query.OrderByDescending(c => c.IsFeatured).Take(8).Select(c => new { id = c.Id, title = c.Title, category = c.Category }).ToListAsync();
        var queries = await _db.Set<Course>().Where(c => c.IsPublished && c.Title.Contains(q)).Select(c => c.Title).Distinct().Take(5).ToListAsync();
        return Ok(new { courses, queries });
    }

    [HttpGet("filters")]
    [AllowAnonymous]
    public async Task<IActionResult> Filters([FromQuery] string? lang)
    {
        var courses = _db.Set<Course>().AsNoTracking().Where(c => c.IsPublished);
        if (!string.IsNullOrWhiteSpace(lang)) courses = courses.Where(c => c.Language == lang || c.Language == "en");
        var levels = await courses.GroupBy(c => c.Level).Select(g => new { value = g.Key, label = g.Key, count = g.Count() }).ToListAsync();
        var categories = await courses.GroupBy(c => c.Category).Select(g => new { value = g.Key, label = g.Key, count = g.Count() }).ToListAsync();
        var languages = await courses.GroupBy(c => c.Language).Select(g => new { value = g.Key, label = g.Key, count = g.Count() }).ToListAsync();
        var teachers = await _db.Set<CourseInstanceTeacher>()
    .Where(t =>
        t.CourseInstance.Course.IsPublished &&
        (string.IsNullOrWhiteSpace(lang) ||
         t.CourseInstance.Course.Language == lang ||
         t.CourseInstance.Course.Language == "en"))
    .GroupBy(t => new
    {
        t.TeacherId,
        t.Teacher.FullName,
        t.Teacher.AvatarUrl
    })
    .Select(g => new
    {
        id = g.Key.TeacherId,
        name = g.Key.FullName,
        avatar = g.Key.AvatarUrl
    })
    .ToListAsync();
        return Ok(new { levels, categories, languages, teachers, priceRanges = new[] { new { label = "Free", min = 0m, max = 0m }, new { label = "Under 1M", min = 1m, max = 999999m }, new { label = "1M+", min = 1000000m, max = decimal.MaxValue } } });
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    public Task<IActionResult> Featured([FromQuery] string? lang, [FromQuery] int limit = 8) => ListCurated(lang, Math.Clamp(limit, 1, 50), c => c.IsFeatured);

    [HttpGet("trending")]
    [AllowAnonymous]
    public Task<IActionResult> Trending([FromQuery] string? lang, [FromQuery] int limit = 8) => ListCurated(lang, Math.Clamp(limit, 1, 50), null);

    [HttpGet("similar/{courseId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Similar(Guid courseId, [FromQuery] string? lang, [FromQuery] int limit = 8)
    {
        var source = await _db.Set<Course>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId);
        if (source is null) return NotFound();
        var query = _db.Set<Course>().AsNoTracking().Where(c => c.IsPublished && c.Id != courseId && (c.Category == source.Category || c.Level == source.Level));
        if (!string.IsNullOrWhiteSpace(lang)) query = query.Where(c => c.Language == lang || c.Language == "en");
        return Ok(await Shape(query.OrderByDescending(c => c.IsFeatured).ThenByDescending(c => c.CreatedAt).Take(Math.Clamp(limit, 1, 50))).ToListAsync());
    }

    private async Task<IActionResult> ListCurated(string? lang, int limit, Expression<Func<Course, bool>>? predicate)
    {
        var query = _db.Set<Course>().AsNoTracking().Where(c => c.IsPublished);
        if (!string.IsNullOrWhiteSpace(lang)) query = query.Where(c => c.Language == lang || c.Language == "en");
        if (predicate is not null) query = query.Where(predicate);
        if (predicate is not null) return Ok(await Shape(query.OrderByDescending(c => c.CreatedAt).Take(limit)).ToListAsync());
        var popularIds = await _db.Set<Enrollment>().GroupBy(e => e.CourseId).OrderByDescending(g => g.Count()).Take(limit).Select(g => g.Key).ToListAsync();
        return Ok(await Shape(query.Where(c => popularIds.Contains(c.Id)).OrderByDescending(c => c.CreatedAt).Take(limit)).ToListAsync());
    }

    private IQueryable<object> Shape(IQueryable<Course> query) => query.Select(c => new
    {
        id = c.Id, title = c.Title, level = c.Level, desc = c.Description,
        lessons = _db.Set<Lesson>().Count(l => l.CourseModule.CourseId == c.Id),
        students = _db.Set<Enrollment>().Count(e => e.CourseId == c.Id),
        basePrice = c.Price, price = c.Price * (1m - c.DiscountPercent / 100m), currency = c.Currency,
        duration = c.Duration, image = c.ImageUrl, category = c.Category, language = c.Language,
        discountPercent = c.DiscountPercent, featured = c.IsFeatured,
        teacher = _db.Set<CourseInstanceTeacher>()
    .Where(t => t.CourseInstance.CourseId == c.Id)
    .OrderBy(t => t.CourseInstance.CreatedAt)
    .ThenBy(t => t.CreatedAt)
    .Select(t => new
    {
        id = t.TeacherId,
        name = t.Teacher.FullName,
        avatar = t.Teacher.AvatarUrl
    })
    .FirstOrDefault(),
        rating = _db.Set<Review>().Where(r => r.CourseId == c.Id).Select(r => (double?)r.Rating).Average() ?? 0,
        reviewCount = _db.Set<Review>().Count(r => r.CourseId == c.Id)
    });
}
