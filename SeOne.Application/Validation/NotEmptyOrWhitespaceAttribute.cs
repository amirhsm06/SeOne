using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.Validation;

/// <summary>
/// Validation attribute that rejects null, empty or whitespace-only strings.
/// </summary>
public sealed class NotEmptyOrWhitespaceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return new ValidationResult(ErrorMessage ?? "The field cannot be empty or whitespace.");

            return ValidationResult.Success;
        }

        // If not a string, let [Required] handle null checks or other validators
        return ValidationResult.Success;
    }
}
