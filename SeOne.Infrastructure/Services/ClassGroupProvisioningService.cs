using Microsoft.EntityFrameworkCore;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public sealed class ClassGroupProvisioningService : IClassGroupProvisioningService
{
    private readonly SeOneDbContext _context;

    public ClassGroupProvisioningService(SeOneDbContext context) => _context = context;

    public async Task EnsureForClassAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        var classItem = await _context.Set<CourseClass>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == classId, cancellationToken);
        if (classItem is null)
            return;

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

        var studentIds = await _context.Set<ClassEnrollment>()
            .AsNoTracking()
            .Where(x => x.ClassId == classId &&
                        (x.Status == "confirmed" || x.Status == "active"))
            .Select(x => x.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignment = await _context.Set<ClassSlotTeacherAssignment>()
            .AsNoTracking()
            .Where(x => x.ClassSlotId == classId && x.Status == "confirmed")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var desiredMembers = new Dictionary<Guid, string>();
        foreach (var studentId in studentIds)
            desiredMembers[studentId] = "student";
        if (assignment is not null)
            desiredMembers[assignment.TeacherId] = "teacher";

        // The composite key is GroupId + UserId. Update an existing member's
        // role instead of inserting a duplicate when a user changes roles.
        var currentMembers = await _context.Set<ClassGroupMember>()
            .Where(x => x.GroupId == group.Id)
            .ToListAsync(cancellationToken);

        foreach (var member in currentMembers)
        {
            if (desiredMembers.TryGetValue(member.UserId, out var desiredRole))
            {
                member.Role = desiredRole;
                member.IsActive = group.IsActive;
                desiredMembers.Remove(member.UserId);
            }
            else
            {
                member.IsActive = false;
            }
        }

        foreach (var pair in desiredMembers)
        {
            _context.Set<ClassGroupMember>().Add(new ClassGroupMember
            {
                GroupId = group.Id,
                UserId = pair.Key,
                Role = pair.Value,
                JoinedAt = DateTime.UtcNow,
                IsActive = group.IsActive
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
