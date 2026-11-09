using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/class-slots")]
[Authorize(Roles = "Student")]
public sealed class StudentClassSlotsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public StudentClassSlotsController(SeOneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetClassSlots(
        [FromQuery] Guid? courseId,
        [FromQuery] Guid? teacherId,
        [FromQuery] string? search,
        [FromQuery] string? level,
        [FromQuery] string? subject,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] bool availableOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 200)
            return BadRequest(new { message = "page must be positive and pageSize must be between 1 and 200." });
        if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            return BadRequest(new { message = "startDate must be on or before endDate." });

        var query = _db.Set<ClassSlotTeacherAssignment>()
            .AsNoTracking()
            .Include(x => x.ClassSlot)
                .ThenInclude(x => x.Course)
            .Include(x => x.Teacher)
            .Where(x => x.Status == "confirmed" && x.ClassSlot.Status == "scheduled");

        if (courseId.HasValue)
            query = query.Where(x => x.ClassSlot.CourseId == courseId.Value);
        if (teacherId.HasValue)
            query = query.Where(x => x.TeacherId == teacherId.Value);
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(x => x.ClassSlot.Level == level.Trim());
        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(x => x.ClassSlot.Subject == subject.Trim());
        if (startDate.HasValue)
            query = query.Where(x => x.ClassSlot.ScheduledDate >= startDate.Value.Date);
        if (endDate.HasValue)
            query = query.Where(x => x.ClassSlot.ScheduledDate < endDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(x =>
                x.ClassSlot.Title.Contains(searchTerm) ||
                x.ClassSlot.Course.Title.Contains(searchTerm) ||
                x.Teacher.FullName.Contains(searchTerm));
        }
        if (availableOnly)
        {
            query = query.Where(x => _db.Set<ClassEnrollment>().Count(enrollment =>
                enrollment.ClassId == x.ClassSlotId && enrollment.Status != "cancelled") < x.ClassSlot.MaxCapacity);
        }

        var assignments = await query
            .OrderBy(x => x.ClassSlot.ScheduledDate)
            .ThenBy(x => x.ClassSlot.StartTime)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var slotIds = assignments.Select(x => x.ClassSlotId).Distinct().ToArray();
        var enrollmentCounts = slotIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await _db.Set<ClassEnrollment>()
                .AsNoTracking()
                .Where(x => slotIds.Contains(x.ClassId) && x.Status != "cancelled")
                .GroupBy(x => x.ClassId)
                .Select(group => new { SlotId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.SlotId, x => x.Count, cancellationToken);

        var slots = assignments.Select(assignment =>
        {
            var slot = assignment.ClassSlot;
            var enrollmentCount = enrollmentCounts.GetValueOrDefault(slot.Id);
            var startsAt = DateTime.SpecifyKind(slot.ScheduledDate.Date + slot.StartTime, DateTimeKind.Utc);
            var endsAt = DateTime.SpecifyKind(slot.ScheduledDate.Date + slot.EndTime, DateTimeKind.Utc);

            return new ClassSlotDto
            {
                Id = slot.Id,
                SlotId = slot.Id,
                CourseId = slot.CourseId,
                AssignmentId = assignment.Id,
                TeacherId = assignment.TeacherId,
                TeacherName = assignment.Teacher.FullName,
                TeacherAvatarUrl = assignment.Teacher.AvatarUrl,
                StartsAt = startsAt,
                EndsAt = endsAt,
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
        }).ToList();

        return Ok(slots);
    }

}
