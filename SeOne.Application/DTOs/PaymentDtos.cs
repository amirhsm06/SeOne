using System;
using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class CreatePaymentIntentRequest
{
    [Required]
    public Guid CourseId { get; set; }
}

public class ConfirmPaymentRequest
{
    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}

public class PaymentIntentDto
{
    public Guid PaymentId { get; set; }

    public string ClientSecret { get; set; } = string.Empty;

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "pending";
}

public class PaymentDto
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "pending";

    public string Provider { get; set; } = string.Empty;

    public string? ProviderPaymentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public Guid? EnrollmentId { get; set; }
}