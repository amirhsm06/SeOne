using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(Roles = "Student")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(
        IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create-intent")]
    public async Task<ActionResult<PaymentIntentDto>>
        CreateIntent(
            [FromBody] CreatePaymentIntentRequest request)
    {
        var studentId = GetUserId();

        var result =
            await _paymentService
                .CreatePaymentIntentAsync(
                    studentId,
                    request.CourseId);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "The course is unavailable, free, or you are already enrolled."
            });
        }

        return Ok(result);
    }

    [HttpPost("{paymentId:guid}/confirm")]
    public async Task<ActionResult<PaymentDto>>
        Confirm(
            Guid paymentId,
            [FromBody] ConfirmPaymentRequest request)
    {
        var studentId = GetUserId();

        var result =
            await _paymentService
                .ConfirmPaymentAsync(
                    studentId,
                    paymentId,
                    request.ClientSecret);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Payment confirmation failed."
            });
        }

        return Ok(result);
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<PaymentDto>>>
        GetMyPayments()
    {
        var studentId = GetUserId();

        return Ok(
            await _paymentService
                .GetMyPaymentsAsync(studentId));
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<ActionResult<PaymentDto>>
        GetById(Guid paymentId)
    {
        var studentId = GetUserId();

        var result =
            await _paymentService
                .GetByIdAsync(
                    studentId,
                    paymentId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("manual-class-receipt/{classEnrollmentId:guid}")]
    public async Task<ActionResult<ReceiptUploadResponse>> UploadManualReceipt(
        Guid classEnrollmentId,
        [FromForm] IFormFile receiptFile,
        [FromForm] string note = "")
    {
        var studentId = GetUserId();

        if (receiptFile == null || receiptFile.Length == 0)
        {
            return BadRequest(new { message = "Receipt file is required." });
        }

        try
        {
            using var stream = receiptFile.OpenReadStream();
            var result = await _paymentService
                .UploadReceiptAsync(
                    studentId,
                    classEnrollmentId,
                    receiptFile.FileName,
                    receiptFile.ContentType,
                    stream,
                    note);

            if (result is null)
            {
                return BadRequest(new { message = "Receipt upload failed." });
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("pending-receipts")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<ActionResult<List<PendingReceiptDto>>> GetPendingReceipts()
    {
        return Ok(
            await _paymentService
                .GetPendingReceiptsAsync());
    }

    [HttpPut("{paymentId:guid}/verify-receipt")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VerifyReceiptResponse>> VerifyReceipt(
        Guid paymentId,
        [FromBody] VerifyReceiptRequest request)
    {
        var adminId = GetUserId();

        try
        {
            var result = await _paymentService
                .VerifyReceiptAsync(
                    paymentId,
                    adminId,
                    request.IsApproved,
                    request.RejectionReason);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("manual-payment-settings")]
    [AllowAnonymous]
    public async Task<ActionResult<ManualPaymentSettingsDto>> GetManualPaymentSettings()
    {
        return Ok(
            await _paymentService
                .GetManualPaymentSettingsAsync());
    }

    private Guid GetUserId()
    {
        var claim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                claim,
                out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }
}