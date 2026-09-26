using System;
using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class AssignmentDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid? ModuleId { get; set; }

    public string? ModuleTitle { get; set; }

    public Guid TeacherId { get; set; }

    public string TeacherName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Instructions { get; set; }

    public DateTime DueDate { get; set; }

    public decimal MaxPoints { get; set; }

    public List<string> Attachments { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsPublished { get; set; }
}

public class SubmissionDto
{
    public Guid Id { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string? StudentAvatar { get; set; }

    public string Content { get; set; } = string.Empty;

    public List<string> Attachments { get; set; } = new();

    public DateTime SubmittedAt { get; set; }

    public decimal? Grade { get; set; }

    public string? Feedback { get; set; }

    public DateTime? GradedAt { get; set; }

    public Guid? GradedBy { get; set; }

    public string? GradedByName { get; set; }

    public string Status { get; set; } = "draft";
}

public class StudentAssignmentDto
{
    public AssignmentDto Assignment { get; set; } = null!;

    public SubmissionDto? Submission { get; set; }
}

public class CreateAssignmentRequest
{
    [Required]
    public Guid CourseId { get; set; }

    public Guid? ModuleId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Instructions { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    [Range(0.01, 1000000)]
    public decimal MaxPoints { get; set; }

    public List<string>? Attachments { get; set; }

    public bool IsPublished { get; set; }
}

public class UpdateAssignmentRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(5000)]
    public string? Description { get; set; }

    [MaxLength(5000)]
    public string? Instructions { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(0.01, 1000000)]
    public decimal? MaxPoints { get; set; }

    public List<string>? Attachments { get; set; }

    public bool? IsPublished { get; set; }
}

public class CreateSubmissionRequest
{
    [Required]
    [MaxLength(20000)]
    public string Content { get; set; } = string.Empty;

    public List<string>? Attachments { get; set; }
}

public class UpdateSubmissionRequest
{
    [MaxLength(20000)]
    public string? Content { get; set; }

    public List<string>? Attachments { get; set; }
}

public class GradeSubmissionRequest
{
    [Range(0, 1000000)]
    public decimal Grade { get; set; }

    [MaxLength(5000)]
    public string? Feedback { get; set; }
}