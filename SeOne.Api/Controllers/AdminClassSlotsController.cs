using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        var startsAt = request.StartsAt ?? slot.ScheduledDate.Date + slot.StartTime;
        var endsAt = request.EndsAt ?? slot.ScheduledDate.Date + slot.EndTime;
        var classType = request.ClassType?.Trim().ToLowerInvariant() ?? slot.Type;
        var capacity = request.Capacity ?? slot.MaxCapacity;
        var basePrice = request.BasePrice ?? slot.BasePrice;
        var finalPrice = request.FinalPrice ?? (request.BasePrice.HasValue ? basePrice : slot.FinalPrice);
        var currency = request.Currency?.Trim().ToUpperInvariant() ?? slot.Currency;

        if (startsAt == default || endsAt <= startsAt || startsAt.Date != endsAt.Date ||
            capacity is < 1 or > 500 ||
            (!string.Equals(classType, "public", StringComparison.Ordinal) && !string.Equals(classType, "private", StringComparison.Ordinal)) ||
            basePrice < 0 || finalPrice < 0 || string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return BadRequest(new { message = "Class slot details are invalid." });

        var currentEnrollment = await _db.Set<ClassEnrollment>()
            .CountAsync(x => x.ClassId == slotId && x.Status != "cancelled", cancellationToken);
        if (capacity < currentEnrollment)
            return Conflict(new { message = "Capacity cannot be lower than the current enrollment count." });

        var scheduleChanged = startsAt != slot.ScheduledDate.Date + slot.StartTime ||
                              endsAt != slot.ScheduledDate.Date + slot.EndTime;
        slot.ScheduledDate = startsAt.Date;
        slot.StartTime = startsAt.TimeOfDay;
        slot.EndTime = endsAt.TimeOfDay;
        slot.Type = classType;
        slot.MaxCapacity = capacity;
        slot.BasePrice = basePrice;
        slot.FinalPrice = finalPrice;
        slot.Currency = currency;

        if (scheduleChanged)
        {
            var assignment = await _db.Set<ClassSlotTeacherAssignment>()
                .FirstOrDefaultAsync(x => x.ClassSlotId == slotId && x.Status == "confirmed", cancellationToken);
            if (assignment is not null)
            {
                var availableTeachers = await GetAvailableTeachersAsync(slot, cancellationToken);
                if (!availableTeachers.Any(x => x.TeacherId == assignment.TeacherId))
                    return Conflict(new { message = "The assigned teacher is unavailable for the updated schedule." });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { id = slot.Id, courseId = slot.CourseId, startsAt, endsAt, classType, capacity, basePrice, finalPrice, currency });
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
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var enrollment = await _db.Set<ClassEnrollment>()
            .FirstOrDefaultAsync(x => x.Id == enrollmentId, cancellationToken);
        if (enrollment is null || enrollment.Status == "cancelled")
            return NotFound(new { message = "Active class enrollment not found." });

        var destination = await _db.Set<CourseClass>()
            .FirstOrDefaultAsync(x => x.Id == request.DestinationSlotId && x.Status == "scheduled", cancellationToken);
        if (destination is null)
            return NotFound(new { message = "Destination class slot not found." });
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
        var finalPrice = request.FinalPrice ?? request.BasePrice;
        if (request.StartsAt == default || request.EndsAt <= request.StartsAt ||
            request.StartsAt.Date != request.EndsAt.Date ||
            request.Capacity is < 1 or > 500 || request.BasePrice < 0 || finalPrice < 0 ||
            (!string.Equals(request.ClassType, "public", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(request.ClassType, "private", StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return BadRequest(new { message = "Class slot details are invalid." });

        var slot = new CourseClass
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            TeacherId = null,
            Title = course.Title,
            Description = course.Description,
            Level = course.Level,
            Subject = course.Category,
            Type = request.ClassType.Trim().ToLowerInvariant(),
            MaxCapacity = request.Capacity,
            BasePrice = request.BasePrice,
            FinalPrice = finalPrice,
            Currency = currency,
            ScheduledDate = request.StartsAt.Date,
            StartTime = request.StartsAt.TimeOfDay,
            EndTime = request.EndsAt.TimeOfDay,
            TimeZone = "UTC",
            Status = "scheduled",
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<CourseClass>().Add(slot);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(CreateSlot), new { slotId = slot.Id }, new
        {
            id = slot.Id,
            courseId = slot.CourseId,
            startsAt = DateTime.SpecifyKind(slot.ScheduledDate.Date + slot.StartTime, DateTimeKind.Utc),
            endsAt = DateTime.SpecifyKind(slot.ScheduledDate.Date + slot.EndTime, DateTimeKind.Utc),
            classType = slot.Type,
            capacity = slot.MaxCapacity,
            enrollmentCount = 0,
            basePrice = slot.BasePrice,
            finalPrice = slot.FinalPrice,
            currency = slot.Currency
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
        CancellationToken cancellationToken) =>
        await AssignTeacherToSlotAsync(slotId, request.TeacherId, false, cancellationToken);

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

        var existingAssignment = await _db.Set<ClassSlotTeacherAssignment>()
            .FirstOrDefaultAsync(x => x.ClassSlotId == slotId, cancellationToken);
        if (onlyCreate && existingAssignment?.Status == "confirmed")
            return Conflict(new { message = "A confirmed teacher assignment already exists for this slot." });

        var assignment = existingAssignment ?? new ClassSlotTeacherAssignment
        {
            Id = Guid.NewGuid(),
            ClassSlotId = slotId,
        };
        assignment.TeacherId = teacher.Id;
        assignment.Status = "confirmed";
        assignment.CreatedAt = DateTime.UtcNow;
        if (existingAssignment is null)
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

    private static ClassSlotDto MapSlot(CourseClass slot, ClassSlotTeacherAssignment? assignment, int enrollmentCount)
    {
        return new ClassSlotDto
        {
            Id = slot.Id,
            SlotId = slot.Id,
            CourseId = slot.CourseId,
            AssignmentId = assignment?.Id,
            TeacherId = assignment?.TeacherId,
            TeacherName = assignment?.Teacher.FullName,
            TeacherAvatarUrl = assignment?.Teacher.AvatarUrl,
            StartsAt = DateTime.SpecifyKind(slot.ScheduledDate.Date + slot.StartTime, DateTimeKind.Utc),
            EndsAt = DateTime.SpecifyKind(slot.ScheduledDate.Date + slot.EndTime, DateTimeKind.Utc),
            ClassType = slot.Type,
            Capacity = slot.MaxCapacity,
            EnrollmentCount = enrollmentCount,
            CurrentEnrollment = enrollmentCount,
            BasePrice = slot.BasePrice,
            FinalPrice = slot.FinalPrice,
            Currency = slot.Currency,
            CourseTitle = slot.Course.Title,
            CourseImageUrl = slot.Course.ImageUrl
        };
    }

    private async Task<List<AvailableClassSlotTeacherDto>> GetAvailableTeachersAsync(
        CourseClass slot,
        CancellationToken cancellationToken)
    {
        var startsAt = slot.ScheduledDate.Date + slot.StartTime;
        var endsAt = slot.ScheduledDate.Date + slot.EndTime;
        var day = slot.ScheduledDate.DayOfWeek;

        return await _db.Users.AsNoTracking()
            .Where(teacher => teacher.Role == UserRole.Teacher && teacher.AccountStatus == "active")
            .Where(teacher => _db.Set<TeacherAvailability>().Any(availability =>
                availability.TeacherId == teacher.Id &&
                availability.DayOfWeek == day &&
                availability.IsAvailable &&
                availability.StartTime <= startsAt.TimeOfDay &&
                availability.EndTime >= endsAt.TimeOfDay))
            .Where(teacher => !_db.Set<ClassSlotTeacherAssignment>().Any(assignment =>
                assignment.TeacherId == teacher.Id &&
                assignment.Status == "confirmed" &&
                assignment.ClassSlotId != slot.Id &&
                assignment.ClassSlot.Status == "scheduled" &&
                assignment.ClassSlot.ScheduledDate == slot.ScheduledDate &&
                startsAt.TimeOfDay < assignment.ClassSlot.EndTime &&
                assignment.ClassSlot.StartTime < endsAt.TimeOfDay))
            .OrderBy(teacher => teacher.FullName)
            .Select(teacher => new AvailableClassSlotTeacherDto
            {
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                TeacherAvatarUrl = teacher.AvatarUrl
            })
            .ToListAsync(cancellationToken);
    }
}
