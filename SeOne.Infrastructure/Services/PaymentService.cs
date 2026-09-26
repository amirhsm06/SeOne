using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly SeOneDbContext _context;
    private readonly IPaymentGateway _paymentGateway;
    private readonly INotificationService _notificationService;

    public PaymentService(
        SeOneDbContext context,
        IPaymentGateway paymentGateway,
        INotificationService notificationService)
    {
        _context = context;
        _paymentGateway = paymentGateway;
        _notificationService = notificationService;
    }

    public async Task<PaymentIntentDto?> CreatePaymentIntentAsync(
        Guid studentId,
        Guid courseId)
    {
        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(x =>
                x.Id == courseId &&
                x.IsPublished);

        if (course is null)
        {
            return null;
        }

        if (course.Price <= 0)
        {
            return null;
        }

        var alreadyEnrolled =
            await _context.Set<Enrollment>()
                .AnyAsync(x =>
                    x.StudentId == studentId &&
                    x.CourseId == courseId);

        if (alreadyEnrolled)
        {
            return null;
        }

        var existingPending =
            await _context.Set<Payment>()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.CourseId == courseId &&
                    x.Status == PaymentStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

        if (existingPending is not null)
        {
            return new PaymentIntentDto
            {
                PaymentId = existingPending.Id,
                ClientSecret =
                    existingPending.ClientSecret,
                CourseId = course.Id,
                CourseTitle = course.Title,
                Amount = existingPending.Amount,
                Currency = existingPending.Currency,
                Status = "pending"
            };
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            Amount = course.Price,
            Currency = "USD",
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Provider = "Development"
        };

        var gatewayIntent =
            await _paymentGateway.CreateIntentAsync(
                payment.Amount,
                payment.Currency,
                payment.Id,
                payment.CourseId);

        payment.ClientSecret =
            gatewayIntent.ClientSecret;

        payment.ProviderPaymentId =
            gatewayIntent.ProviderPaymentId;

        payment.Provider =
            gatewayIntent.Provider;

        _context.Set<Payment>().Add(payment);

        await _context.SaveChangesAsync();

        return new PaymentIntentDto
        {
            PaymentId = payment.Id,
            ClientSecret = payment.ClientSecret,
            CourseId = course.Id,
            CourseTitle = course.Title,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = "pending"
        };
    }

    public async Task<PaymentDto?> ConfirmPaymentAsync(
        Guid studentId,
        Guid paymentId,
        string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var payment =
            await _context.Set<Payment>()
                .Include(x => x.Course)
                .FirstOrDefaultAsync(x =>
                    x.Id == paymentId &&
                    x.StudentId == studentId);

        if (payment is null)
        {
            return null;
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            return await BuildPaymentDtoAsync(
                payment,
                studentId);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return null;
        }

        if (!string.Equals(
                payment.ClientSecret,
                clientSecret,
                StringComparison.Ordinal))
        {
            return null;
        }

        var confirmed =
            await _paymentGateway
                .ConfirmAsync(clientSecret);

        if (!confirmed)
        {
            payment.Status =
                PaymentStatus.Failed;

            payment.FailureReason =
                "Payment gateway rejected the payment.";

            await _context.SaveChangesAsync();

            return null;
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        payment.Status =
            PaymentStatus.Succeeded;

        payment.PaidAt =
            DateTime.UtcNow;

        var enrollment =
            await _context.Set<Enrollment>()
                .FirstOrDefaultAsync(x =>
                    x.StudentId == studentId &&
                    x.CourseId == payment.CourseId);

        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CourseId = payment.CourseId,
                EnrolledAt = DateTime.UtcNow
            };

            _context.Set<Enrollment>()
                .Add(enrollment);
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateAsync(
            studentId,
            "payment",
            "Payment successful",
            $"Your payment for \"{payment.Course.Title}\" was successful.",
            new Dictionary<string, object?>
            {
                ["paymentId"] = payment.Id,
                ["courseId"] = payment.CourseId,
                ["amount"] = payment.Amount,
                ["currency"] = payment.Currency
            });

        await transaction.CommitAsync();

        return await BuildPaymentDtoAsync(
            payment,
            studentId);
    }

    public async Task<PaymentDto?> GetByIdAsync(
        Guid studentId,
        Guid paymentId)
    {
        var payment =
            await _context.Set<Payment>()
                .Include(x => x.Course)
                .FirstOrDefaultAsync(x =>
                    x.Id == paymentId &&
                    x.StudentId == studentId);

        if (payment is null)
        {
            return null;
        }

        return await BuildPaymentDtoAsync(
            payment,
            studentId);
    }

    public async Task<List<PaymentDto>> GetMyPaymentsAsync(
        Guid studentId)
    {
        var payments =
            await _context.Set<Payment>()
                .AsNoTracking()
                .Include(x => x.Course)
                .Where(x =>
                    x.StudentId == studentId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

        var result =
            new List<PaymentDto>();

        foreach (var payment in payments)
        {
            result.Add(
                await BuildPaymentDtoAsync(
                    payment,
                    studentId));
        }

        return result;
    }

    private async Task<PaymentDto>
        BuildPaymentDtoAsync(
            Payment payment,
            Guid studentId)
    {
        var enrollmentId =
            await _context.Set<Enrollment>()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.CourseId == payment.CourseId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();

        return new PaymentDto
        {
            Id = payment.Id,
            StudentId = payment.StudentId,
            CourseId = payment.CourseId,
            CourseTitle = payment.Course.Title,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status
                .ToString()
                .ToLowerInvariant(),
            Provider = payment.Provider,
            ProviderPaymentId =
                payment.ProviderPaymentId,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            EnrollmentId = enrollmentId
        };
    }
}