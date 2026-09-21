using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/details")]
public class CourseDetailsController : ControllerBase
{
    private readonly ICourseDetailsService _detailsService;

    public CourseDetailsController(ICourseDetailsService detailsService)
    {
        _detailsService = detailsService;
    }

    [HttpGet]
    public async Task<ActionResult<CourseDetailsDto>> GetCourseDetails(Guid courseId)
    {
        Guid? userId = null;
        string userRole = string.Empty;

        if (User.Identity?.IsAuthenticated ?? false)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(idClaim) && Guid.TryParse(idClaim, out var parsed))
                userId = parsed;

            userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        var dto = await _detailsService.GetCourseDetailsAsync(courseId, userId, userRole);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }
}
