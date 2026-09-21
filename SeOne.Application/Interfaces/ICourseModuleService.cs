using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface ICourseModuleService
{
    Task<CourseModuleDto> CreateAsync(Guid teacherId, Guid courseId, string title, string description, int order);

    Task<List<CourseModuleDto>> GetByCourseAsync(Guid courseId, Guid userId, string userRole);

    Task<CourseModuleDto?> UpdateAsync(Guid moduleId, Guid teacherId, string title, string description, int order);

    Task<bool> DeleteAsync(Guid moduleId, Guid teacherId);
}
