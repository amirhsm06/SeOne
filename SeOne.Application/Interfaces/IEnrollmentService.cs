using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentDto?> EnrollAsync(Guid studentId, Guid courseId);

    Task<List<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId);

    Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId);

    Task<List<CourseEnrollmentStudentDto>> GetCourseStudentsAsync(Guid courseId, Guid teacherId);
}
