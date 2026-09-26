namespace SeOne.Domain.Entities;

public class LearningSession
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid LessonId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public User Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
}
