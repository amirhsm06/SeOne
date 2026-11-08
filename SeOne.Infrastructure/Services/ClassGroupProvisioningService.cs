using System.Data;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

/// <summary>
/// Keeps exactly one ClassGroup per CourseClass and synchronizes its active members.
/// This service deliberately does not decide whether an enrollment is paid: callers must
/// invoke it only after the existing payment/enrollment workflow has confirmed the seat.
/// </summary>
public sealed class ClassGroupProvisioningService : IClassGroupProvisioningService
{
    private readonly SeOneDbContext _db;

    public ClassGroupProvisioningService(SeOneDbContext db) => _db = db;

    public async Task<Guid?> EnsureForClassAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var courseClass = await _db.CourseClasses
            .SingleOrDefaultAsync(x => x.Id == classId, cancellationToken);

        if (courseClass is null)
            return null;

        var group = await _db.ClassGroups
            .SingleOrDefaultAsync(x => x.ClassId == classId, cancellationToken);

        var confirmedEnrollments = await _db.ClassEnrollments
            .AsNoTracking()
            .Where(x => x.ClassId == classId)
            .ToListAsync(cancellationToken);

        // This project currently uses "confirmed" for paid/free confirmed seats.
        // "active" is also accepted for compatibility with the newer API contract.
        var activeStudentIds = confirmedEnrollments
            .Where(x => IsActiveEnrollmentStatus(x.Status))
            .Select(x => x.StudentId)
            .Distinct()
            .ToHashSet();

        var classIsCancelled = string.Equals(courseClass.Status, "cancelled", StringComparison.OrdinalIgnoreCase);
        if (group is null && (activeStudentIds.Count == 0 || classIsCancelled))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (group is null)
        {
            group = new ClassGroup
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                Title = string.IsNullOrWhiteSpace(courseClass.Title)
                    ? "Class group"
                    : $"{courseClass.Title} — Group",
                GroupType = NormalizeGroupType(courseClass.Type),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.ClassGroups.Add(group);
            await _db.SaveChangesAsync(cancellationToken);
        }

        group.IsActive = !classIsCancelled;
        group.Title = string.IsNullOrWhiteSpace(courseClass.Title)
            ? group.Title
            : $"{courseClass.Title} — Group";
        group.GroupType = NormalizeGroupType(courseClass.Type);

        var desiredMembers = new Dictionary<Guid, string>();

        // CourseClass.TeacherId is the Identity User.Id for the assigned teacher.
        // SeOne stores teacher approval state on User.AccountStatus.
        var teacher = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == courseClass.TeacherId
                     && x.Role == UserRole.Teacher
                     && x.AccountStatus == "active",
                cancellationToken);

        if (teacher is not null)
            desiredMembers[teacher.Id] = "Teacher";

        foreach (var studentId in activeStudentIds)
            desiredMembers[studentId] = "Student";

        var existingMembers = await _db.ClassGroupMembers
            .Where(x => x.GroupId == group.Id)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingMembers)
        {
            if (desiredMembers.TryGetValue(existing.UserId, out var role) && !classIsCancelled)
            {
                existing.IsActive = true;
                existing.Role = role;
                desiredMembers.Remove(existing.UserId);
            }
            else
            {
                existing.IsActive = false;
            }
        }

        if (!classIsCancelled)
        {
            foreach (var (userId, role) in desiredMembers)
            {
                _db.ClassGroupMembers.Add(new ClassGroupMember
                {
                    GroupId = group.Id,
                    UserId = userId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return group.Id;
    }

    private static bool IsActiveEnrollmentStatus(string? status) =>
        string.Equals(status, "confirmed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeGroupType(string? type) =>
        string.Equals(type, "private", StringComparison.OrdinalIgnoreCase) ? "private" : "public";

}
