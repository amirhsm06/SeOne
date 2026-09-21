using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface ITeacherService
{
    Task<List<TeacherDto>> GetAllTeachersAsync();

    Task<TeacherDto?> GetTeacherByIdAsync(Guid teacherId);

    Task<TeacherDto?> GetMyProfileAsync(Guid teacherId);

    Task<TeacherDto?> UpdateMyProfileAsync(Guid teacherId, string? avatar, string? teachingLanguage, string? subject, string? level, string? bio);
}
