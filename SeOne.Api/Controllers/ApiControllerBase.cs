using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SeOne.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected Guid? CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    protected Guid RequireUserId()
    {
        var id = CurrentUserId;
        if (!id.HasValue)
            throw new UnauthorizedAccessException();
        return id.Value;
    }

    protected static string NormalizeStatus(string? value, string fallback = "active") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

}
