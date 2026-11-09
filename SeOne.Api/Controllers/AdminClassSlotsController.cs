using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
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

    public AdminClassSlotsController(SeOneDbContext db) => _db = db;

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
        CancellationToken cancellationToken)
    {
        var slot = await _db.Set<CourseClass>()
            .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken);
        if (slot is null)
            return NotFound(new { message = "Class slot not found." });

        var teacher = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TeacherId && x.Role == UserRole.Teacher && x.AccountStatus == "active", cancellationToken);
        if (teacher is null)
            return BadRequest(new { message = "A valid active teacherId is required." });

        var availableTeachers = await GetAvailableTeachersAsync(slot, cancellationToken);
        if (!availableTeachers.Any(x => x.TeacherId == teacher.Id))
            return Conflict(new { message = "The teacher is unavailable for this slot or has a conflicting confirmed class assignment." });

        var existingAssignment = await _db.Set<ClassSlotTeacherAssignment>()
            .FirstOrDefaultAsync(x => x.ClassSlotId == slotId, cancellationToken);
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
