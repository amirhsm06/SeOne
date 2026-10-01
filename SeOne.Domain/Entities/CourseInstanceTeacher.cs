namespace SeOne.Domain.Entities;

public class CourseInstanceTeacher
{
    public Guid Id { get; set; }

    public Guid CourseInstanceId { get; set; }

    public Guid TeacherId { get; set; }

    public DateTime CreatedAt { get; set; }

    public CourseInstance CourseInstance { get; set; } = null!;

    public User Teacher { get; set; } = null!;
}