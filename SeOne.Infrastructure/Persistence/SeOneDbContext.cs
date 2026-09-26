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

    public DbSet<Practice> Practices { get; set; }

    public DbSet<PracticeQuestion> PracticeQuestions { get; set; }

    public DbSet<PracticeAttempt> PracticeAttempts { get; set; }

    public DbSet<PracticeAttemptAnswer> PracticeAttemptAnswers { get; set; }

    public DbSet<LearningSession> LearningSessions { get; set; }

    public DbSet<SupportTicket> SupportTickets { get; set; }

    public DbSet<SupportMessage> SupportMessages { get; set; }

    public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }

    public DbSet<NewsletterCampaign> NewsletterCampaigns { get; set; }

    public DbSet<SiteSetting> SiteSettings { get; set; }

    public DbSet<Enrollment> Enrollments { get; set; }

    public DbSet<Lesson> Lessons { get; set; }

    public DbSet<LessonProgress> LessonProgress { get; set; }
    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SeOneDbContext).Assembly);
    }
}