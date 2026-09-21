using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IProgressService
{
    Task<LessonProgressDto?> UpdateLessonProgressAsync(Guid studentId, Guid lessonId, bool isCompleted);

    Task<LessonProgressDto?> GetLessonProgressAsync(Guid studentId, Guid lessonId);

    Task<CourseProgressDto> GetCourseProgressAsync(Guid studentId, Guid courseId);

    Task<ModuleProgressDto> GetModuleProgressAsync(Guid studentId, Guid moduleId);
}
