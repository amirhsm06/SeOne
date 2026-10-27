using System.ComponentModel.DataAnnotations;

namespace SeOne.Api.DTOs;

public sealed class CreateTeacherWithdrawalRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(80)]
    public string Iban { get; set; } = string.Empty;
}

public sealed class DecideTeacherWithdrawalRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}