using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.Interfaces;
using SeOne.Application.DTOs;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/blog")]
public class BlogController : ControllerBase
{
    private readonly IBlogService _blogService;

    public BlogController(IBlogService blogService)
    {
        _blogService = blogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<BlogPostDto>>> GetAll([FromQuery(Name = "lang")] string lang = "en")
    {
        var list = await _blogService.GetPublishedPostsAsync(lang);
        return Ok(list);
    }

    [HttpGet("{idOrSlug}")]
    [AllowAnonymous]
    public async Task<ActionResult<BlogPostDto>> GetByIdOrSlug(
        string idOrSlug,
        [FromQuery(Name = "lang")] string lang = "en")
    {
        var dto = Guid.TryParse(idOrSlug, out var id)
            ? await _blogService.GetPublishedPostByIdAsync(id)
            : await _blogService.GetPublishedPostBySlugAsync(idOrSlug, lang);

        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // Note: /api/blog/cambridge is handled by frontend (Next.js). Do not implement backend duplication.
}
