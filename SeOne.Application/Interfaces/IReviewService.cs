using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IReviewService
{
    Task<List<ReviewDto>?> GetCourseReviewsAsync(
        Guid courseId,
        Guid? currentUserId);

    Task<CourseRatingSummaryDto?> GetCourseRatingSummaryAsync(
        Guid courseId);

    Task<ReviewDto?> CreateAsync(
        Guid studentId,
        Guid courseId,
        int rating,
        string title,
        string content);

    Task<List<ReviewDto>> GetUserReviewsAsync(
        Guid studentId);

    Task<ReviewDto?> GetByIdAsync(
        Guid reviewId,
        Guid? currentUserId);

    Task<ReviewDto?> UpdateAsync(
        Guid reviewId,
        Guid studentId,
        int? rating,
        string? title,
        string? content);

    Task<bool> DeleteAsync(
        Guid reviewId,
        Guid studentId);

    Task<bool> MarkHelpfulAsync(
        Guid reviewId,
        Guid userId);

    Task<bool> UnmarkHelpfulAsync(
        Guid reviewId,
        Guid userId);
}