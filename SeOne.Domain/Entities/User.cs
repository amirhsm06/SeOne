using Microsoft.AspNetCore.Identity;
using SeOne.Domain.Enums;

namespace SeOne.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }
}