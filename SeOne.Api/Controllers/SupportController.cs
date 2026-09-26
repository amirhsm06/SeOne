using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api")]
public class SupportController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly IWebHostEnvironment _environment;
    public SupportController(SeOneDbContext db, IWebHostEnvironment environment) { _db = db; _environment = environment; }

    [HttpPost("contact")]
    [AllowAnonymous]
    public async Task<IActionResult> Contact([FromBody] ContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "Name, email, subject, and message are required." });
        Guid? userId = null;
        if (CurrentUserId.HasValue) userId = CurrentUserId.Value;
        var ticket = new SupportTicket { Id = Guid.NewGuid(), UserId = userId, UserEmail = request.Email.Trim(), UserName = request.Name?.Trim(), Subject = request.Subject?.Trim() ?? "Contact form", Category = "general", Priority = "medium", Status = "open", Description = request.Message.Trim(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.SupportTickets.Add(ticket); await _db.SaveChangesAsync();
        return Ok(new { id = ticket.Id, name = ticket.UserName ?? request.Name, email = ticket.UserEmail, subject = ticket.Subject, message = ticket.Description, createdAt = ticket.CreatedAt });
    }

    [HttpGet("support/tickets")]
    [Authorize]
    public async Task<IActionResult> Tickets([FromQuery] string? status, [FromQuery] string? category)
    {
        var userId = RequireUserId(); var isSupport = IsSupportUser();
        var query = _db.SupportTickets.AsNoTracking().Include(t => t.User).Include(t => t.AssignedTo).Include(t => t.Messages).AsQueryable();
        if (!isSupport) query = query.Where(t => t.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(t => t.Category == category);
        var result = await query.OrderByDescending(t => t.UpdatedAt).ToListAsync();
        return Ok(result.Select(t => MapTicket(t, isSupport)));
    }

    [HttpPost("support/tickets")]
    [Authorize]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var userId = RequireUserId(); var user = await _db.Users.FindAsync(userId); if (user is null) return NotFound();
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket { Id = Guid.NewGuid(), UserId = userId, UserEmail = user.Email ?? string.Empty, UserName = user.FullName, Subject = request.Subject.Trim(), Category = Normalize(request.Category, "general"), Priority = Normalize(request.Priority, "medium"), Status = "open", Description = request.Description.Trim(), Attachments = request.Attachments ?? new(), CreatedAt = now, UpdatedAt = now };
        _db.SupportTickets.Add(ticket); await _db.SaveChangesAsync(); return Ok(MapTicket(ticket, false));
    }

    [HttpGet("support/tickets/{ticketId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTicket(Guid ticketId)
    {
        var userId = RequireUserId(); var support = IsSupportUser();
        var ticket = await _db.SupportTickets.Include(t => t.User).Include(t => t.AssignedTo).Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return NotFound(); if (!support && ticket.UserId != userId) return Forbid(); return Ok(MapTicket(ticket, support));
    }

    [HttpPatch("support/tickets/{ticketId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateTicket(Guid ticketId, [FromBody] UpdateTicketRequest request)
    {
        var ticket = await GetOwnedTicket(ticketId); if (ticket is null) return NotFound();
        if (!IsSupportUser() && ticket.UserId != RequireUserId()) return Forbid();
        if (request.Subject is not null) ticket.Subject = request.Subject.Trim();
        if (request.Category is not null) ticket.Category = Normalize(request.Category, ticket.Category);
        if (request.Priority is not null) ticket.Priority = Normalize(request.Priority, ticket.Priority);
        ticket.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return Ok(MapTicket(ticket, IsSupportUser()));
    }

    [HttpPost("support/tickets/{ticketId:guid}/close")]
    [Authorize]
    public Task<IActionResult> Close(Guid ticketId) => SetTicketStatus(ticketId, "closed");

    [HttpPost("support/tickets/{ticketId:guid}/reopen")]
    [Authorize]
    public Task<IActionResult> Reopen(Guid ticketId) => SetTicketStatus(ticketId, "open");

    [HttpPost("support/tickets/{ticketId:guid}/messages")]
    [Authorize]
    public async Task<IActionResult> AddMessage(Guid ticketId, [FromBody] CreateSupportMessageRequest request)
    {
        var ticket = await _db.SupportTickets.Include(t => t.User).Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == ticketId); if (ticket is null) return NotFound();
        var userId = RequireUserId(); var support = IsSupportUser(); if (!support && ticket.UserId != userId) return Forbid();
        var user = await _db.Users.FindAsync(userId); if (user is null) return NotFound();
        var message = new SupportMessage { Id = Guid.NewGuid(), TicketId = ticketId, UserId = userId, UserName = user.FullName, IsFromSupport = support, Content = request.Content.Trim(), Attachments = request.Attachments ?? new(), CreatedAt = DateTime.UtcNow, IsInternal = support && request.IsInternal };
        _db.SupportMessages.Add(message); ticket.UpdatedAt = DateTime.UtcNow; if (support && !message.IsInternal) ticket.Status = "in_progress"; await _db.SaveChangesAsync(); return Ok(MapMessage(message));
    }

    [HttpGet("support/tickets/{ticketId:guid}/messages")]
    [Authorize]
    public async Task<IActionResult> Messages(Guid ticketId)
    {
        var ticket = await _db.SupportTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId); if (ticket is null) return NotFound();
        var userId = RequireUserId(); var support = IsSupportUser(); if (!support && ticket.UserId != userId) return Forbid();
        var query = _db.SupportMessages.AsNoTracking().Where(m => m.TicketId == ticketId); if (!support) query = query.Where(m => !m.IsInternal);
        var messages = await query.OrderBy(m => m.CreatedAt).ToListAsync(); return Ok(messages.Select(MapMessage));
    }

    [HttpPost("support/tickets/{ticketId:guid}/messages/{messageId:guid}/upload")]
    [Authorize]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(Guid ticketId, Guid messageId, IFormFile? attachment)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId); if (ticket is null) return NotFound();
        var support = IsSupportUser(); if (!support && ticket.UserId != RequireUserId()) return Forbid();
        var message = await _db.SupportMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.TicketId == ticketId); if (message is null) return NotFound();
        if (attachment is null || attachment.Length == 0) return BadRequest();
        var dir = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "support"); Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(attachment.FileName); var name = $"{Guid.NewGuid():N}{ext}"; var path = Path.Combine(dir, name); await using (var stream = System.IO.File.Create(path)) await attachment.CopyToAsync(stream);
        var url = $"/uploads/support/{name}"; message.Attachments.Add(url); await _db.SaveChangesAsync(); return Ok(new { fileUrl = url });
    }

    [HttpGet("support/faq")]
    [AllowAnonymous]
    public IActionResult Faq([FromQuery] string? category)
    {
        var faq = new[] {
            new { id="account-1", category="account", question="How do I change my password?", answer="Open your account settings and use Change Password.", order=1 },
            new { id="booking-1", category="booking", question="How do I book a teacher?", answer="Choose a teacher, select an available slot, and create a booking.", order=2 },
            new { id="payment-1", category="billing", question="How are paid courses purchased?", answer="Paid courses require a payment intent before enrollment is created.", order=3 },
            new { id="learning-1", category="academic", question="How is lesson progress saved?", answer="Lesson progress is saved through the learning/progress endpoints while you study.", order=4 }
        }.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category)) faq = faq.Where(x => x.category.Equals(category, StringComparison.OrdinalIgnoreCase));
        return Ok(faq);
    }

    private bool IsSupportUser() => User.IsInRole("Admin") || User.IsInRole("Teacher");
    private async Task<SupportTicket?> GetOwnedTicket(Guid id) => await _db.SupportTickets.Include(t => t.User).Include(t => t.AssignedTo).Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);
    private async Task<IActionResult> SetTicketStatus(Guid ticketId, string status)
    {
        var ticket = await GetOwnedTicket(ticketId); if (ticket is null) return NotFound(); var userId = RequireUserId(); if (!IsSupportUser() && ticket.UserId != userId) return Forbid(); ticket.Status = status; ticket.UpdatedAt = DateTime.UtcNow; ticket.ResolvedAt = status == "resolved" || status == "closed" ? DateTime.UtcNow : null; await _db.SaveChangesAsync(); return Ok(MapTicket(ticket, IsSupportUser()));
    }
    private static object MapTicket(SupportTicket t, bool support) => new { id=t.Id,userId=t.UserId,userName=t.UserName ?? t.User?.FullName,userEmail=t.UserEmail,subject=t.Subject,category=t.Category,priority=t.Priority,status=t.Status,description=t.Description,attachments=t.Attachments,createdAt=t.CreatedAt,updatedAt=t.UpdatedAt,resolvedAt=t.ResolvedAt,assignedTo=t.AssignedToId,assignedToName=t.AssignedTo?.FullName,messages=t.Messages.Where(m => support || !m.IsInternal).OrderBy(m => m.CreatedAt).Select(MapMessage) };
    private static object MapMessage(SupportMessage m) => new { id=m.Id,ticketId=m.TicketId,userId=m.UserId,userName=m.UserName,isFromSupport=m.IsFromSupport,content=m.Content,attachments=m.Attachments,createdAt=m.CreatedAt,isInternal=m.IsInternal };
    private static string Normalize(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
}

public sealed class ContactRequest { public string Name { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string Subject { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; }
public sealed class CreateTicketRequest { public string Subject { get; set; } = string.Empty; public string Category { get; set; } = "general"; public string? Priority { get; set; } public string Description { get; set; } = string.Empty; public List<string>? Attachments { get; set; } }
public sealed class UpdateTicketRequest { public string? Subject { get; set; } public string? Category { get; set; } public string? Priority { get; set; } }
public sealed class CreateSupportMessageRequest { public string Content { get; set; } = string.Empty; public List<string>? Attachments { get; set; } public bool IsInternal { get; set; } }
