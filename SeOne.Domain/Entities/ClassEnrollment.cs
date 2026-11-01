namespace SeOne.Domain.Entities;

public sealed class ClassEnrollment
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public decimal FinalPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string Status { get; set; } = "pending";
    public string? InviteCode { get; set; }
    public Guid? InvitedByStudentId { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public CourseClass CourseClass { get; set; } = null!;
    public User Student { get; set; } = null!;
    public User? InvitedByStudent { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
