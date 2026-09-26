using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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