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

    public DbSet<WishlistItem> WishlistItems { get; set; }

    public DbSet<Review> Reviews { get; set; }

    public DbSet<ReviewHelpful> ReviewHelpful { get; set; }

    public DbSet<Assignment> Assignments { get; set; }

    public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    public DbSet<Conversation> Conversations { get; set; }

    public DbSet<ConversationParticipant> ConversationParticipants { get; set; }

    public DbSet<Message> Messages { get; set; }

    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SeOneDbContext).Assembly);
    }
}