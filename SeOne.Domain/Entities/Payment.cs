using System;
using SeOne.Domain.Enums;

namespace SeOne.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? ClassEnrollmentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public PaymentStatus Status { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? ProviderPaymentId { get; set; }

    public string ClientSecret { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public string? ReceiptObjectPath { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public Guid? VerifiedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public Course Course { get; set; } = null!;

    public ClassEnrollment? ClassEnrollment { get; set; }

    public User Student { get; set; } = null!;
}