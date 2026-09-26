using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly SeOneDbContext _context;

    public ReviewService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReviewDto>?> GetCourseReviewsAsync(
        Guid courseId,
        Guid? currentUserId)
    {
        var courseExists = await _context.Set<Course>()
            .AnyAsync(x =>
                x.Id == courseId &&
                x.IsPublished);

        if (!courseExists)
        {
            return null;
        }

        return await _context.Set<Review>()
            .AsNoTracking()
            .Where(x => x.CourseId == courseId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ReviewDto
            {
                Id = x.Id,
                CourseId = x.CourseId,
                UserId = x.StudentId,
                UserName = x.Student.FullName,
                UserAvatar = null,
                Rating = x.Rating,
                Title = x.Title,
                Content = x.Content,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                HelpfulCount = x.HelpfulMarks.Count(),
                IsHelpful = currentUserId.HasValue &&
                            x.HelpfulMarks.Any(h =>
                                h.UserId == currentUserId.Value)
            })
            .ToListAsync();
    }

    public async Task<CourseRatingSummaryDto?> GetCourseRatingSummaryAsync(
        Guid courseId)
    {
        var courseExists = await _context.Set<Course>()
            .AnyAsync(x =>
                x.Id == courseId &&
                x.IsPublished);

        if (!courseExists)
        {
            return null;
        }

        var ratings = await _context.Set<Review>()
            .AsNoTracking()
            .Where(x => x.CourseId == courseId)
            .Select(x => x.Rating)
            .ToListAsync();

        var distribution = new Dictionary<int, int>
        {
            [1] = ratings.Count(x => x == 1),
            [2] = ratings.Count(x => x == 2),
            [3] = ratings.Count(x => x == 3),
            [4] = ratings.Count(x => x == 4),
            [5] = ratings.Count(x => x == 5)
        };

        var average = ratings.Count == 0
            ? 0m
            : Math.Round(
                (decimal)ratings.Average(),
                2);

        return new CourseRatingSummaryDto
        {
            CourseId = courseId,
            AverageRating = average,
            TotalReviews = ratings.Count,
            RatingDistribution = distribution
        };
    }

    public async Task<ReviewDto?> CreateAsync(
        Guid studentId,
        Guid courseId,
        int rating,
        string title,
        string content)
    {
        if (rating < 1 || rating > 5)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(title) ||
            title.Length > 200)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > 2000)
        {
            return null;
        }

        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(x =>
                x.Id == courseId &&
                x.IsPublished);

        if (course is null)
        {
            return null;
        }

        var enrolled = await _context.Set<Enrollment>()
            .AnyAsync(x =>
                x.StudentId == studentId &&
                x.CourseId == courseId);

        if (!enrolled)
        {
            return null;
        }

        var alreadyReviewed = await _context.Set<Review>()
            .AnyAsync(x =>
                x.StudentId == studentId &&
                x.CourseId == courseId);

        if (alreadyReviewed)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        var review = new Review
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StudentId = studentId,
            Rating = rating,
            Title = title.Trim(),
            Content = content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Set<Review>().Add(review);

        await _context.SaveChangesAsync();

        var studentName = await _context.Set<User>()
            .Where(x => x.Id == studentId)
            .Select(x => x.FullName)
            .FirstAsync();

        return new ReviewDto
        {
            Id = review.Id,
            CourseId = review.CourseId,
            UserId = review.StudentId,
            UserName = studentName,
            UserAvatar = null,
            Rating = review.Rating,
            Title = review.Title,
            Content = review.Content,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            HelpfulCount = 0,
            IsHelpful = false
        };
    }

    public async Task<List<ReviewDto>> GetUserReviewsAsync(
        Guid studentId)
    {
        return await _context.Set<Review>()
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ReviewDto
            {
                Id = x.Id,
                CourseId = x.CourseId,
                UserId = x.StudentId,
                UserName = x.Student.FullName,
                UserAvatar = null,
                Rating = x.Rating,
                Title = x.Title,
                Content = x.Content,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                HelpfulCount = x.HelpfulMarks.Count(),
                IsHelpful = x.HelpfulMarks.Any(h =>
                    h.UserId == studentId)
            })
            .ToListAsync();
    }

    public async Task<ReviewDto?> GetByIdAsync(
        Guid reviewId,
        Guid? currentUserId)
    {
        return await _context.Set<Review>()
            .AsNoTracking()
            .Where(x => x.Id == reviewId)
            .Select(x => new ReviewDto
            {
                Id = x.Id,
                CourseId = x.CourseId,
                UserId = x.StudentId,
                UserName = x.Student.FullName,
                UserAvatar = null,
                Rating = x.Rating,
                Title = x.Title,
                Content = x.Content,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                HelpfulCount = x.HelpfulMarks.Count(),
                IsHelpful = currentUserId.HasValue &&
                            x.HelpfulMarks.Any(h =>
                                h.UserId == currentUserId.Value)
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ReviewDto?> UpdateAsync(
        Guid reviewId,
        Guid studentId,
        int? rating,
        string? title,
        string? content)
    {
        if (rating.HasValue &&
            (rating.Value < 1 || rating.Value > 5))
        {
            return null;
        }

        if (title is not null &&
            (string.IsNullOrWhiteSpace(title) ||
             title.Length > 200))
        {
            return null;
        }

        if (content is not null &&
            (string.IsNullOrWhiteSpace(content) ||
             content.Length > 2000))
        {
            return null;
        }

        if (!rating.HasValue &&
            title is null &&
            content is null)
        {
            return null;
        }

        var review = await _context.Set<Review>()
            .FirstOrDefaultAsync(x =>
                x.Id == reviewId &&
                x.StudentId == studentId);

        if (review is null)
        {
            return null;
        }

        if (rating.HasValue)
        {
            review.Rating = rating.Value;
        }

        if (title is not null)
        {
            review.Title = title.Trim();
        }

        if (content is not null)
        {
            review.Content = content.Trim();
        }

        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var helpfulCount = await _context.Set<ReviewHelpful>()
            .CountAsync(x => x.ReviewId == review.Id);

        var studentName = await _context.Set<User>()
            .Where(x => x.Id == studentId)
            .Select(x => x.FullName)
            .FirstAsync();

        var isHelpful = await _context.Set<ReviewHelpful>()
            .AnyAsync(x =>
                x.ReviewId == review.Id &&
                x.UserId == studentId);

        return new ReviewDto
        {
            Id = review.Id,
            CourseId = review.CourseId,
            UserId = review.StudentId,
            UserName = studentName,
            UserAvatar = null,
            Rating = review.Rating,
            Title = review.Title,
            Content = review.Content,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            HelpfulCount = helpfulCount,
            IsHelpful = isHelpful
        };
    }

    public async Task<bool> DeleteAsync(
        Guid reviewId,
        Guid studentId)
    {
        var review = await _context.Set<Review>()
            .FirstOrDefaultAsync(x =>
                x.Id == reviewId &&
                x.StudentId == studentId);

        if (review is null)
        {
            return false;
        }

        _context.Set<Review>().Remove(review);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> MarkHelpfulAsync(
        Guid reviewId,
        Guid userId)
    {
        var reviewExists = await _context.Set<Review>()
            .AnyAsync(x => x.Id == reviewId);

        if (!reviewExists)
        {
            return false;
        }

        var alreadyMarked = await _context.Set<ReviewHelpful>()
            .AnyAsync(x =>
                x.ReviewId == reviewId &&
                x.UserId == userId);

        if (!alreadyMarked)
        {
            _context.Set<ReviewHelpful>().Add(
                new ReviewHelpful
                {
                    Id = Guid.NewGuid(),
                    ReviewId = reviewId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });

            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> UnmarkHelpfulAsync(
        Guid reviewId,
        Guid userId)
    {
        var reviewExists = await _context.Set<Review>()
            .AnyAsync(x => x.Id == reviewId);

        if (!reviewExists)
        {
            return false;
        }

        var mark = await _context.Set<ReviewHelpful>()
            .FirstOrDefaultAsync(x =>
                x.ReviewId == reviewId &&
                x.UserId == userId);

        if (mark is not null)
        {
            _context.Set<ReviewHelpful>().Remove(mark);

            await _context.SaveChangesAsync();
        }

        return true;
    }
}