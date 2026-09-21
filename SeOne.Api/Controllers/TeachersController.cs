using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teachers")]
public class TeachersController : ControllerBase
{
    private readonly ITeacherService _teacherService;
    private readonly ICourseService _courseService;

    public TeachersController(ITeacherService teacherService, ICourseService courseService)
    {
        _teacherService = teacherService;
        _courseService = courseService;
    }

    [HttpGet("me/courses/{courseId:guid}/students")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<List<TeacherCourseStudentDto>>> GetCourseStudents(Guid courseId, [FromServices] ITeacherCourseStudentService studentService)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var list = await studentService.GetStudentsForCourseAsync(teacherId, courseId);

        if (list == null || list.Count == 0)
        {
            // Either course not found/owned or no students. Need to distinguish: service returns empty list for not-owned course as well.
            // Verify ownership to decide 404 vs empty list
            var courseOwned = await _courseService.GetByIdAsync(courseId, teacherId, "Teacher") != null;

            if (!courseOwned)
                return NotFound();
        }

        return Ok(list);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<TeacherDto>>> GetAll()
    {
        var list = await _teacherService.GetAllTeachersAsync();
        return Ok(list);
    }

    [HttpGet("{teacherId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeacherDto>> GetById(Guid teacherId)
    {
        var dto = await _teacherService.GetTeacherByIdAsync(teacherId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherDto>> GetMyProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var dto = await _teacherService.GetMyProfileAsync(teacherId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }

    [HttpPut("me")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherDto>> UpdateMyProfile([FromBody] UpdateTeacherProfileRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var updated = await _teacherService.UpdateMyProfileAsync(teacherId, request.Avatar, request.TeachingLanguage, request.Subject, request.Level, request.Bio);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }
}
