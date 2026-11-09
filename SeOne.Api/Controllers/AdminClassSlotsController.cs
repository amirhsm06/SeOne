using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Api.Utilities;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/admin/class-slots")]
[Authorize(Roles = "Admin")]
public sealed class AdminClassSlotsController : ControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly IClassGroupProvisioningService _classGroupProvisioningService;

    public AdminClassSlotsController(SeOneDbContext db, IClassGroupProvisioningService classGroupProvisioningService)
    {
        _db = db;
        _classGroupProvisioningService = classGroupProvisioningService;
    }

    [HttpGet]
    public async Task<IActionResult> ListSlots(CancellationToken cancellationToken)
    {
        var slots = await _db.Set<CourseClass>().AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.TeacherAssignments).ThenInclude(x => x.Teacher)
            .OrderBy(x => x.ScheduledDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);
        var slotIds = slots.Select(x => x.Id).ToArray();
        var counts = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Where(x => slotIds.Contains(x.ClassId) && x.Status != "cancelled")
            .GroupBy(x => x.ClassId)
            .Select(group => new { SlotId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.SlotId, x => x.Count, cancellationToken);

        return Ok(slots.Select(slot => MapSlot(
            slot,
            slot.TeacherAssignments.FirstOrDefault(x => x.Status == "confirmed"),
            counts.GetValueOrDefault(slot.Id))));
    }

    [HttpPatch("{slotId:guid}")]
    public async Task<IActionResult> UpdateSlot(
        Guid slotId,
        [FromBody] UpdateClassSlotRequest request,
        CancellationToken cancellationToken)
    {
        var slot = await _db.Set<CourseClass>()
            .Include(x => x.Course)
            .Include(x => x.TeacherAssignments).ThenInclude(x => x.Teacher)
            .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        var startsAt = request.StartsAt?.UtcDateTime ?? ClassSlotTime.GetStartUtc(slot);
        var endsAt = request.EndsAt?.UtcDateTime ?? ClassSlotTime.GetEndUtc(slot);
        var classType = NormalizeClassType(request.ClassType ?? slot.Type);
        var capacity = classType == "semi_private" ? 4 : request.Capacity ?? slot.MaxCapacity;
        var basePrice = request.BasePrice ?? slot.BasePrice;
        var finalPrice = request.FinalPrice ?? (request.BasePrice.HasValue ? basePrice : slot.FinalPrice);
        var currency = request.Currency?.Trim().ToUpperInvariant() ?? slot.Currency;
        if (!ClassSlotTime.TryGetTimeZone(request.TimeZone ?? slot.TimeZone, out _, out var timeZoneId))
            return BadRequest(new { message = "TimeZone must be a valid IANA or system time-zone identifier." });

        if (startsAt == default)
            return BadRequest(new { message = "A valid class start time is required." });
        if (endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromHours(24))
            return BadRequest(new { message = "The class end time must be after its start time and the duration cannot exceed 24 hours." });
        if (!IsSupportedClassType(classType))
            return BadRequest(new { message = "ClassType must be public, private, or semi_private." });
        if (capacity is < 1 or > 500)
            return BadRequest(new { message = "Class capacity must be between 1 and 500 (semi_private is fixed at 4)." });
        if (basePrice < 0 || finalPrice < 0)
            return BadRequest(new { message = "Class prices cannot be negative." });
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return BadRequest(new { message = "Currency must be a three-letter ISO currency code such as IRR or USD." });

        var currentEnrollment = await _db.Set<ClassEnrollment>()
            .CountAsync(x => x.ClassId == slotId && (x.Status == "confirmed" || x.Status == "active" || x.Status == "pending"), cancellationToken);
        if (capacity < currentEnrollment)
            return Conflict(new { message = "Capacity cannot be lower than the current enrollment count." });

        var oldStart = ClassSlotTime.GetStartUtc(slot);
        var oldEnd = ClassSlotTime.GetEndUtc(slot);
        var scheduleChanged = startsAt != oldStart || endsAt != oldEnd;
        slot.ScheduledDate = startsAt.Date;
        slot.StartTime = startsAt.TimeOfDay;
        slot.EndTime = endsAt.TimeOfDay;
        slot.TimeZone = timeZoneId;
        slot.Type = classType;
        slot.MaxCapacity = capacity;
        slot.BasePrice = basePrice;
        slot.FinalPrice = finalPrice;
        slot.Currency = currency;
        if (!string.IsNullOrWhiteSpace(request.Title))
            slot.Title = request.Title.Trim();
        if (request.Description is not null)
            slot.Description = request.Description;
        if (!string.IsNullOrWhiteSpace(request.Level))
            slot.Level = request.Level.Trim();
        if (!string.IsNullOrWhiteSpace(request.TeacherRole))
            slot.Subject = request.TeacherRole.Trim();

        if (scheduleChanged)
        {
            var assignment = await _db.Set<ClassSlotTeacherAssignment>()
                .FirstOrDefaultAsync(x => x.ClassSlotId == slotId && x.Status == "confirmed", cancellationToken);
            if (assignment is not null)
            {
                var eligibleTeachers = await GetAvailableTeachersAsync(slot, cancellationToken);
                if (!eligibleTeachers.Any(x => x.TeacherId == assignment.TeacherId))
                    return Conflict(new { message = "The assigned teacher is unavailable for the updated schedule." });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        var confirmedAssignment = slot.TeacherAssignments.FirstOrDefault(item => item.Status == "confirmed");
        await _classGroupProvisioningService.EnsureForClassAsync(slotId, cancellationToken);
        return Ok(MapSlot(slot, confirmedAssignment, currentEnrollment));
    }

    [HttpGet("{slotId:guid}/enrollments")]
    public async Task<IActionResult> GetSlotEnrollments(Guid slotId, CancellationToken cancellationToken)
    {
        if (!await _db.Set<CourseClass>().AnyAsync(x => x.Id == slotId, cancellationToken))
            return NotFound(new { message = "Class slot not found." });

        var enrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Include(x => x.Student)
            .Where(x => x.ClassId == slotId)
            .OrderByDescending(x => x.EnrolledAt)
            .Select(x => new
            {
                id = x.Id,
                slotId = x.ClassId,
                studentId = x.StudentId,
                studentName = x.Student.FullName,
                studentEmail = x.Student.Email,
                finalPrice = x.FinalPrice,
                discountPercent = x.DiscountPercent,
                status = x.Status,
                enrolledAt = x.EnrolledAt,
                cancelledAt = x.CancelledAt
            })
            .ToListAsync(cancellationToken);

        return Ok(enrollments);
    }

    [HttpPost("{slotId:guid}/enrollments")]
    public async Task<IActionResult> AddSlotEnrollment(
        Guid slotId,
        [FromBody] AddClassSlotEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var slot = await _db.Set<CourseClass>().FirstOrDefaultAsync(x => x.Id == slotId && x.Status == "scheduled", cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Scheduled class slot not found." });

        if (!await _db.Users.AnyAsync(x => x.Id == request.StudentId && x.Role == UserRole.Student, cancellationToken))
            return BadRequest(new { message = "A valid studentId is required." });

        if (await _db.Set<ClassEnrollment>().AnyAsync(x => x.ClassId == slotId && x.StudentId == request.StudentId, cancellationToken))
            return Conflict(new { message = "An enrollment record already exists for this student and slot." });

        var currentCount = await _db.Set<ClassEnrollment>()
            .CountAsync(x => x.ClassId == slotId && x.Status != "cancelled", cancellationToken);
        if (currentCount >= slot.MaxCapacity)
            return Conflict(new { message = "This class slot is full." });

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = slotId,
            StudentId = request.StudentId,
            FinalPrice = slot.FinalPrice,
            DiscountPercent = slot.BasePrice <= 0 ? 0 : Math.Clamp((slot.BasePrice - slot.FinalPrice) / slot.BasePrice * 100m, 0m, 100m),
            Status = "confirmed",
            EnrolledAt = DateTime.UtcNow
        };
        _db.Add(enrollment);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _classGroupProvisioningService.EnsureForClassAsync(slotId, cancellationToken);

        return Ok(new { id = enrollment.Id, slotId, studentId = enrollment.StudentId, finalPrice = enrollment.FinalPrice, status = enrollment.Status, enrolledAt = enrollment.EnrolledAt });
    }

    [HttpPost("~/api/admin/class-enrollments/{enrollmentId:guid}/move")]
    public async Task<IActionResult> MoveEnrollment(
        Guid enrollmentId,
        [FromBody] MoveClassEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var requestedDestinationSlotId = request.TargetSlotId != Guid.Empty
            ? request.TargetSlotId
            : request.DestinationSlotId;
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var enrollment = await _db.Set<ClassEnrollment>()
            .FirstOrDefaultAsync(x => x.Id == enrollmentId, cancellationToken);
        if (enrollment is null || enrollment.Status == "cancelled")
            return NotFound(new { message = "Active class enrollment not found." });

        var destination = await _db.Set<CourseClass>()
            .FirstOrDefaultAsync(x => x.Id == requestedDestinationSlotId && x.Status == "scheduled", cancellationToken);
        if (destination is null)
            return NotFound(new { message = "Destination class slot not found." });
        var sourceSlot = await _db.Set<CourseClass>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == enrollment.ClassId, cancellationToken);
        if (sourceSlot is null || sourceSlot.CourseId != destination.CourseId)
            return BadRequest(new { message = "Enrollment can only be moved between slots of the same course." });
        if (destination.Id == enrollment.ClassId)
            return BadRequest(new { message = "The enrollment is already in this class slot." });

        if (await _db.Set<ClassEnrollment>().AnyAsync(
                x => x.ClassId == destination.Id && x.StudentId == enrollment.StudentId && x.Status != "cancelled",
                cancellationToken))
            return Conflict(new { message = "The student is already enrolled in the destination slot." });

        var destinationCount = await _db.Set<ClassEnrollment>()
            .CountAsync(x => x.ClassId == destination.Id && x.Status != "cancelled", cancellationToken);
        if (destinationCount >= destination.MaxCapacity)
            return Conflict(new { message = "The destination class slot is full." });

        var sourceSlotId = enrollment.ClassId;
        enrollment.ClassId = destination.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _classGroupProvisioningService.EnsureForClassAsync(sourceSlotId, cancellationToken);
        await _classGroupProvisioningService.EnsureForClassAsync(destination.Id, cancellationToken);

        return Ok(new
        {
            id = enrollment.Id,
            sourceSlotId,
            destinationSlotId = destination.Id,
            studentId = enrollment.StudentId,
            status = enrollment.Status,
            finalPrice = enrollment.FinalPrice
        });
    }

    [HttpPost("~/api/admin/class-slot-assignments")]
    public Task<IActionResult> CreateSlotAssignment(
        [FromBody] CreateClassSlotAssignmentRequest request,
        CancellationToken cancellationToken) =>
        AssignTeacherToSlotAsync(request.SlotId, request.TeacherId, true, cancellationToken);

    [HttpPatch("~/api/admin/class-slot-assignments/{assignmentId:guid}")]
    public async Task<IActionResult> UpdateSlotAssignment(
        Guid assignmentId,
        [FromBody] UpdateClassSlotAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TeacherId is null && string.IsNullOrWhiteSpace(request.Status))
            return BadRequest(new { message = "Provide a teacherId or status to update." });

        var assignment = await _db.Set<ClassSlotTeacherAssignment>()
            .FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
        if (assignment is null)
            return NotFound(new { message = "Class-slot assignment not found." });

        var status = request.Status?.Trim().ToLowerInvariant() ?? assignment.Status;
        if (status is not ("confirmed" or "cancelled"))
            return BadRequest(new { message = "Assignment status must be confirmed or cancelled." });

        var slot = await _db.Set<CourseClass>()
            .FirstOrDefaultAsync(x => x.Id == assignment.ClassSlotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        if (status == "cancelled")
        {
            assignment.Status = status;
            if (slot.TeacherId == assignment.TeacherId)
                slot.TeacherId = null;
        }
        else
        {
            var teacherId = request.TeacherId ?? assignment.TeacherId;
            var eligibleTeachers = await GetAvailableTeachersAsync(slot, cancellationToken);
            if (!eligibleTeachers.Any(x => x.TeacherId == teacherId))
                return Conflict(new { message = "The teacher is unavailable for this slot or has a conflicting confirmed class assignment." });

            assignment.TeacherId = teacherId;
            assignment.Status = "confirmed";
            slot.TeacherId = teacherId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _classGroupProvisioningService.EnsureForClassAsync(slot.Id, cancellationToken);
        return Ok(new { assignmentId = assignment.Id, slotId = assignment.ClassSlotId, teacherId = assignment.TeacherId, status = assignment.Status });
    }

    [HttpPost]
    public async Task<IActionResult> CreateSlot(
        [FromBody] CreateClassSlotRequest request,
        CancellationToken cancellationToken)
    {
        var course = await _db.Set<Course>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CourseId, cancellationToken);
        if (course is null)
            return NotFound(new { message = "Course not found." });

        var currency = request.Currency?.Trim().ToUpperInvariant();
        var classType = NormalizeClassType(request.ClassType);
        var capacity = classType == "semi_private" ? 4 : request.Capacity;
        var finalPrice = request.FinalPrice ?? request.BasePrice;
        if (!ClassSlotTime.TryGetTimeZone(request.TimeZone, out _, out var timeZoneId))
            return BadRequest(new { message = "TimeZone must be a valid IANA or system time-zone identifier." });

        var startsAt = request.StartsAt.UtcDateTime;
        var endsAt = request.EndsAt.UtcDateTime;
        if (request.StartsAt == default || request.EndsAt == default || startsAt == default)
            return BadRequest(new { message = "Valid class start and end times are required." });
        if (endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromHours(24))
            return BadRequest(new { message = "The class end time must be after its start time and the duration cannot exceed 24 hours." });
        if (!IsSupportedClassType(classType))
            return BadRequest(new { message = "ClassType must be public, private, or semi_private." });
        if (capacity is < 1 or > 500)
            return BadRequest(new { message = "Class capacity must be between 1 and 500 (semi_private is fixed at 4)." });
        if (request.BasePrice < 0 || finalPrice < 0)
            return BadRequest(new { message = "Class prices cannot be negative." });
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return BadRequest(new { message = "Currency must be a three-letter ISO currency code such as IRR or USD." });

        var slot = new CourseClass
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            TeacherId = null,
            Title = string.IsNullOrWhiteSpace(request.Title) ? course.Title : request.Title.Trim(),
            Description = request.Description ?? course.Description,
            Level = string.IsNullOrWhiteSpace(request.Level) ? course.Level : request.Level.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.TeacherRole) ? course.Category : request.TeacherRole.Trim(),
            Type = classType,
            MaxCapacity = capacity,
            BasePrice = request.BasePrice,
            FinalPrice = finalPrice,
            Currency = currency,
            ScheduledDate = startsAt.Date,
            StartTime = startsAt.TimeOfDay,
            EndTime = endsAt.TimeOfDay,
            TimeZone = timeZoneId,
            Status = "scheduled",
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<CourseClass>().Add(slot);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(CreateSlot), new { slotId = slot.Id }, new
        {
            id = slot.Id,
            courseId = slot.CourseId,
            startsAt = ClassSlotTime.GetStartUtc(slot),
            endsAt = ClassSlotTime.GetEndUtc(slot),
            classType = slot.Type,
            title = slot.Title,
            description = slot.Description,
            level = slot.Level,
            teacherRole = slot.Subject,
            language = course.Language,
            capacity = slot.MaxCapacity,
            enrollmentCount = 0,
            basePrice = slot.BasePrice,
            finalPrice = slot.FinalPrice,
            currency = slot.Currency,
            timeZone = slot.TimeZone,
            status = slot.Status,
            enrolledCount = 0,
            remainingSeats = slot.MaxCapacity,
            teacherAssignments = Array.Empty<object>()
        });
    }

    [HttpGet("{slotId:guid}/available-teachers")]
    public async Task<IActionResult> GetAvailableTeachers(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await _db.Set<CourseClass>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        var candidates = await GetAvailableTeachersAsync(slot, cancellationToken);
        return Ok(candidates);
    }

    [HttpPut("{slotId:guid}/teacher")]
    public async Task<IActionResult> AssignTeacher(
        Guid slotId,
        [FromBody] AssignClassSlotTeacherRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TeacherId.HasValue)
            return await AssignTeacherToSlotAsync(slotId, request.TeacherId.Value, false, cancellationToken);

        var slot = await _db.Set<CourseClass>().FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        var assignments = await _db.Set<ClassSlotTeacherAssignment>()
            .Where(x => x.ClassSlotId == slotId && x.Status == "confirmed")
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignments)
            assignment.Status = "cancelled";
        slot.TeacherId = null;
        await _db.SaveChangesAsync(cancellationToken);
        await _classGroupProvisioningService.EnsureForClassAsync(slotId, cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> AssignTeacherToSlotAsync(
        Guid slotId,
        Guid teacherId,
        bool onlyCreate,
        CancellationToken cancellationToken)
    {
        var slot = await _db.Set<CourseClass>()
            .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        var teacher = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == teacherId && x.Role == UserRole.Teacher && x.AccountStatus == "active", cancellationToken);
        if (teacher is null)
            return BadRequest(new { message = "A valid active teacherId is required." });

        var availableTeachers = await GetAvailableTeachersAsync(slot, cancellationToken);
        if (!availableTeachers.Any(x => x.TeacherId == teacher.Id))
            return Conflict(new { message = "The teacher is unavailable for this slot or has a conflicting confirmed class assignment." });

        var slotAssignments = await _db.Set<ClassSlotTeacherAssignment>()
            .Where(x => x.ClassSlotId == slotId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        if (onlyCreate && slotAssignments.Any(x => x.Status == "confirmed"))
            return Conflict(new { message = "A confirmed teacher assignment already exists for this slot." });

        var currentConfirmed = slotAssignments.FirstOrDefault(x => x.Status == "confirmed");
        if (currentConfirmed is not null && currentConfirmed.TeacherId == teacher.Id)
        {
            await _classGroupProvisioningService.EnsureForClassAsync(slotId, cancellationToken);
            return Ok(new
            {
                assignmentId = currentConfirmed.Id,
                slotId,
                teacherId = teacher.Id,
                teacherName = teacher.FullName,
                teacherAvatarUrl = teacher.AvatarUrl,
                status = currentConfirmed.Status,
                createdAt = currentConfirmed.CreatedAt
            });
        }

        foreach (var previous in slotAssignments.Where(x => x.Status == "confirmed"))
            previous.Status = "cancelled";

        // Keep the former assignment as history; a replacement receives a new ID.
        var assignment = new ClassSlotTeacherAssignment
        {
            Id = Guid.NewGuid(),
            ClassSlotId = slotId,
            TeacherId = teacher.Id,
            Status = "confirmed",
            CreatedAt = DateTime.UtcNow
        };
        _db.Add(assignment);
        slot.TeacherId = teacher.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await _classGroupProvisioningService.EnsureForClassAsync(slotId, cancellationToken);

        return Ok(new
        {
            assignmentId = assignment.Id,
            slotId,
            teacherId = teacher.Id,
            teacherName = teacher.FullName,
            teacherAvatarUrl = teacher.AvatarUrl,
            status = assignment.Status,
            createdAt = assignment.CreatedAt
        });
    }

    private static string NormalizeClassType(string? value)
    {
        var normalized = (value ?? "public").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized switch
        {
            "semi_private" or "semiprivate" => "semi_private",
            "public" => "public",
            "private" => "private",
            _ => normalized
        };
    }

    private static bool IsSupportedClassType(string value) =>
        value is "public" or "private" or "semi_private";

    private static ClassSlotDto MapSlot(CourseClass slot, ClassSlotTeacherAssignment? assignment, int enrollmentCount)
    {
        return new ClassSlotDto
        {
            Id = slot.Id,
            Title = slot.Title,
            Description = slot.Description,
            Level = slot.Level,
            TeacherRole = slot.Subject,
            Language = slot.Course.Language,
            SlotId = slot.Id,
            CourseId = slot.CourseId,
            AssignmentId = assignment?.Id,
            TeacherId = assignment?.TeacherId,
            TeacherName = assignment?.Teacher.FullName,
            TeacherAvatarUrl = assignment?.Teacher.AvatarUrl,
            StartsAt = ClassSlotTime.GetStartUtc(slot),
            EndsAt = ClassSlotTime.GetEndUtc(slot),
            ClassType = slot.Type,
            Capacity = slot.MaxCapacity,
            EnrollmentCount = enrollmentCount,
            CurrentEnrollment = enrollmentCount,
            BasePrice = slot.BasePrice,
            FinalPrice = slot.FinalPrice,
            Currency = slot.Currency,
            TimeZone = slot.TimeZone,
            Status = slot.Status,
            EnrolledCount = enrollmentCount,
            RemainingSeats = Math.Max(0, slot.MaxCapacity - enrollmentCount),
            TeacherAssignments = slot.TeacherAssignments
                .Select(item => new ClassSlotTeacherAssignmentDto
                {
                    Id = item.Id,
                    SlotId = item.ClassSlotId,
                    TeacherId = item.TeacherId,
                    Status = item.Status,
                    SelectedAt = item.CreatedAt,
                    TeacherName = item.Teacher.FullName,
                    TeacherAvatarUrl = item.Teacher.AvatarUrl
                })
                .ToList(),
            CourseTitle = slot.Course.Title,
            CourseImageUrl = slot.Course.ImageUrl
        };
    }

    private async Task<List<AvailableClassSlotTeacherDto>> GetAvailableTeachersAsync(
        CourseClass slot,
        CancellationToken cancellationToken)
    {
        var slotStartUtc = ClassSlotTime.GetStartUtc(slot);
        var slotEndUtc = ClassSlotTime.GetEndUtc(slot);
        var teachers = await _db.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Teacher && user.AccountStatus == "active")
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);
        if (teachers.Count == 0)
            return new List<AvailableClassSlotTeacherDto>();

        var teacherIds = teachers.Select(x => x.Id).ToArray();
        var availabilities = await _db.Set<TeacherAvailability>().AsNoTracking()
            .Where(x => teacherIds.Contains(x.TeacherId) && x.IsAvailable)
            .ToListAsync(cancellationToken);
        var profiles = await _db.Set<TeacherProfile>().AsNoTracking()
            .Where(x => teacherIds.Contains(x.TeacherId))
            .ToDictionaryAsync(x => x.TeacherId, cancellationToken);
        var confirmedAssignments = await _db.Set<ClassSlotTeacherAssignment>().AsNoTracking()
            .Include(x => x.ClassSlot)
            .Where(x => teacherIds.Contains(x.TeacherId) &&
                        x.Status == "confirmed" &&
                        x.ClassSlotId != slot.Id &&
                        x.ClassSlot.Status == "scheduled")
            .ToListAsync(cancellationToken);

        var result = new List<AvailableClassSlotTeacherDto>();
        foreach (var teacher in teachers)
        {
            var teacherTimeZone = TimeZoneInfo.Utc;
            if (!string.IsNullOrWhiteSpace(teacher.Timezone) &&
                ClassSlotTime.TryGetTimeZone(teacher.Timezone, out var parsedTimeZone, out _))
                teacherTimeZone = parsedTimeZone;

            var localStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(slotStartUtc, DateTimeKind.Utc), teacherTimeZone);
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(slotEndUtc, DateTimeKind.Utc), teacherTimeZone);

            // Weekly availability is expressed in the teacher's own local timezone.
            // An overnight slot cannot be represented by one same-day availability row.
            if (localStart.Date != localEnd.Date)
                continue;

            var matchingAvailability = availabilities
                .Where(x => x.TeacherId == teacher.Id &&
                            x.DayOfWeek == localStart.DayOfWeek &&
                            x.StartTime <= localStart.TimeOfDay &&
                            x.EndTime >= localEnd.TimeOfDay)
                .OrderBy(x => x.StartTime)
                .FirstOrDefault();
            if (matchingAvailability is null)
                continue;

            var hasConflict = confirmedAssignments
                .Where(x => x.TeacherId == teacher.Id)
                .Any(x => ClassSlotTime.Overlaps(
                    slotStartUtc,
                    slotEndUtc,
                    ClassSlotTime.GetStartUtc(x.ClassSlot),
                    ClassSlotTime.GetEndUtc(x.ClassSlot)));
            if (hasConflict)
                continue;

            profiles.TryGetValue(teacher.Id, out var profile);
            var matched = $"{localStart:dddd}, {localStart:HH:mm}–{localEnd:HH:mm}";
            result.Add(new AvailableClassSlotTeacherDto
            {
                Id = teacher.Id,
                Name = teacher.FullName,
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                AvatarUrl = profile?.Avatar ?? teacher.AvatarUrl,
                TeacherAvatarUrl = profile?.Avatar ?? teacher.AvatarUrl,
                TeachingLanguage = profile?.TeachingLanguage ?? string.Empty,
                Subject = profile?.Subject ?? slot.Subject,
                MatchedAvailability = matched
            });
        }

        return result;
    }

}
