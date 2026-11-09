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

    public async Task<ReceiptUploadResponse?> UploadReceiptAsync(
        Guid studentId,
        Guid classEnrollmentId,
        string fileName,
        string mimeType,
        Stream fileStream,
        string note)
    {
        var enrollment = await _context.Set<ClassEnrollment>()
            .Include(x => x.CourseClass)
            .FirstOrDefaultAsync(x =>
                x.Id == classEnrollmentId &&
                x.StudentId == studentId);

        if (enrollment is null)
        {
            throw new ArgumentException("Class enrollment not found or unauthorized.");
        }

        var settings = await _context.Set<ManualPaymentSettings>()
            .FirstOrDefaultAsync();

        if (settings == null || !settings.IsEnabled)
        {
            throw new ArgumentException("Manual payments are not enabled.");
        }

        // Validate file size
        if (fileStream.Length > settings.MaxReceiptSizeBytes)
        {
            throw new ArgumentException(
                $"Receipt file exceeds maximum size of {settings.MaxReceiptSizeBytes} bytes.");
        }

        // Validate MIME type
        var allowedTypes = settings.AllowedMimeTypes.Split(',');
        if (!allowedTypes.Contains(mimeType))
        {
            throw new ArgumentException(
                $"File type '{mimeType}' is not allowed.");
        }

        // Create payment record with receipt
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = enrollment.CourseClass.CourseId,  // Link to class's course
            ClassEnrollmentId = classEnrollmentId,
            Amount = enrollment.FinalPrice,
            Currency = enrollment.CourseClass.Currency,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Provider = "Manual"
        };

        // Save file and store path
        // Note: File storage would typically use IMessagingFileStorageService or similar
        // For now, storing the file name as a placeholder
        var storedFileName = $"receipts/{Guid.NewGuid()}_{fileName}";
        payment.ReceiptObjectPath = storedFileName;

        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();

        return new ReceiptUploadResponse
        {
            PaymentId = payment.Id,
            ClassEnrollmentId = classEnrollmentId,
            ReceiptObjectPath = storedFileName,
            Status = "pending",
            UploadedAt = payment.CreatedAt,
            Message = "Receipt uploaded successfully. Awaiting admin verification."
        };
    }

    public async Task<ReceiptUploadResponse?> UploadReceiptForPaymentAsync(
        Guid studentId,
        Guid paymentId,
        string fileName,
        string mimeType,
        Stream fileStream,
        string note)
    {
        var payment = await _context.Set<Payment>()
            .FirstOrDefaultAsync(x =>
                x.Id == paymentId &&
                x.StudentId == studentId);

        if (payment is null)
        {
            throw new ArgumentException("Payment not found or unauthorized.");
        }

        if (payment.Status != PaymentStatus.Pending || payment.ReceiptObjectPath is not null)
        {
            throw new ArgumentException("Payment is not eligible for receipt upload.");
        }

        var settings = await _context.Set<ManualPaymentSettings>()
            .FirstOrDefaultAsync();

        if (settings is null || !settings.IsEnabled)
        {
            throw new ArgumentException("Manual payments are not enabled.");
        }

        if (fileStream.Length > settings.MaxReceiptSizeBytes)
        {
            throw new ArgumentException(
                $"Receipt file exceeds maximum size of {settings.MaxReceiptSizeBytes} bytes.");
        }

        var allowedTypes = settings.AllowedMimeTypes.Split(',');
        if (!allowedTypes.Contains(mimeType))
        {
            throw new ArgumentException(
                $"File type '{mimeType}' is not allowed.");
        }

        var storedFileName = $"receipts/{Guid.NewGuid()}_{fileName}";
        payment.ReceiptObjectPath = storedFileName;
        await _context.SaveChangesAsync();

        return new ReceiptUploadResponse
        {
            PaymentId = payment.Id,
            ClassEnrollmentId = payment.ClassEnrollmentId,
            ReceiptObjectPath = storedFileName,
            Status = "pending",
            UploadedAt = payment.CreatedAt,
            Message = "Receipt uploaded successfully. Awaiting admin verification."
        };
    }

    public async Task<List<PendingReceiptDto>> GetPendingReceiptsAsync()
    {
        var pendingPayments = await _context.Set<Payment>()
            .Where(x =>
                x.Status == PaymentStatus.Pending &&
                x.ReceiptObjectPath != null)
            .Include(x => x.Student)
            .Include(x => x.ClassEnrollment)
                .ThenInclude(x => x.CourseClass)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return pendingPayments.Select(p => new PendingReceiptDto
        {
            PaymentId = p.Id,
            ClassEnrollmentId = p.ClassEnrollmentId ?? Guid.Empty,
            StudentId = p.StudentId,
            StudentName = p.Student?.FullName ?? "Unknown",
            StudentEmail = p.Student?.Email ?? "",
            Amount = p.Amount,
            ClassName = p.ClassEnrollment?.CourseClass?.Title ?? "Unknown Class",
            ReceiptObjectPath = p.ReceiptObjectPath,
            UploadedAt = p.CreatedAt,
            ReceiptDownloadUrl = $"/api/payments/{p.Id}/receipt-download",
            PreviewUrl = $"/api/payments/{p.Id}/receipt-preview"
        }).ToList();
    }

    public async Task<VerifyReceiptResponse?> VerifyReceiptAsync(
        Guid paymentId,
        Guid adminId,
        bool isApproved,
        string? rejectionReason)
    {
        var payment = await _context.Set<Payment>()
            .FirstOrDefaultAsync(x => x.Id == paymentId);

        if (payment is null || payment.Status != PaymentStatus.Pending)
        {
            return null;
        }

        if (!isApproved && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejection reason is required when rejecting a receipt.");
        }

        payment.VerifiedAt = DateTime.UtcNow;
        payment.VerifiedBy = adminId;

        if (isApproved)
        {
            payment.Status = PaymentStatus.Verified;
            payment.PaidAt = DateTime.UtcNow;

            // Update enrollment status if needed
            if (payment.ClassEnrollmentId.HasValue)
            {
                var enrollment = await _context.Set<ClassEnrollment>()
                    .FirstOrDefaultAsync(x =>
                        x.Id == payment.ClassEnrollmentId.Value);
                if (enrollment != null)
                {
                    enrollment.Status = "confirmed";
                }
            }
            else if (!await _context.Set<Enrollment>().AnyAsync(x => x.StudentId == payment.StudentId && x.CourseId == payment.CourseId))
            {
                _context.Set<Enrollment>().Add(new Enrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = payment.StudentId,
                    CourseId = payment.CourseId,
                    EnrolledAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            payment.Status = PaymentStatus.Rejected;
            payment.RejectionReason = rejectionReason;
        }

        await _context.SaveChangesAsync();

        return new VerifyReceiptResponse
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString().ToLowerInvariant(),
            VerifiedAt = payment.VerifiedAt ?? DateTime.UtcNow,
            Message = isApproved ? "Receipt approved. Payment confirmed." : "Receipt rejected."
        };
    }

    public async Task<ManualPaymentSettingsDto> GetManualPaymentSettingsAsync()
    {
        var settings = await _context.Set<ManualPaymentSettings>()
            .FirstOrDefaultAsync();

        if (settings is null)
        {
            // Return default settings
            settings = new ManualPaymentSettings
            {
                IsEnabled = true,
                MaxReceiptSizeBytes = 5242880,  // 5MB
                AllowedMimeTypes = "image/png,image/jpeg,image/gif,application/pdf",
                WhatsAppContactNumber = "+1234567890",
                UpdatedAt = DateTime.UtcNow
            };
        }

        return new ManualPaymentSettingsDto
        {
            IsEnabled = settings.IsEnabled,
            MaxReceiptSizeBytes = settings.MaxReceiptSizeBytes,
            AllowedMimeTypes = settings.AllowedMimeTypes,
            WhatsAppContactNumber = settings.WhatsAppContactNumber,
            WhatsAppLink = $"https://wa.me/{settings.WhatsAppContactNumber.Replace("+", "").Replace("-", "").Replace(" ", "")}"
        };
    }
}