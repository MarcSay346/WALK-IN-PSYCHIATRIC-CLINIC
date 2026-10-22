using System;
using System.Collections.Generic;

namespace ClinicApi.Services;

/// <summary>
/// Thrown when input fails validation. The API endpoints turn it into a 400 Bad Request
/// whose body lists every problem: { "errors": { "fieldName": "message" } }.
/// </summary>
public class ApiValidationException : Exception
{
    public IReadOnlyDictionary<string, string> Errors { get; }

    public ApiValidationException(IDictionary<string, string> errors)
        : base("Validation failed: " + string.Join(" ", errors.Values))
    {
        Errors = new Dictionary<string, string>(errors);
    }

    public ApiValidationException(string field, string message)
        : this(new Dictionary<string, string> { [field] = message })
    {
    }
}

/// <summary>Shared validation rules: required fields, field types/ranges and length limits.</summary>
public static class InputRules
{
    public static void RequiredText(IDictionary<string, string> errors, string field, string label, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = $"{label} is required.";
        else if (value.Trim().Length > maxLength)
            errors[field] = $"{label} must be {maxLength} characters or fewer.";
    }

    public static void IntInRange(IDictionary<string, string> errors, string field, string label, int? value, int min, int max)
    {
        if (value is null)
            errors[field] = $"{label} is required.";
        else if (value < min || value > max)
            errors[field] = $"{label} must be between {min} and {max}.";
    }

    public static void ThrowIfInvalid(IDictionary<string, string> errors)
    {
        if (errors.Count > 0)
            throw new ApiValidationException(errors);
    }
}
