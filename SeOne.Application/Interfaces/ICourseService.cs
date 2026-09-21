using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface ICourseService
{

    // Get courses visible to the authenticated user. User id and role come from claims.
    Task<List<CourseDto>> GetAllAsync(Guid userId, string userRole);

    // Get a single course by id considering the authenticated user's visibility
    Task<CourseDto?> GetByIdAsync(Guid id, Guid userId, string userRole);

    Task<CourseDto> CreateAsync(string title, string description, string level, decimal price, string? duration, string? imageUrl, Guid teacherId);

    Task<bool> SetPublishedAsync(Guid courseId, Guid teacherId, bool isPublished);

    Task<CourseDto?> UpdateAsync(Guid courseId, Guid teacherId, string title, string description, string level, decimal price, string? duration, string? imageUrl);

    Task<List<CourseCatalogDto>> GetPublishedCatalogAsync(string lang);

    Task<bool> DeleteAsync(Guid courseId, Guid teacherId);
}