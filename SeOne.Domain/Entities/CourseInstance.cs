namespace SeOne.Domain.Entities;

public class CourseInstance
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Course Course { get; set; } = null!;

    public ICollection<CourseInstanceTeacher> Teachers { get; set; } = new List<CourseInstanceTeacher>();
}