using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IAssignmentService
{
    Task<List<AssignmentDto>> GetTeacherAssignmentsAsync(
        Guid teacherId);

    Task<AssignmentDto?> CreateAssignmentAsync(
        Guid teacherId,
        CreateAssignmentRequest request);

    Task<AssignmentDto?> GetTeacherAssignmentAsync(
        Guid teacherId,
        Guid assignmentId);

    Task<AssignmentDto?> UpdateAssignmentAsync(
        Guid teacherId,
        Guid assignmentId,
        UpdateAssignmentRequest request);

    Task<bool> DeleteAssignmentAsync(
        Guid teacherId,
        Guid assignmentId);

    Task<List<SubmissionDto>?> GetAssignmentSubmissionsAsync(
        Guid teacherId,
        Guid assignmentId);

    Task<SubmissionDto?> GradeSubmissionAsync(
        Guid teacherId,
        Guid submissionId,
        GradeSubmissionRequest request);

    Task<List<AssignmentDto>> GetStudentAssignmentsAsync(
        Guid studentId);

    Task<StudentAssignmentDto?> GetStudentAssignmentAsync(
        Guid studentId,
        Guid assignmentId);

    Task<SubmissionDto?> SubmitAssignmentAsync(
        Guid studentId,
        Guid assignmentId,
        CreateSubmissionRequest request);

    Task<List<SubmissionDto>> GetStudentSubmissionsAsync(
        Guid studentId);

    Task<SubmissionDto?> GetStudentSubmissionAsync(
        Guid studentId,
        Guid submissionId);

    Task<SubmissionDto?> UpdateStudentSubmissionAsync(
        Guid studentId,
        Guid submissionId,
        UpdateSubmissionRequest request);
}