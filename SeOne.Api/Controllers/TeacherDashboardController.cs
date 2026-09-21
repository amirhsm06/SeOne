using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teachers/me/dashboard")]
[Authorize(Roles = "Teacher")]
public class TeacherDashboardController : ControllerBase
{
    private readonly ITeacherDashboardService _dashboardService;

    public TeacherDashboardController(ITeacherDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<TeacherDashboardDto>> GetMyDashboard()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var dto = await _dashboardService.GetMyDashboardAsync(teacherId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }
}
