using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminCourseInstancesController : ControllerBase
{
    private readonly SeOneDbContext _db;
    public AdminCourseInstancesController(SeOneDbContext db) { _db = db; }

    [HttpPost("courses/{courseId:guid}/instances")]
    public async Task<IActionResult> CreateInstance(Guid courseId, [FromBody] CourseInstanceRequest r)
    {
        if (!await _db.Set<Course>().AnyAsync(c => c.Id == courseId)) return NotFound();

        var instance = new CourseInstance
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(instance);
        await _db.SaveChangesAsync();

        return Created($"/api/admin/course-instances/{instance.Id}", new { id = instance.Id, courseId = instance.CourseId, startDate = instance.StartDate, endDate = instance.EndDate, createdAt = instance.CreatedAt });
    }

    [HttpGet("courses/{courseId:guid}/instances")]
    public async Task<IActionResult> ListInstances(Guid courseId)
    {
        if (!await _db.Set<Course>().AnyAsync(c => c.Id == courseId)) return NotFound();

        var list = await _db.Set<CourseInstance>().AsNoTracking().Where(i => i.CourseId == courseId).OrderBy(i => i.CreatedAt).Select(i => new
        {
            id = i.Id,
            courseId = i.CourseId,
            startDate = i.StartDate,
            endDate = i.EndDate,
            createdAt = i.CreatedAt,
            teachers = i.Teachers.Select(t => new { teacherId = t.TeacherId, fullName = t.Teacher.FullName, avatarUrl = t.Teacher.AvatarUrl ?? string.Empty })
        }).ToListAsync();

        return Ok(list);
    }

    [HttpPut("course-instances/{instanceId:guid}")]
    public async Task<IActionResult> UpdateInstance(Guid instanceId, [FromBody] CourseInstanceRequest r)
    {
        var instance = await _db.Set<CourseInstance>().FindAsync(instanceId);
        if (instance is null) return NotFound();
        instance.StartDate = r.StartDate;
        instance.EndDate = r.EndDate;
        await _db.SaveChangesAsync();
        return Ok(new { id = instance.Id, courseId = instance.CourseId, startDate = instance.StartDate, endDate = instance.EndDate, createdAt = instance.CreatedAt });
    }

    [HttpDelete("course-instances/{instanceId:guid}")]
    public async Task<IActionResult> DeleteInstance(Guid instanceId)
    {
        var instance = await _db.Set<CourseInstance>().Include(i => i.Teachers).FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null) return NotFound();

        // delete related CourseInstanceTeacher rows explicitly because of Restrict behavior
        var assignments = await _db.Set<CourseInstanceTeacher>().Where(t => t.CourseInstanceId == instanceId).ToListAsync();
        if (assignments.Any()) _db.RemoveRange(assignments);

        _db.Remove(instance);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("course-instances/{instanceId:guid}/teachers/{teacherId:guid}")]
    public async Task<IActionResult> AssignTeacher(Guid instanceId, Guid teacherId)
    {
        var instance = await _db.Set<CourseInstance>().Include(i => i.Course).FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null) return NotFound(new { message = "Course instance not found." });

        var user = await _db.Users.FindAsync(teacherId);
        if (user is null || user.Role != UserRole.Teacher) return BadRequest(new { message = "User is not a teacher or does not exist." });
        if (user.AccountStatus == "deleted") return BadRequest(new { message = "Teacher is deleted." });

        var exists = await _db.Set<CourseInstanceTeacher>().AnyAsync(x => x.CourseInstanceId == instanceId && x.TeacherId == teacherId);
        if (exists) return Conflict(new { message = "Teacher already assigned to this instance." });

        var assign = new CourseInstanceTeacher { Id = Guid.NewGuid(), CourseInstanceId = instanceId, TeacherId = teacherId, CreatedAt = DateTime.UtcNow };
        _db.Add(assign);
        await _db.SaveChangesAsync();

        return Ok(new { id = assign.Id, courseInstanceId = assign.CourseInstanceId, teacherId = assign.TeacherId, createdAt = assign.CreatedAt });
    }

    [HttpDelete("course-instances/{instanceId:guid}/teachers/{teacherId:guid}")]
    public async Task<IActionResult> RemoveTeacher(Guid instanceId, Guid teacherId)
    {
        var assign = await _db.Set<CourseInstanceTeacher>().FirstOrDefaultAsync(x => x.CourseInstanceId == instanceId && x.TeacherId == teacherId);
        if (assign is null) return NotFound();
        _db.Remove(assign);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("course-instances/{instanceId:guid}/teachers")]
    public async Task<IActionResult> ListInstanceTeachers(Guid instanceId)
    {
        var instance = await _db.Set<CourseInstance>().AsNoTracking().Include(i => i.Teachers).ThenInclude(t => t.Teacher).FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null) return NotFound();

        var list = instance.Teachers.Select(t => new { teacherId = t.TeacherId, fullName = t.Teacher.FullName, avatarUrl = t.Teacher.AvatarUrl ?? string.Empty });
        return Ok(list);
    }

    public sealed class CourseInstanceRequest { public DateTime? StartDate { get; set; } public DateTime? EndDate { get; set; } }
}
