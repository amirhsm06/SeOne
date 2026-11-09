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
    public async Task<IActionResult> GetClassSlots(CancellationToken cancellationToken)
    {
        var assignments = await _db.Set<ClassSlotTeacherAssignment>()
            .AsNoTracking()
            .Include(x => x.ClassSlot)
                .ThenInclude(x => x.Course)
            .Include(x => x.Teacher)
            .Where(x => x.Status == "confirmed" && x.ClassSlot.Status == "scheduled")
            .OrderBy(x => x.ClassSlot.ScheduledDate)
            .ThenBy(x => x.ClassSlot.StartTime)
            .ToListAsync(cancellationToken);

        var slotIds = assignments.Select(x => x.ClassSlotId).ToArray();
        var enrollmentCounts = await _db.Set<ClassEnrollment>()
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
