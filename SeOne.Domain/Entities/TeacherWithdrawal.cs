namespace SeOne.Domain.Entities;

public class TeacherWithdrawal
{
    public Guid Id { get; set; }

    public Guid TeacherId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "IRR";

    // Only Admin-authorized withdrawal endpoints return this field.
    public string Iban { get; set; } = string.Empty;

    // pending | approved | rejected. Approval is not proof of bank transfer.
    public string Status { get; set; } = "pending";

    public DateTime RequestedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public Guid? DecidedById { get; set; }

    public User Teacher { get; set; } = null!;

    public User? DecidedBy { get; set; }
}