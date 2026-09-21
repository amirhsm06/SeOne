using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence;

public class SeOneDbContext
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public SeOneDbContext(
        DbContextOptions<SeOneDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings { get; set; }

    public DbSet<BlogPost> BlogPosts { get; set; }

    public DbSet<OtpCode> OtpCodes { get; set; }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SeOneDbContext).Assembly);
    }
}