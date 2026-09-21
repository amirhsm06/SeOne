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

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<BlogPostDto>> GetById(Guid id)
    {
        var dto = await _blogService.GetPublishedPostByIdAsync(id);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // Note: /api/blog/cambridge is handled by frontend (Next.js). Do not implement backend duplication.
}
