using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/students/me/dashboard")]
[Authorize(Roles = "Student")]
public class StudentDashboardController : ControllerBase
{
    private readonly IStudentDashboardService _dashboardService;

    public StudentDashboardController(IStudentDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<StudentDashboardDto>> GetMyDashboard()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var dto = await _dashboardService.GetMyDashboardAsync(studentId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }
}
