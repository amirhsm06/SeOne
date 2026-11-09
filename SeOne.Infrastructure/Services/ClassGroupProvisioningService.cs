using Microsoft.EntityFrameworkCore;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public sealed class ClassGroupProvisioningService : IClassGroupProvisioningService
{
    private readonly SeOneDbContext _context;

    public ClassGroupProvisioningService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task EnsureForClassAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        var classItem = await _context.Set<CourseClass>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == classId, cancellationToken);

        if (classItem is null)
        {
            return;
        }

        var group = await _context.Set<ClassGroup>()
            .FirstOrDefaultAsync(x => x.ClassId == classId, cancellationToken);

        if (group is null)
        {
            group = new ClassGroup
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                Title = classItem.Title,
                GroupType = classItem.Type,
                CreatedAt = DateTime.UtcNow,
                IsActive = classItem.Status != "cancelled"
            };
            _context.Set<ClassGroup>().Add(group);
        }
        else
        {
            group.Title = classItem.Title;
            group.GroupType = classItem.Type;
            group.IsActive = classItem.Status != "cancelled";
        }

        var enrollments = await _context.Set<ClassEnrollment>()
            .AsNoTracking()
            .Where(x => x.ClassId == classId && x.Status != "cancelled")
            .Select(x => x.StudentId)
            .ToListAsync(cancellationToken);
        var currentMembers = await _context.Set<ClassGroupMember>()
            .Where(x => x.GroupId == group.Id)
            .ToListAsync(cancellationToken);
        var enrolledStudentIds = enrollments.ToHashSet();

        foreach (var member in currentMembers)
        {
            if (member.Role == "student")
            {
                member.IsActive = enrolledStudentIds.Contains(member.UserId);
            }
        }

        var existingStudentIds = currentMembers
            .Where(x => x.Role == "student")
            .Select(x => x.UserId)
            .ToHashSet();
        foreach (var studentId in enrolledStudentIds.Except(existingStudentIds))
        {
            _context.Set<ClassGroupMember>().Add(new ClassGroupMember
            {
                GroupId = group.Id,
                UserId = studentId,
                Role = "student",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        var assignment = await _context.Set<ClassSlotTeacherAssignment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClassSlotId == classId && x.Status == "confirmed", cancellationToken);
        if (assignment is not null)
        {
            var teacherMember = currentMembers.FirstOrDefault(x => x.UserId == assignment.TeacherId);
            if (teacherMember is null)
            {
                _context.Set<ClassGroupMember>().Add(new ClassGroupMember
                {
                    GroupId = group.Id,
                    UserId = assignment.TeacherId,
                    Role = "teacher",
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
            else
            {
                teacherMember.Role = "teacher";
                teacherMember.IsActive = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
