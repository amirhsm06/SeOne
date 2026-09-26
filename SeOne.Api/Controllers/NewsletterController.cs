using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api")]
public class NewsletterController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    public NewsletterController(SeOneDbContext db) => _db = db;

    [HttpPost("newsletter/subscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request) => await UpsertSubscriber(request, request.Source ?? "website");

    [HttpPost("blog/newsletter/subscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> BlogSubscribe([FromBody] SubscribeRequest request) => await UpsertSubscriber(request, "blog");

    [HttpPost("newsletter/unsubscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> Unsubscribe([FromBody] TokenRequest request)
    {
        var s = await _db.NewsletterSubscribers.FirstOrDefaultAsync(x => x.ManageToken == request.Token); if (s is null) return NotFound(); s.Status = "unsubscribed"; s.UnsubscribedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("newsletter/preferences/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Preferences(string token) { var s = await FindSubscriber(token); return s is null ? NotFound() : Ok(MapSubscriber(s)); }

    [HttpPatch("newsletter/preferences/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdatePreferences(string token, [FromBody] PreferencesRequest request)
    {
        var s = await FindSubscriber(token); if (s is null) return NotFound(); ApplyPreferences(s, request.Preferences); if (s.Status == "unsubscribed") s.Status = "active"; s.UnsubscribedAt = null; await _db.SaveChangesAsync(); return Ok(MapSubscriber(s));
    }

    [HttpGet("newsletter/subscribers")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Subscribers([FromQuery] string? status, [FromQuery] string? source)
    {
        var q = _db.NewsletterSubscribers.AsNoTracking().AsQueryable(); if (!string.IsNullOrWhiteSpace(status)) q=q.Where(s=>s.Status==status); if(!string.IsNullOrWhiteSpace(source)) q=q.Where(s=>s.Source==source); return Ok((await q.OrderByDescending(s=>s.SubscribedAt).ToListAsync()).Select(MapSubscriber));
    }

    [HttpGet("newsletter/subscribers/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Subscriber(Guid id) { var s=await _db.NewsletterSubscribers.FindAsync(id); return s is null?NotFound():Ok(MapSubscriber(s)); }

    [HttpPatch("newsletter/subscribers/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSubscriber(Guid id, [FromBody] UpdateSubscriberRequest request)
    {
        var s=await _db.NewsletterSubscribers.FindAsync(id); if(s is null)return NotFound(); if(request.Name is not null)s.Name=request.Name; if(request.Status is not null)s.Status=Normalize(request.Status,s.Status); if(request.Preferences is not null)ApplyPreferences(s,request.Preferences); s.UnsubscribedAt=s.Status=="unsubscribed"?DateTime.UtcNow:null; await _db.SaveChangesAsync(); return Ok(MapSubscriber(s));
    }

    [HttpDelete("newsletter/subscribers/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSubscriber(Guid id){var s=await _db.NewsletterSubscribers.FindAsync(id);if(s is null)return NotFound();_db.Remove(s);await _db.SaveChangesAsync();return NoContent();}

    [HttpPost("newsletter/subscribers/import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import([FromForm] IFormFile? file)
    {
        if(file is null||file.Length==0)return BadRequest(new{message="CSV file is required."});
        var imported=0;var failed=0;var errors=new List<string>();
        using var reader=new StreamReader(file.OpenReadStream()); string? line; var lineNo=0;
        while((line=await reader.ReadLineAsync()) is not null){lineNo++; if(lineNo==1&&line.Contains("email",StringComparison.OrdinalIgnoreCase))continue; var parts=line.Split(','); if(parts.Length==0||!IsEmail(parts[0])){failed++;errors.Add($"Line {lineNo}: invalid email.");continue;} var email=parts[0].Trim(); var existing=await _db.NewsletterSubscribers.FirstOrDefaultAsync(s=>s.Email==email); if(existing is null){existing=new NewsletterSubscriber{Id=Guid.NewGuid(),Email=email,Name=parts.Length>1?parts[1].Trim():null,Status="active",SubscribedAt=DateTime.UtcNow,Source="import",ManageToken=NewToken()};_db.Add(existing);}else{existing.Status="active";existing.UnsubscribedAt=null;}imported++;}
        await _db.SaveChangesAsync(); return Ok(new{imported,failed,errors});
    }

    [HttpGet("newsletter/campaigns")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Campaigns([FromQuery]string? status,[FromQuery]string? category){var q=_db.NewsletterCampaigns.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(status))q=q.Where(c=>c.Status==status);if(!string.IsNullOrWhiteSpace(category))q=q.Where(c=>c.Category==category);return Ok((await q.OrderByDescending(c=>c.CreatedAt).ToListAsync()).Select(MapCampaign));}

    [HttpPost("newsletter/campaigns")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request){var id=RequireUserId();var c=new NewsletterCampaign{Id=Guid.NewGuid(),Subject=request.Subject.Trim(),Content=request.Content,Category=Normalize(request.Category,"tips"),Status=request.ScheduledAt.HasValue?"scheduled":"draft",ScheduledAt=request.ScheduledAt,CreatedAt=DateTime.UtcNow,CreatedBy=id};_db.Add(c);await _db.SaveChangesAsync();return Ok(MapCampaign(c));}

    [HttpGet("newsletter/campaigns/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Campaign(Guid id){var c=await _db.NewsletterCampaigns.FindAsync(id);return c is null?NotFound():Ok(MapCampaign(c));}

    [HttpPatch("newsletter/campaigns/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCampaign(Guid id,[FromBody] UpdateCampaignRequest request){var c=await _db.NewsletterCampaigns.FindAsync(id);if(c is null)return NotFound();if(c.Status=="sent")return BadRequest(new{message="Sent campaigns cannot be edited."});if(request.Subject is not null)c.Subject=request.Subject.Trim();if(request.Content is not null)c.Content=request.Content;if(request.Category is not null)c.Category=Normalize(request.Category,c.Category);if(request.ScheduledAt.HasValue){c.ScheduledAt=request.ScheduledAt;c.Status="scheduled";}await _db.SaveChangesAsync();return Ok(MapCampaign(c));}

    [HttpPost("newsletter/campaigns/{id:guid}/send")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendCampaign(Guid id){var c=await _db.NewsletterCampaigns.FindAsync(id);if(c is null)return NotFound();if(c.Status=="sent")return Ok(MapCampaign(c));c.RecipientCount=await _db.NewsletterSubscribers.CountAsync(s=>s.Status=="active");c.Status="sent";c.SentAt=DateTime.UtcNow;_db.Update(c);await _db.SaveChangesAsync();return Ok(MapCampaign(c));}

    [HttpDelete("newsletter/campaigns/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCampaign(Guid id){var c=await _db.NewsletterCampaigns.FindAsync(id);if(c is null)return NotFound();_db.Remove(c);await _db.SaveChangesAsync();return NoContent();}

    [HttpGet("newsletter/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Stats(){var total=await _db.NewsletterSubscribers.CountAsync();var active=await _db.NewsletterSubscribers.CountAsync(s=>s.Status=="active");var unsub=await _db.NewsletterSubscribers.CountAsync(s=>s.Status=="unsubscribed");var campaigns=await _db.NewsletterCampaigns.CountAsync();var sent=await _db.NewsletterCampaigns.CountAsync(c=>c.Status=="sent");var scheduled=await _db.NewsletterCampaigns.CountAsync(c=>c.Status=="scheduled");var sentCampaigns=await _db.NewsletterCampaigns.Where(c=>c.Status=="sent"&&c.RecipientCount>0).ToListAsync();var openRate=sentCampaigns.Count==0?0:sentCampaigns.Average(c=>c.OpenCount*100d/c.RecipientCount);var clickRate=sentCampaigns.Count==0?0:sentCampaigns.Average(c=>c.ClickCount*100d/c.RecipientCount);return Ok(new{totalSubscribers=total,activeSubscribers=active,unsubscribedCount=unsub,totalCampaigns=campaigns,sentCampaigns=sent,scheduledCampaigns=scheduled,averageOpenRate=Math.Round(openRate,2),averageClickRate=Math.Round(clickRate,2)});}

    private async Task<IActionResult> UpsertSubscriber(SubscribeRequest request,string source){if(!IsEmail(request.Email))return BadRequest(new{message="A valid email is required."});var email=request.Email.Trim().ToLowerInvariant();var s=await _db.NewsletterSubscribers.FirstOrDefaultAsync(x=>x.Email==email);if(s is null){s=new NewsletterSubscriber{Id=Guid.NewGuid(),Email=email,ManageToken=NewToken(),SubscribedAt=DateTime.UtcNow};_db.Add(s);}s.Name=request.Name?.Trim();s.Source=source;s.Status="active";s.UnsubscribedAt=null;ApplyPreferences(s,request.Preferences);await _db.SaveChangesAsync();return Ok(MapSubscriber(s));}
    private Task<NewsletterSubscriber?> FindSubscriber(string token)=>_db.NewsletterSubscribers.FirstOrDefaultAsync(s=>s.ManageToken==token);
    private static void ApplyPreferences(NewsletterSubscriber s, Preferences? p){if(p is null)return;if(p.Blog.HasValue)s.Blog=p.Blog.Value;if(p.Courses.HasValue)s.Courses=p.Courses.Value;if(p.Promotions.HasValue)s.Promotions=p.Promotions.Value;if(p.Tips.HasValue)s.Tips=p.Tips.Value;}
    private static object MapSubscriber(NewsletterSubscriber s)=>new{id=s.Id,email=s.Email,name=s.Name,status=s.Status,subscribedAt=s.SubscribedAt,unsubscribedAt=s.UnsubscribedAt,preferences=new{blog=s.Blog,courses=s.Courses,promotions=s.Promotions,tips=s.Tips},source=s.Source,manageToken=s.ManageToken};
    private static object MapCampaign(NewsletterCampaign c)=>new{id=c.Id,subject=c.Subject,content=c.Content,category=c.Category,status=c.Status,scheduledAt=c.ScheduledAt,sentAt=c.SentAt,recipientCount=c.RecipientCount,openCount=c.OpenCount,clickCount=c.ClickCount,createdAt=c.CreatedAt,createdBy=c.CreatedBy};
    private static string NewToken()=>Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    private static bool IsEmail(string? email)=>!string.IsNullOrWhiteSpace(email)&&new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);
    private static string Normalize(string? value,string fallback)=>string.IsNullOrWhiteSpace(value)?fallback:value.Trim().ToLowerInvariant();
}

public sealed class SubscribeRequest { public string Email { get; set; }=string.Empty; public string? Name { get; set; } public Preferences? Preferences { get; set; } public string? Source { get; set; } }
public sealed class TokenRequest { public string Token { get; set; }=string.Empty; }
public sealed class PreferencesRequest { public Preferences? Preferences { get; set; } }
public sealed class Preferences { public bool? Blog { get; set; } public bool? Courses { get; set; } public bool? Promotions { get; set; } public bool? Tips { get; set; } }
public sealed class UpdateSubscriberRequest { public string? Name { get; set; } public string? Status { get; set; } public Preferences? Preferences { get; set; } }
public sealed class CreateCampaignRequest { public string Subject { get; set; }=string.Empty; public string Content { get; set; }=string.Empty; public string Category { get; set; }="tips"; public DateTime? ScheduledAt { get; set; } }
public sealed class UpdateCampaignRequest { public string? Subject { get; set; } public string? Content { get; set; } public string? Category { get; set; } public DateTime? ScheduledAt { get; set; } }
