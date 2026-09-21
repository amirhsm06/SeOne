using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface ICourseLearningService
{
    Task<CourseLearningDto?> GetCourseLearningAsync(Guid studentId, Guid courseId);
}
