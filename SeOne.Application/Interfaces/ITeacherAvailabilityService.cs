using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface ITeacherAvailabilityService
{
    Task<List<TeacherAvailabilityDto>> GetByTeacherAsync(Guid teacherId);

    Task<List<TeacherAvailabilityDto>> GetMyAvailabilityAsync(Guid teacherId);

    Task<TeacherAvailabilityDto?> CreateAsync(Guid teacherId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime, bool isAvailable);

    Task<TeacherAvailabilityDto?> UpdateAsync(Guid id, Guid teacherId, TimeSpan startTime, TimeSpan endTime, bool isAvailable);

    Task<bool> DeleteAsync(Guid id, Guid teacherId);

    Task DeleteAllAsync(Guid teacherId);
}
