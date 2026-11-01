namespace SeOne.Domain.Entities;

public sealed class ClassInvite
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid CreatedById { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public CourseClass CourseClass { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
