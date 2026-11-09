namespace SeOne.Domain.Entities;

public sealed class ClassSlotTeacherAssignment
{
    public Guid Id { get; set; }
    public Guid ClassSlotId { get; set; }
    public Guid TeacherId { get; set; }
    public string Status { get; set; } = "confirmed";
    public DateTime CreatedAt { get; set; }

    public CourseClass ClassSlot { get; set; } = null!;
    public User Teacher { get; set; } = null!;
}
