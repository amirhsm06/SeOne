using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IPracticeService
{
    Task<List<PracticeContentDto>> GetTeacherPracticeContentAsync(
        Guid teacherId,
        Guid? courseId = null,
        Guid? moduleId = null,
        Guid? lessonId = null,
        string? language = null,
        string? level = null,
        string? category = null);

    Task<List<PracticeStudentContentDto>> GetStudentPracticeContentAsync(
        Guid studentId,
        Guid? courseId = null,
        Guid? moduleId = null,
        Guid? lessonId = null,
        string? language = null,
        string? level = null,
        string? category = null);

    Task<PracticeContentDto?> GetTeacherPracticeAsync(
        Guid teacherId,
        Guid practiceId);

    Task<PracticeStudentContentDto?> GetStudentPracticeAsync(
        Guid studentId,
        Guid practiceId);

    Task<PracticeContentDto?> CreatePracticeAsync(
        Guid teacherId,
        CreatePracticeRequest request);

    Task<PracticeContentDto?> UpdatePracticeAsync(
        Guid teacherId,
        Guid practiceId,
        UpdatePracticeRequest request);

    Task<bool> DeletePracticeAsync(
        Guid teacherId,
        Guid practiceId);

    Task<StartPracticeAttemptDto?> StartAttemptAsync(
        Guid studentId,
        Guid practiceId);

    Task<PracticeAttemptDto?> SubmitAttemptAsync(
        Guid studentId,
        Guid attemptId,
        SubmitPracticeAttemptRequest request);

    Task<List<PracticeAttemptDto>> GetPracticeAttemptsAsync(
        Guid studentId,
        Guid? practiceId = null);

    Task<PracticeAttemptDto?> GetAttemptAsync(
        Guid studentId,
        Guid attemptId);

    Task<List<PracticeHistoryDto>> GetHistoryAsync(
        Guid studentId);

    Task<StreakInfoDto> GetStreakAsync(
        Guid studentId);

    Task<List<PracticeLeaderboardEntryDto>?> GetLeaderboardAsync(
        Guid studentId,
        Guid practiceId);
}
