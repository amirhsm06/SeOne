using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private const string DisabledMessage = "Payment processing is not enabled yet. Configure a real payment gateway and receipt storage before accepting payments.";
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) => _paymentService = paymentService;

    [HttpPost]
    [HttpPost("create-intent")]
    [Authorize(Roles = "Student")]
    public IActionResult CreateIntent([FromBody] CreatePaymentIntentRequest request)
        => PaymentUnavailable();

    [HttpPost("{paymentId:guid}/confirm")]
    [Authorize(Roles = "Student")]
    public IActionResult Confirm(Guid paymentId, [FromBody] ConfirmPaymentRequest request)
        => PaymentUnavailable();

    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<List<PaymentDto>>> GetMyPayments()
        => Ok(await _paymentService.GetMyPaymentsAsync(GetUserId()));

    [HttpGet("{paymentId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<PaymentDto>> GetById(Guid paymentId)
    {
        var result = await _paymentService.GetByIdAsync(GetUserId(), paymentId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("manual-class-receipt/{classEnrollmentId:guid}")]
    [Authorize(Roles = "Student")]
    [Consumes("multipart/form-data")]
    public IActionResult UploadManualReceipt(Guid classEnrollmentId, IFormFile? receiptFile, [FromForm] string note = "")
        => PaymentUnavailable();

    [HttpPost("{paymentId:guid}/receipt")]
    [Authorize(Roles = "Student")]
    [Consumes("multipart/form-data")]
    public IActionResult UploadPaymentReceipt(Guid paymentId, IFormFile? receiptFile, [FromForm] string note = "")
        => PaymentUnavailable();

    [HttpGet("pending-receipts")]
    [Authorize(Roles = "Admin,Teacher")]
    public IActionResult GetPendingReceipts() => PaymentUnavailable();

    [HttpPut("{paymentId:guid}/verify-receipt")]
    [Authorize(Roles = "Admin")]
    public IActionResult VerifyReceipt(Guid paymentId, [FromBody] VerifyReceiptRequest request)
        => PaymentUnavailable();

    [HttpGet("manual-payment-settings")]
    [HttpGet("~/api/payment/settings")]
    [AllowAnonymous]
    public async Task<ActionResult<ManualPaymentSettingsDto>> GetManualPaymentSettings()
    {
        var settings = await _paymentService.GetManualPaymentSettingsAsync();
        // Receipt storage has not been implemented/configured, so the public API must not advertise it as enabled.
        settings.IsEnabled = false;
        settings.WhatsAppContactNumber = string.Empty;
        settings.WhatsAppLink = string.Empty;
        return Ok(settings);
    }

    private IActionResult PaymentUnavailable()
        => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Payments are not configured",
            Detail = DisabledMessage
        });

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException();
        return userId;
    }
}
