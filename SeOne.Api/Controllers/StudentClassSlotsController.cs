using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Api.Utilities;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/class-slots")]
[Authorize(Roles = "Student")]
public sealed class StudentClassSlotsController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly IClassGroupProvisioningService _groupProvisioning;

    public StudentClassSlotsController(
        SeOneDbContext db,
        IClassGroupProvisioningService groupProvisioning)
    {
        _db = db;
        _groupProvisioning = groupProvisioning;
    }

    [HttpGet]
    public async Task<IActionResult> GetClassSlots(CancellationToken cancellationToken)
    {
        var studentId = RequireUserId();
        var assignments = await _db.Set<ClassSlotTeacherAssignment>()
            .AsNoTracking()
            .Include(x => x.ClassSlot)
                .ThenInclude(x => x.Course)
            .Include(x => x.Teacher)
            .Where(x => x.Status == "confirmed" && x.ClassSlot.Status == "scheduled")
            .OrderBy(x => x.ClassSlot.ScheduledDate)
            .ThenBy(x => x.ClassSlot.StartTime)
            .ToListAsync(cancellationToken);

        var slotIds = assignments.Select(x => x.ClassSlotId).Distinct().ToArray();
        var enrollments = await _db.Set<ClassEnrollment>().AsNoTracking()
            .Where(x => slotIds.Contains(x.ClassId) &&
                        x.StudentId == studentId &&
                        (x.Status == "confirmed" || x.Status == "active"))
            .Select(x => x.ClassId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var enrolledSlotIds = enrollments.ToHashSet();

        // Older enrolments may predate group creation or member synchronization.
        // Ensure the group and the student's membership before returning classGroupId.
        foreach (var enrolledSlotId in enrolledSlotIds)
            await _groupProvisioning.EnsureForClassAsync(enrolledSlotId, cancellationToken);

        var groupMemberships = await (
            from member in _db.Set<ClassGroupMember>().AsNoTracking()
            join classGroup in _db.Set<ClassGroup>().AsNoTracking() on member.GroupId equals classGroup.Id
            where member.UserId == studentId && member.Role == "student" &&
                  member.IsActive && classGroup.IsActive && slotIds.Contains(classGroup.ClassId)
            select new { classGroup.ClassId, classGroup.Id }
        ).ToListAsync(cancellationToken);
        var groupBySlot = groupMemberships
            .GroupBy(x => x.ClassId)
            .ToDictionary(x => x.Key, x => (Guid?)x.First().Id);

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
                AssignmentId = assignment.Id,
                TeacherId = assignment.TeacherId,
                TeacherName = assignment.Teacher.FullName,
                TeacherAvatarUrl = assignment.Teacher.AvatarUrl,
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
                Enrolled = enrolledSlotIds.Contains(slot.Id),
                ClassGroupId = groupBySlot.GetValueOrDefault(slot.Id),
                TeacherAssignments = new List<ClassSlotTeacherAssignmentDto>
                {
                    new()
                    {
                        Id = assignment.Id,
                        SlotId = assignment.ClassSlotId,
                        TeacherId = assignment.TeacherId,
                        Status = assignment.Status,
                        SelectedAt = assignment.CreatedAt,
                        TeacherName = assignment.Teacher.FullName,
                        TeacherAvatarUrl = assignment.Teacher.AvatarUrl
                    }
                },
                CourseTitle = slot.Course.Title,
                CourseImageUrl = slot.Course.ImageUrl
            };
        }).ToList();

        return Ok(slots);
    }
}
