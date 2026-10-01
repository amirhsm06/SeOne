using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class AssignmentService : IAssignmentService
{
    private readonly SeOneDbContext _context;

    public AssignmentService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<AssignmentDto>> GetTeacherAssignmentsAsync(
        Guid teacherId)
    {
        return await _context.Set<Assignment>()
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapAssignment)
            .ToListAsync();
    }

    public async Task<AssignmentDto?> CreateAssignmentAsync(
        Guid teacherId,
        CreateAssignmentRequest request)
    {
        if (!IsValidText(request.Title, 200) ||
            !IsValidText(request.Description, 5000) ||
            request.MaxPoints <= 0 ||
            !AreAttachmentsValid(request.Attachments))
        {
            return null;
        }

        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(x =>
                x.Id == request.CourseId &&
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId &&
                        t.CourseInstance.CourseId == x.Id));

        if (course is null)
        {
            return null;
        }

        if (request.ModuleId.HasValue)
        {
            var moduleBelongsToCourse =
                await _context.Set<CourseModule>()
                    .AnyAsync(x =>
                        x.Id == request.ModuleId.Value &&
                        x.CourseId == request.CourseId);

            if (!moduleBelongsToCourse)
            {
                return null;
            }
        }

        var now = DateTime.UtcNow;

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            CourseModuleId = request.ModuleId,
            TeacherId = teacherId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Instructions = string.IsNullOrWhiteSpace(
                request.Instructions)
                ? null
                : request.Instructions.Trim(),
            DueDate = request.DueDate.ToUniversalTime(),
            MaxPoints = request.MaxPoints,
            Attachments = request.Attachments?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList()
                ?? new List<string>(),
            CreatedAt = now,
            UpdatedAt = now,
            IsPublished = request.IsPublished
        };

        _context.Set<Assignment>().Add(assignment);

        await _context.SaveChangesAsync();

        return await GetTeacherAssignmentAsync(
            teacherId,
            assignment.Id);
    }

    public async Task<AssignmentDto?> GetTeacherAssignmentAsync(
        Guid teacherId,
        Guid assignmentId)
    {
        return await _context.Set<Assignment>()
            .AsNoTracking()
            .Where(x =>
                x.Id == assignmentId &&
                x.TeacherId == teacherId)
            .Select(MapAssignment)
            .FirstOrDefaultAsync();
    }

    public async Task<AssignmentDto?> UpdateAssignmentAsync(
        Guid teacherId,
        Guid assignmentId,
        UpdateAssignmentRequest request)
    {
        if (request.Title is not null &&
            !IsValidText(request.Title, 200))
        {
            return null;
        }

        if (request.Description is not null &&
            !IsValidText(request.Description, 5000))
        {
            return null;
        }

        if (request.Instructions is not null &&
            request.Instructions.Length > 5000)
        {
            return null;
        }

        if (request.MaxPoints.HasValue &&
            request.MaxPoints.Value <= 0)
        {
            return null;
        }

        if (!AreAttachmentsValid(request.Attachments))
        {
            return null;
        }

        var assignment = await _context.Set<Assignment>()
            .FirstOrDefaultAsync(x =>
                x.Id == assignmentId &&
                x.TeacherId == teacherId);

        if (assignment is null)
        {
            return null;
        }

        if (request.Title is not null)
        {
            assignment.Title = request.Title.Trim();
        }

        if (request.Description is not null)
        {
            assignment.Description =
                request.Description.Trim();
        }

        if (request.Instructions is not null)
        {
            assignment.Instructions =
                string.IsNullOrWhiteSpace(request.Instructions)
                    ? null
                    : request.Instructions.Trim();
        }

        if (request.DueDate.HasValue)
        {
            assignment.DueDate =
                request.DueDate.Value.ToUniversalTime();
        }

        if (request.MaxPoints.HasValue)
        {
            assignment.MaxPoints =
                request.MaxPoints.Value;
        }

        if (request.Attachments is not null)
        {
            assignment.Attachments =
                request.Attachments
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
        }

        if (request.IsPublished.HasValue)
        {
            assignment.IsPublished =
                request.IsPublished.Value;
        }

        assignment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetTeacherAssignmentAsync(
            teacherId,
            assignmentId);
    }

    public async Task<bool> DeleteAssignmentAsync(
        Guid teacherId,
        Guid assignmentId)
    {
        var assignment = await _context.Set<Assignment>()
            .FirstOrDefaultAsync(x =>
                x.Id == assignmentId &&
                x.TeacherId == teacherId);

        if (assignment is null)
        {
            return false;
        }

        _context.Set<Assignment>()
            .Remove(assignment);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<SubmissionDto>?> GetAssignmentSubmissionsAsync(
        Guid teacherId,
        Guid assignmentId)
    {
        var ownsAssignment =
            await _context.Set<Assignment>()
                .AnyAsync(x =>
                    x.Id == assignmentId &&
                    x.TeacherId == teacherId);

        if (!ownsAssignment)
        {
            return null;
        }

        return await _context.Set<AssignmentSubmission>()
            .AsNoTracking()
            .Where(x => x.AssignmentId == assignmentId)
            .OrderByDescending(x => x.SubmittedAt)
            .Select(MapSubmission)
            .ToListAsync();
    }

    public async Task<SubmissionDto?> GradeSubmissionAsync(
        Guid teacherId,
        Guid submissionId,
        GradeSubmissionRequest request)
    {
        var submission =
            await _context.Set<AssignmentSubmission>()
                .Include(x => x.Assignment)
                .FirstOrDefaultAsync(x =>
                    x.Id == submissionId &&
                    x.Assignment.TeacherId == teacherId);

        if (submission is null)
        {
            return null;
        }

        if (request.Grade < 0 ||
            request.Grade > submission.Assignment.MaxPoints)
        {
            return null;
        }

        submission.Grade = request.Grade;

        submission.Feedback =
            string.IsNullOrWhiteSpace(request.Feedback)
                ? null
                : request.Feedback.Trim();

        submission.GradedAt = DateTime.UtcNow;

        submission.GradedById = teacherId;

        submission.Status =
            AssignmentSubmissionStatus.Graded;

        await _context.SaveChangesAsync();

        return await GetSubmissionDtoAsync(
            submission.Id);
    }

    public async Task<List<AssignmentDto>>
        GetStudentAssignmentsAsync(Guid studentId)
    {
        return await _context.Set<Assignment>()
            .AsNoTracking()
            .Where(x =>
                x.IsPublished &&
                x.Course.IsPublished &&
                _context.Set<Enrollment>()
                    .Any(e =>
                        e.StudentId == studentId &&
                        e.CourseId == x.CourseId))
            .OrderBy(x => x.DueDate)
            .Select(MapAssignment)
            .ToListAsync();
    }

    public async Task<StudentAssignmentDto?>
        GetStudentAssignmentAsync(
            Guid studentId,
            Guid assignmentId)
    {
        var assignment =
            await _context.Set<Assignment>()
                .AsNoTracking()
                .Where(x =>
                    x.Id == assignmentId &&
                    x.IsPublished &&
                    x.Course.IsPublished &&
                    _context.Set<Enrollment>()
                        .Any(e =>
                            e.StudentId == studentId &&
                            e.CourseId == x.CourseId))
                .Select(MapAssignment)
                .FirstOrDefaultAsync();

        if (assignment is null)
        {
            return null;
        }

        var submission =
            await _context.Set<AssignmentSubmission>()
                .AsNoTracking()
                .Where(x =>
                    x.AssignmentId == assignmentId &&
                    x.StudentId == studentId)
                .Select(MapSubmission)
                .FirstOrDefaultAsync();

        return new StudentAssignmentDto
        {
            Assignment = assignment,
            Submission = submission
        };
    }

    public async Task<SubmissionDto?> SubmitAssignmentAsync(
        Guid studentId,
        Guid assignmentId,
        CreateSubmissionRequest request)
    {
        if (!IsValidText(request.Content, 20000) ||
            !AreAttachmentsValid(request.Attachments))
        {
            return null;
        }

        var assignment =
            await _context.Set<Assignment>()
                .FirstOrDefaultAsync(x =>
                    x.Id == assignmentId &&
                    x.IsPublished &&
                    x.Course.IsPublished);

        if (assignment is null)
        {
            return null;
        }

        var enrolled =
            await _context.Set<Enrollment>()
                .AnyAsync(x =>
                    x.StudentId == studentId &&
                    x.CourseId == assignment.CourseId);

        if (!enrolled)
        {
            return null;
        }

        var existing =
            await _context.Set<AssignmentSubmission>()
                .FirstOrDefaultAsync(x =>
                    x.AssignmentId == assignmentId &&
                    x.StudentId == studentId);

        if (existing is not null)
        {
            return null;
        }

        var submission = new AssignmentSubmission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentId = studentId,
            Content = request.Content.Trim(),
            Attachments = request.Attachments?
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList()
                ?? new List<string>(),
            SubmittedAt = DateTime.UtcNow,
            Status = AssignmentSubmissionStatus.Submitted
        };

        _context.Set<AssignmentSubmission>()
            .Add(submission);

        await _context.SaveChangesAsync();

        return await GetSubmissionDtoAsync(
            submission.Id);
    }

    public async Task<List<SubmissionDto>>
        GetStudentSubmissionsAsync(
            Guid studentId)
    {
        return await _context.Set<AssignmentSubmission>()
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.SubmittedAt)
            .Select(MapSubmission)
            .ToListAsync();
    }

    public async Task<SubmissionDto?>
        GetStudentSubmissionAsync(
            Guid studentId,
            Guid submissionId)
    {
        return await _context.Set<AssignmentSubmission>()
            .AsNoTracking()
            .Where(x =>
                x.Id == submissionId &&
                x.StudentId == studentId)
            .Select(MapSubmission)
            .FirstOrDefaultAsync();
    }

    public async Task<SubmissionDto?>
        UpdateStudentSubmissionAsync(
            Guid studentId,
            Guid submissionId,
            UpdateSubmissionRequest request)
    {
        var submission =
            await _context.Set<AssignmentSubmission>()
                .FirstOrDefaultAsync(x =>
                    x.Id == submissionId &&
                    x.StudentId == studentId);

        if (submission is null)
        {
            return null;
        }

        if (submission.Status !=
            AssignmentSubmissionStatus.Draft)
        {
            return null;
        }

        if (request.Content is not null)
        {
            if (!IsValidText(
                    request.Content,
                    20000))
            {
                return null;
            }

            submission.Content =
                request.Content.Trim();
        }

        if (request.Attachments is not null)
        {
            if (!AreAttachmentsValid(
                    request.Attachments))
            {
                return null;
            }

            submission.Attachments =
                request.Attachments
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
        }

        submission.SubmittedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetSubmissionDtoAsync(
            submission.Id);
    }

    private async Task<SubmissionDto?>
        GetSubmissionDtoAsync(
            Guid submissionId)
    {
        return await _context.Set<AssignmentSubmission>()
            .AsNoTracking()
            .Where(x => x.Id == submissionId)
            .Select(MapSubmission)
            .FirstOrDefaultAsync();
    }

    private static bool IsValidText(
        string? value,
        int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Length <= maxLength;
    }

    private static bool AreAttachmentsValid(
        List<string>? attachments)
    {
        if (attachments is null)
        {
            return true;
        }

        if (attachments.Count > 20)
        {
            return false;
        }

        return attachments.All(x =>
            !string.IsNullOrWhiteSpace(x) &&
            x.Length <= 1000);
    }

    private static System.Linq.Expressions.Expression<
        Func<Assignment, AssignmentDto>> MapAssignment =>
        x => new AssignmentDto
        {
            Id = x.Id,
            CourseId = x.CourseId,
            CourseTitle = x.Course.Title,
            ModuleId = x.CourseModuleId,
            ModuleTitle = x.CourseModule != null
                ? x.CourseModule.Title
                : null,
            TeacherId = x.TeacherId,
            TeacherName = x.Teacher.FullName,
            Title = x.Title,
            Description = x.Description,
            Instructions = x.Instructions,
            DueDate = x.DueDate,
            MaxPoints = x.MaxPoints,
            Attachments = x.Attachments,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            IsPublished = x.IsPublished
        };

    private static System.Linq.Expressions.Expression<
        Func<AssignmentSubmission, SubmissionDto>> MapSubmission =>
        x => new SubmissionDto
        {
            Id = x.Id,
            AssignmentId = x.AssignmentId,
            StudentId = x.StudentId,
            StudentName = x.Student.FullName,
            StudentAvatar = null,
            Content = x.Content,
            Attachments = x.Attachments,
            SubmittedAt = x.SubmittedAt,
            Grade = x.Grade,
            Feedback = x.Feedback,
            GradedAt = x.GradedAt,
            GradedBy = x.GradedById,
            GradedByName = x.GradedBy != null
                ? x.GradedBy.FullName
                : null,
            Status = x.Status == AssignmentSubmissionStatus.Draft
                ? "draft"
                : x.Status == AssignmentSubmissionStatus.Submitted
                    ? "submitted"
                    : x.Status == AssignmentSubmissionStatus.Graded
                        ? "graded"
                        : "returned"
        };
}