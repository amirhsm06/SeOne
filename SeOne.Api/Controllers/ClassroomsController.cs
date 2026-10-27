using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/classrooms")]
[Authorize]
public sealed class ClassroomsController : ApiControllerBase
{
    private const long MaximumFileSizeBytes = 52_428_800;
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "scheduled", "ongoing", "completed", "cancelled" };
    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
        { "course_material", "homework", "submission" };
    private static readonly IReadOnlyDictionary<string, string> AllowedMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf", [".png"] = "image/png", [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".gif"] = "image/gif",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain", [".mp4"] = "video/mp4", [".webm"] = "video/webm"
        };

    private readonly SeOneDbContext _db;
    private readonly IClassroomFileStorageService _fileStorage;
    private readonly ILogger<ClassroomsController> _logger;

    public ClassroomsController(SeOneDbContext db, IClassroomFileStorageService fileStorage, ILogger<ClassroomsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var query = _db.ClassroomSessions.AsNoTracking()
            .Include(s => s.Course).Include(s => s.Teacher)
            .Include(s => s.Materials)
            .Include(s => s.Attendance).ThenInclude(a => a.Student)
            .AsQueryable();

        if (User.IsInRole("Admin"))
        {
            // Admins may inspect all sessions.
        }
        else if (User.IsInRole("Teacher"))
        {
            query = query.Where(s => s.TeacherId == userId);
        }
        else if (User.IsInRole("Student"))
        {
            query = query.Where(s => _db.Enrollments.Any(e => e.StudentId == userId && e.CourseId == s.CourseId));
        }
        else
        {
            return Forbid();
        }

        var sessions = await query.OrderBy(s => s.ScheduledAt).ToListAsync(cancellationToken);
        var result = new List<object>(sessions.Count);
        foreach (var session in sessions)
            result.Add(await MapSessionAsync(session, cancellationToken));
        return Ok(result);
    }
    [HttpGet("courses")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetTeacherCourses(
    CancellationToken cancellationToken)
    {
        var teacherId = RequireUserId();

        var courses = await _db.Set<Course>()
            .AsNoTracking()
            .Where(course =>
                _db.Set<CourseInstanceTeacher>()
                    .Any(assignment =>
                        assignment.TeacherId == teacherId &&
                        assignment.CourseInstance.CourseId == course.Id))
            .OrderBy(course => course.Title)
            .Select(course => new
            {
                id = course.Id,
                title = course.Title
            })
            .ToListAsync(cancellationToken);

        return Ok(courses);
    }

    [HttpPost("sessions")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> CreateSession([FromBody] CreateClassroomSessionRequest request, CancellationToken cancellationToken)
    {
        var teacherId = RequireUserId();
        if (request.CourseId == Guid.Empty || request.ScheduledAt == default ||
            request.ScheduledAt.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-5) ||
            request.Duration is < 1 or > 480 || !IsValidProvider(request.Provider ?? "adobe_connect") ||
            !IsValidOptionalHttpsUrl(request.MeetingUrl) ||
            (request.Notes?.Length ?? 0) > 4000)
        {
            return BadRequest(new { message = "The classroom session details are invalid." });
        }

        var courseExists = await _db.Set<Course>().AnyAsync(c => c.Id == request.CourseId, cancellationToken);
        if (!courseExists) return NotFound(new { message = "Course not found." });

        var isAssigned = await _db.Set<CourseInstanceTeacher>().AnyAsync(t =>
            t.TeacherId == teacherId && t.CourseInstance.CourseId == request.CourseId, cancellationToken);
        if (!isAssigned) return Forbid();

        var now = DateTime.UtcNow;
        var session = new ClassroomSession
        {
            Id = Guid.NewGuid(), CourseId = request.CourseId, TeacherId = teacherId,
            ScheduledAt = request.ScheduledAt.ToUniversalTime(), Duration = request.Duration,
            Status = "scheduled", Provider = string.IsNullOrWhiteSpace(request.Provider) ? "adobe_connect" : request.Provider.Trim(),
            MeetingUrl = request.MeetingUrl?.Trim(), Notes = request.Notes?.Trim(),
            CreatedAt = now, UpdatedAt = now
        };

        _db.ClassroomSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(session).Reference(s => s.Course).LoadAsync(cancellationToken);
        await _db.Entry(session).Reference(s => s.Teacher).LoadAsync(cancellationToken);
        return Ok(await MapSessionAsync(session, cancellationToken));
    }

    [HttpPatch("sessions/{sessionId:guid}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> UpdateSession(Guid sessionId, [FromBody] UpdateClassroomSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _db.ClassroomSessions
            .Include(s => s.Course).Include(s => s.Teacher).Include(s => s.Materials)
            .Include(s => s.Attendance).ThenInclude(a => a.Student)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null) return NotFound();
        if (!User.IsInRole("Admin") && session.TeacherId != RequireUserId()) return Forbid();

        if (request.Status is not null)
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (!ValidStatuses.Contains(status)) return BadRequest(new { message = "Unsupported classroom status." });
            session.Status = status;
        }
        if (request.MeetingUrl is not null)
        {
            if (!IsValidOptionalHttpsUrl(request.MeetingUrl)) return BadRequest(new { message = "Meeting URL must use HTTPS." });
            session.MeetingUrl = request.MeetingUrl.Trim();
        }
        if (request.RecordingUrl is not null)
        {
            if (!IsValidOptionalHttpsUrl(request.RecordingUrl)) return BadRequest(new { message = "Recording URL must use HTTPS." });
            session.RecordingUrl = request.RecordingUrl.Trim();
        }
        if (request.Notes is not null)
        {
            if (request.Notes.Length > 4000) return BadRequest(new { message = "Notes cannot exceed 4000 characters." });
            session.Notes = request.Notes.Trim();
        }
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapSessionAsync(session, cancellationToken));
    }

    [HttpPost("sessions/{sessionId:guid}/join")]
    public async Task<IActionResult> JoinSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _db.ClassroomSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null) return NotFound();
        var userId = RequireUserId();
        if (!await CanAccessSessionAsync(session, userId, cancellationToken)) return Forbid();

        if (!Uri.TryCreate(session.MeetingUrl, UriKind.Absolute, out var meetingUri) || meetingUri.Scheme != Uri.UriSchemeHttps)
            return Conflict(new { message = "This session does not have a valid classroom URL yet." });

        if (User.IsInRole("Student"))
        {
            var attendance = await _db.ClassroomAttendances.FirstOrDefaultAsync(
                a => a.SessionId == sessionId && a.StudentId == userId, cancellationToken);
            if (attendance is null)
            {
                attendance = new ClassroomAttendance
                {
                    Id = Guid.NewGuid(), SessionId = sessionId, StudentId = userId,
                    Status = "present", JoinedAt = DateTime.UtcNow
                };
                _db.ClassroomAttendances.Add(attendance);
            }
            else
            {
                // Preserve an existing attendance status on repeat joins.
                attendance.JoinedAt = attendance.JoinedAt == default ? DateTime.UtcNow : attendance.JoinedAt;
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { meetingUrl = session.MeetingUrl });
    }

    [HttpPost("sessions/{sessionId:guid}/files/upload-grant")]
    public async Task<IActionResult> CreateFileUploadGrant(Guid sessionId, [FromBody] ClassroomUploadGrantRequest request, CancellationToken cancellationToken)
    {
        var session = await _db.ClassroomSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null) return NotFound();
        var category = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidCategories.Contains(category)) return BadRequest(new { message = "Unsupported material category." });
        if (!await CanUploadCategoryAsync(session, RequireUserId(), category, cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentType) ||
            request.Size <= 0 || request.Size > MaximumFileSizeBytes)
            return BadRequest(new { message = "File details are invalid or exceed 50 MiB." });

        var grant = await _fileStorage.CreateUploadGrantAsync(sessionId, request.FileName, request.ContentType, request.Size, cancellationToken);
        if (grant is null)
            return StatusCode(503, new { message = "Classroom file storage is unavailable or the file type is not supported." });
        return Ok(grant);
    }

    [HttpPost("sessions/{sessionId:guid}/files")]
    public async Task<IActionResult> SaveUploadedFile(Guid sessionId, [FromBody] SaveClassroomFileRequest request, CancellationToken cancellationToken)
    {
        var session = await _db.ClassroomSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null) return NotFound();
        var userId = RequireUserId();
        var category = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidCategories.Contains(category)) return BadRequest(new { message = "Unsupported material category." });
        if (!await CanUploadCategoryAsync(session, userId, category, cancellationToken)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Trim().Length > 255 ||
            request.Size <= 0 || request.Size > MaximumFileSizeBytes ||
            string.IsNullOrWhiteSpace(request.ContentType))
            return BadRequest(new { message = "File metadata is invalid." });

        if (!_fileStorage.IsValidObjectPath(sessionId, request.Path))
            return BadRequest(new { message = "The uploaded object path is invalid." });

        var extension = Path.GetExtension(request.Path).ToLowerInvariant();
        var contentType = request.ContentType.Split(';', 2)[0].Trim();
        if (!AllowedMimeTypes.TryGetValue(extension, out var expectedMimeType) ||
            !string.Equals(contentType, expectedMimeType, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "The content type does not match the stored file path." });

        // A signed download URL is issued only when Supabase recognizes the stored object.
        var downloadUrl = await _fileStorage.CreateSignedDownloadUrlAsync(sessionId, request.Path, 300, cancellationToken);
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return BadRequest(new { message = "The uploaded file could not be verified in private storage." });

        var material = new ClassroomMaterial
        {
            Id = Guid.NewGuid(), SessionId = sessionId, Name = request.FileName.Trim(), Category = category,
            ContentType = contentType, Size = request.Size, ObjectPath = request.Path,
            UploadedById = userId, CreatedAt = DateTime.UtcNow
        };
        _db.ClassroomMaterials.Add(material);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Do not delete an object that an existing material record already references.
            // If the reference check fails, preserve the object rather than risk data loss.
            try
            {
                var objectIsReferenced = await _db.ClassroomMaterials
                    .AsNoTracking()
                    .AnyAsync(m => m.ObjectPath == request.Path, CancellationToken.None);

                if (objectIsReferenced)
                {
                    _logger.LogWarning(
                        "Skipped classroom object cleanup because the object path is already referenced by material metadata.");
                }
                else
                {
                    await DeleteObjectQuietlyAsync(sessionId, request.Path);
                }
            }
            catch (Exception cleanupCheckException)
            {
                _logger.LogWarning(
                    cleanupCheckException,
                    "Could not verify classroom object references after a metadata save failure; leaving the object in private storage.");
            }

            throw;
        }

        return Ok(new { id = material.Id, name = material.Name, category = material.Category,
            contentType = material.ContentType, size = material.Size, createdAt = material.CreatedAt, downloadUrl });
    }
    [HttpDelete("sessions/{sessionId:guid}/files/{materialId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClassroomMaterial(
    Guid sessionId,
    Guid materialId,
    CancellationToken cancellationToken)
    {
        var material = await _db.ClassroomMaterials
            .FirstOrDefaultAsync(
                m => m.Id == materialId && m.SessionId == sessionId,
                cancellationToken);

        if (material is null)
            return NotFound();

        if (!_fileStorage.IsValidObjectPath(sessionId, material.ObjectPath))
            return Conflict(new
            {
                message = "The stored classroom file path is invalid; the object was not deleted."
            });

        var objectDeleted = await _fileStorage.DeleteObjectAsync(
            sessionId,
            material.ObjectPath,
            cancellationToken);

        if (!objectDeleted)
        {
            return StatusCode(503, new
            {
                message = "The stored file could not be deleted; material metadata was retained."
            });
        }

        _db.ClassroomMaterials.Remove(material);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Classroom file object was deleted but metadata cleanup failed. " +
                "SessionId={SessionId}, MaterialId={MaterialId}",
                sessionId,
                materialId);

            return StatusCode(500, new
            {
                message = "The file object was deleted, but metadata cleanup failed. Administrator intervention is required."
            });
        }

        return NoContent();
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmptySession(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _db.ClassroomSessions
            .Include(s => s.Materials)
            .Include(s => s.Attendance)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return NotFound();

        if (session.Materials.Count > 0 || session.Attendance.Count > 0)
            return Conflict(new { message = "This classroom session cannot be deleted while it has materials or attendance records." });

        _db.ClassroomSessions.Remove(session);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
    private async Task<bool> CanAccessSessionAsync(ClassroomSession session, Guid userId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin")) return true;
        if (User.IsInRole("Teacher")) return session.TeacherId == userId;
        if (User.IsInRole("Student"))
            return await _db.Enrollments.AnyAsync(e => e.StudentId == userId && e.CourseId == session.CourseId, cancellationToken);
        return false;
    }

    private async Task<bool> CanUploadCategoryAsync(ClassroomSession session, Guid userId, string category, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin")) return true;
        if (User.IsInRole("Teacher"))
            return session.TeacherId == userId && category != "submission";
        if (User.IsInRole("Student"))
            return category == "submission" && await _db.Enrollments.AnyAsync(e => e.StudentId == userId && e.CourseId == session.CourseId, cancellationToken);
        return false;
    }

    private async Task<object> MapSessionAsync(ClassroomSession session, CancellationToken cancellationToken)
    {
        var currentUserId = RequireUserId();
        var visibleMaterials = User.IsInRole("Student")
            ? session.Materials
                .Where(m => !string.Equals(m.Category, "submission", StringComparison.OrdinalIgnoreCase)
                    || m.UploadedById == currentUserId)
                .ToList()
            : session.Materials.ToList();

        var materials = new List<object>(visibleMaterials.Count);
        foreach (var material in visibleMaterials.OrderBy(m => m.CreatedAt))
        {
            var downloadUrl = await _fileStorage.CreateSignedDownloadUrlAsync(session.Id, material.ObjectPath, 300, cancellationToken);
            materials.Add(new { id = material.Id, name = material.Name, category = material.Category,
                contentType = material.ContentType, size = material.Size, createdAt = material.CreatedAt, downloadUrl });
        }

        var visibleAttendance = User.IsInRole("Student")
            ? session.Attendance.Where(a => a.StudentId == RequireUserId())
            : session.Attendance;
        var attendance = visibleAttendance.OrderBy(a => a.JoinedAt).Select(a => new
        {
            studentId = a.StudentId, studentName = a.Student?.FullName ?? string.Empty,
            status = a.Status, joinedAt = a.JoinedAt, leftAt = a.LeftAt
        }).ToList();

        return new
        {
            id = session.Id, courseId = session.CourseId, courseTitle = session.Course?.Title ?? string.Empty,
            teacherId = session.TeacherId, teacherName = session.Teacher?.FullName ?? string.Empty,
            scheduledAt = session.ScheduledAt, duration = session.Duration, status = session.Status,
            provider = session.Provider, meetingUrl = session.MeetingUrl, recordingUrl = session.RecordingUrl,
            materials, notes = session.Notes, attendance
        };
    }

    private async Task DeleteObjectQuietlyAsync(Guid sessionId, string objectPath)
    {
        try
        {
            if (!await _fileStorage.DeleteObjectAsync(sessionId, objectPath, CancellationToken.None))
                _logger.LogWarning("Could not clean up classroom object {ObjectPath} after a database failure.", objectPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error cleaning up classroom object {ObjectPath} after a database failure.", objectPath);
        }
    }

    private static bool IsValidProvider(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && provider.Length <= 80 &&
        provider.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static bool IsValidOptionalHttpsUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo));
}

public sealed class CreateClassroomSessionRequest
{
    public Guid CourseId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int Duration { get; set; }
    public string? Provider { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateClassroomSessionRequest
{
    public string? Status { get; set; }
    public string? MeetingUrl { get; set; }
    public string? RecordingUrl { get; set; }
    public string? Notes { get; set; }
}

public sealed class ClassroomUploadGrantRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Category { get; set; } = "course_material";
}

public sealed class SaveClassroomFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Category { get; set; } = "course_material";
}
