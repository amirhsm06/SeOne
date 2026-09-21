using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface ILessonService
{
    Task<LessonDto?> CreateAsync(Guid teacherId, Guid courseModuleId, string title, string content, int order);

    Task<List<LessonDto>?> GetByModuleAsync(Guid courseModuleId, Guid userId, string userRole);

    Task<LessonDto?> UpdateAsync(Guid courseModuleId, Guid lessonId, Guid teacherId, string title, string content, int order);

    Task<bool> DeleteAsync(Guid courseModuleId, Guid lessonId, Guid teacherId);
}
