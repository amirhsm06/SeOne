using System;
using SeOne.Domain.Enums;

namespace SeOne.Domain.Entities;

public class AssignmentSubmission
{
    public Guid Id { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid StudentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public List<string> Attachments { get; set; } = new();

    public DateTime SubmittedAt { get; set; }

    public decimal? Grade { get; set; }

    public string? Feedback { get; set; }

    public DateTime? GradedAt { get; set; }

    public Guid? GradedById { get; set; }

    public AssignmentSubmissionStatus Status { get; set; }

    public Assignment Assignment { get; set; } = null!;

    public User Student { get; set; } = null!;

    public User? GradedBy { get; set; }
}