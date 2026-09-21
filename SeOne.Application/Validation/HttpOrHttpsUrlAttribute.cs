using System.ComponentModel.DataAnnotations;
using System;

namespace SeOne.Application.Validation;

public sealed class HttpOrHttpsUrlAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success; // optional

        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return new ValidationResult(ErrorMessage ?? "The URL cannot be empty or whitespace.");

            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    return ValidationResult.Success;
            }

            return new ValidationResult(ErrorMessage ?? "The field must be a valid HTTP or HTTPS URL.");
        }

        return ValidationResult.Success;
    }
}
