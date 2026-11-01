using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/classes")]
public sealed class ClassesController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly IConfiguration _configuration;

    public ClassesController(SeOneDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetClasses(
        [FromQuery] Guid? teacherId,
        [FromQuery] string? type,
        [FromQuery] bool? availableOnly,
        [FromQuery] string? level,
        [FromQuery] string? subject,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "Page must be positive and pageSize must be between 1 and 100." });

        if (!string.IsNullOrWhiteSpace(type) &&
            !string.Equals(type, "public", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "private", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Type must be public or private." });

        var query = _db.Set<CourseClass>()
            .AsNoTracking()
            .Where(classItem => classItem.Status == "scheduled");

        if (teacherId.HasValue)
            query = query.Where(classItem => classItem.TeacherId == teacherId.Value);
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(classItem => classItem.Type == type.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(classItem => classItem.Level == level.Trim());
        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(classItem => classItem.Subject == subject.Trim());
        if (availableOnly == true)
            query = query.Where(classItem => _db.Set<ClassEnrollment>().Count(enrollment => enrollment.ClassId == classItem.Id && enrollment.Status != "cancelled") < classItem.MaxCapacity);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(classItem => classItem.ScheduledDate)
            .ThenBy(classItem => classItem.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(classItem => new
            {
                Class = classItem,
                CurrentEnrollment = _db.Set<ClassEnrollment>().Count(enrollment => enrollment.ClassId == classItem.Id && enrollment.Status != "cancelled"),
                TeacherAvatar = _db.Set<TeacherProfile>().Where(profile => profile.TeacherId == classItem.TeacherId).Select(profile => profile.Avatar).FirstOrDefault(),
                TeacherRating = _db.Set<TeacherProfile>().Where(profile => profile.TeacherId == classItem.TeacherId).Select(profile => profile.Rating).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new CourseClassDto
        {
            Id = row.Class.Id,
            Title = row.Class.Title,
            Description = row.Class.Description,
            Level = row.Class.Level,
            Subject = row.Class.Subject,
            Type = row.Class.Type,
            MaxCapacity = row.Class.MaxCapacity,
            CurrentEnrollment = row.CurrentEnrollment,
            RemainingCapacity = Math.Max(0, row.Class.MaxCapacity - row.CurrentEnrollment),
            BasePrice = row.Class.BasePrice,
            FinalPrice = GetFinalPrice(row.Class, row.CurrentEnrollment),
            ScheduledDate = DateTime.SpecifyKind(row.Class.ScheduledDate.Date + row.Class.StartTime, DateTimeKind.Utc),
            StartTime = row.Class.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            EndTime = row.Class.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            TimeZone = row.Class.TimeZone,
            Status = row.Class.Status,
            Teacher = new ClassTeacherDto
            {
                Id = row.Class.TeacherId,
                Name = row.Class.Teacher.FullName,
                Avatar = row.TeacherAvatar ?? row.Class.Teacher.AvatarUrl,
                Rating = row.TeacherRating
            },
            Course = new ClassCourseDto
            {
                Id = row.Class.CourseId,
                Title = row.Class.Course.Title,
                ImageUrl = row.Class.Course.ImageUrl
            }
        }).ToList();

        return Ok(new PagedResult<CourseClassDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{classId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetClass(Guid classId, CancellationToken cancellationToken)
    {
        return await GetClassByIdAsync(classId, cancellationToken);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> CreateClass([FromBody] CreateCourseClassRequest request, CancellationToken cancellationToken)
    {
        var course = await _db.Set<Course>().FirstOrDefaultAsync(item => item.Id == request.CourseId && item.IsPublished, cancellationToken);
        if (course is null)
            return NotFound(new { message = "Published course not found." });

        var teacherId = RequireUserId();
        if (User.IsInRole("Admin"))
        {
            if (!request.TeacherId.HasValue || !await _db.Users.AnyAsync(user => user.Id == request.TeacherId && user.Role == UserRole.Teacher, cancellationToken))
                return BadRequest(new { message = "A valid teacherId is required." });
            teacherId = request.TeacherId.Value;
        }
        else if (!await _db.Set<CourseInstanceTeacher>().AnyAsync(assignment => assignment.TeacherId == teacherId && assignment.CourseInstance.CourseId == course.Id, cancellationToken))
        {
            return Forbid();
        }

        if (!TryParseTimes(request.StartTime, request.EndTime, out var startTime, out var endTime) ||
            request.ScheduledDate == default || request.ScheduledDate.Date < DateTime.UtcNow.Date ||
            request.MaxCapacity is < 1 or > 500 || request.BasePrice is < 0 ||
            (!string.Equals(request.Type, "public", StringComparison.OrdinalIgnoreCase) && !string.Equals(request.Type, "private", StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(request.TimeZone) || request.TimeZone.Length > 100 ||
            !IsValidOptionalHttpsUrl(request.MeetingUrl))
            return BadRequest(new { message = "Class details are invalid." });

        if (await HasScheduleConflictAsync(teacherId, request.ScheduledDate.Date, startTime, endTime, null, cancellationToken))
            return Conflict(new { message = "The teacher already has a scheduled class during this time." });

        var classItem = new CourseClass
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            TeacherId = teacherId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? course.Title : request.Title.Trim(),
            Description = request.Description?.Trim() ?? course.Description,
            Level = string.IsNullOrWhiteSpace(request.Level) ? course.Level : request.Level.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? course.Category : request.Subject.Trim(),
            Type = request.Type.ToLowerInvariant(),
            MaxCapacity = request.MaxCapacity,
            BasePrice = request.BasePrice ?? course.Price,
            ScheduledDate = request.ScheduledDate.Date,
            StartTime = startTime,
            EndTime = endTime,
            TimeZone = request.TimeZone.Trim(),
            MeetingUrl = request.MeetingUrl?.Trim(),
            Status = "scheduled",
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(classItem);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetClass), new { classId = classItem.Id }, new { id = classItem.Id });
    }

    [HttpPost("{classId:guid}/enroll")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Enroll(Guid classId, [FromBody] EnrollInClassRequest? request, CancellationToken cancellationToken)
    {
        var studentId = RequireUserId();
        var classItem = await _db.Set<CourseClass>().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (classItem is null || classItem.Status != "scheduled")
            return NotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var existing = await _db.Set<ClassEnrollment>().FirstOrDefaultAsync(enrollment => enrollment.ClassId == classId && enrollment.StudentId == studentId, cancellationToken);
        if (existing is not null && existing.Status != "cancelled")
            return Conflict(new { message = "You are already enrolled in this class." });

        var currentEnrollment = await _db.Set<ClassEnrollment>().CountAsync(enrollment => enrollment.ClassId == classId && enrollment.Status != "cancelled", cancellationToken);
        if (currentEnrollment >= classItem.MaxCapacity)
            return Conflict(new { message = "This class is full." });

        ClassInvite? invite = null;
        if (!string.IsNullOrWhiteSpace(request?.InviteCode))
        {
            invite = await _db.Set<ClassInvite>().Include(item => item.CreatedBy)
                .FirstOrDefaultAsync(item => item.ClassId == classId && item.Code == request.InviteCode.Trim().ToUpperInvariant(), cancellationToken);
            if (invite is null)
                return BadRequest(new { message = "The invite code is invalid for this class." });
        }

        Guid? invitedByStudentId = invite?.CreatedBy.Role == UserRole.Student ? invite.CreatedById : null;
        if (request?.InvitedByStudentId is Guid requestedInviter && requestedInviter != invitedByStudentId)
            return BadRequest(new { message = "invitedByStudentId must match the invite code owner." });

        var discountPercent = classItem.Type == "private" && currentEnrollment >= 1 ? 30m : 0m;
        var finalPrice = Math.Round(classItem.BasePrice * (1m - discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        var enrollment = new ClassEnrollment
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ClassId = classId,
            StudentId = studentId,
            FinalPrice = finalPrice,
            DiscountPercent = discountPercent,
            Status = finalPrice > 0 ? "pending" : "confirmed",
            InviteCode = invite?.Code,
            InvitedByStudentId = invitedByStudentId,
            EnrolledAt = DateTime.UtcNow,
            CancelledAt = null
        };

        if (existing is null)
            _db.Add(enrollment);
        else
        {
            existing.FinalPrice = enrollment.FinalPrice;
            existing.DiscountPercent = enrollment.DiscountPercent;
            existing.Status = enrollment.Status;
            existing.InviteCode = enrollment.InviteCode;
            existing.InvitedByStudentId = enrollment.InvitedByStudentId;
            existing.EnrolledAt = enrollment.EnrolledAt;
            existing.CancelledAt = null;
            enrollment = existing;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(MapEnrollment(enrollment));
    }

    [HttpPost("{classId:guid}/invite")]
    [Authorize(Roles = "Teacher,Student,Admin")]
    public async Task<IActionResult> CreateInvite(Guid classId, CancellationToken cancellationToken)
    {
        var classItem = await _db.Set<CourseClass>().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (classItem is null)
            return NotFound();
        if (classItem.Type != "private")
            return BadRequest(new { message = "Invites are only available for private classes." });

        var creatorId = RequireUserId();
        if (User.IsInRole("Teacher") && classItem.TeacherId != creatorId)
            return Forbid();
        if (User.IsInRole("Student") && !await _db.Set<ClassEnrollment>().AnyAsync(enrollment => enrollment.ClassId == classId && enrollment.StudentId == creatorId && enrollment.Status != "cancelled", cancellationToken))
            return Forbid();

        var code = await CreateUniqueInviteCodeAsync(cancellationToken);
        _db.Add(new ClassInvite { Id = Guid.NewGuid(), ClassId = classId, CreatedById = creatorId, Code = code, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);

        var frontendUrl = (_configuration["Frontend:BaseUrl"] ?? "https://se-one-delta.vercel.app").TrimEnd('/');
        var inviteUrl = $"{frontendUrl}/dashboard/bookClass?classId={classId:D}&invite={Uri.EscapeDataString(code)}";
        return Ok(new { inviteCode = code, inviteUrl });
    }

    [HttpGet("{classId:guid}/check-conflict")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> CheckConflict(Guid classId, CancellationToken cancellationToken)
    {
        var classItem = await _db.Set<CourseClass>().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (classItem is null)
            return NotFound();
        if (!User.IsInRole("Admin") && classItem.TeacherId != RequireUserId())
            return Forbid();

        var hasConflict = await HasScheduleConflictAsync(classItem.TeacherId, classItem.ScheduledDate, classItem.StartTime, classItem.EndTime, classItem.Id, cancellationToken);
        return Ok(new { hasConflict });
    }

    private async Task<IActionResult> GetClassByIdAsync(Guid classId, CancellationToken cancellationToken)
    {
        var classItem = await _db.Set<CourseClass>()
            .AsNoTracking()
            .Include(item => item.Teacher)
            .Include(item => item.Course)
            .FirstOrDefaultAsync(item => item.Id == classId && item.Status == "scheduled", cancellationToken);
        if (classItem is null)
            return NotFound();

        var count = await _db.Set<ClassEnrollment>().CountAsync(enrollment => enrollment.ClassId == classId && enrollment.Status != "cancelled", cancellationToken);
        var profile = await _db.Set<TeacherProfile>().AsNoTracking().FirstOrDefaultAsync(item => item.TeacherId == classItem.TeacherId, cancellationToken);
        return Ok(new CourseClassDto
        {
            Id = classItem.Id,
            Title = classItem.Title,
            Description = classItem.Description,
            Level = classItem.Level,
            Subject = classItem.Subject,
            Type = classItem.Type,
            MaxCapacity = classItem.MaxCapacity,
            CurrentEnrollment = count,
            RemainingCapacity = Math.Max(0, classItem.MaxCapacity - count),
            BasePrice = classItem.BasePrice,
            FinalPrice = GetFinalPrice(classItem, count),
            ScheduledDate = DateTime.SpecifyKind(classItem.ScheduledDate.Date + classItem.StartTime, DateTimeKind.Utc),
            StartTime = classItem.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            EndTime = classItem.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            TimeZone = classItem.TimeZone,
            Status = classItem.Status,
            Teacher = new ClassTeacherDto { Id = classItem.TeacherId, Name = classItem.Teacher.FullName, Avatar = profile?.Avatar ?? classItem.Teacher.AvatarUrl, Rating = profile?.Rating ?? 0m },
            Course = new ClassCourseDto { Id = classItem.CourseId, Title = classItem.Course.Title, ImageUrl = classItem.Course.ImageUrl }
        });
    }

    private async Task<bool> HasScheduleConflictAsync(Guid teacherId, DateTime scheduledDate, TimeSpan startTime, TimeSpan endTime, Guid? excludeClassId, CancellationToken cancellationToken)
    {
        var candidates = await _db.Set<CourseClass>().AsNoTracking()
            .Where(item => item.TeacherId == teacherId && item.ScheduledDate == scheduledDate.Date && item.Status == "scheduled" && (!excludeClassId.HasValue || item.Id != excludeClassId.Value))
            .Select(item => new { item.StartTime, item.EndTime })
            .ToListAsync(cancellationToken);
        return candidates.Any(item => startTime < item.EndTime && item.StartTime < endTime);
    }

    private async Task<string> CreateUniqueInviteCodeAsync(CancellationToken cancellationToken)
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        }
        while (await _db.Set<ClassInvite>().AnyAsync(invite => invite.Code == code, cancellationToken));
        return code;
    }

    private static object MapEnrollment(ClassEnrollment enrollment) => new
    {
        id = enrollment.Id,
        classId = enrollment.ClassId,
        studentId = enrollment.StudentId,
        finalPrice = enrollment.FinalPrice,
        discountPercent = enrollment.DiscountPercent,
        status = enrollment.Status,
        inviteCode = enrollment.InviteCode,
        invitedByStudentId = enrollment.InvitedByStudentId,
        enrolledAt = enrollment.EnrolledAt,
        cancelledAt = enrollment.CancelledAt
    };

    private static decimal GetFinalPrice(CourseClass classItem, int currentEnrollment) =>
        classItem.Type == "private" && currentEnrollment >= 1
            ? Math.Round(classItem.BasePrice * 0.70m, 2, MidpointRounding.AwayFromZero)
            : classItem.BasePrice;

    private static bool TryParseTimes(string? startValue, string? endValue, out TimeSpan startTime, out TimeSpan endTime)
    {
        startTime = default;
        endTime = default;
        return TimeSpan.TryParseExact(startValue, @"hh\:mm", CultureInfo.InvariantCulture, out startTime) &&
               TimeSpan.TryParseExact(endValue, @"hh\:mm", CultureInfo.InvariantCulture, out endTime) &&
               endTime > startTime;
    }

    private static bool IsValidOptionalHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
    }
}
