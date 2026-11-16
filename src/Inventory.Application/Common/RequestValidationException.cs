using FluentValidation;
using Inventory.Domain.Common;

namespace Inventory.Application.Common;

public sealed class RequestValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Validation.Failed", "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public RequestValidationException(string propertyName, string errorMessage)
        : base("Validation.Failed", errorMessage)
    {
        Errors = new Dictionary<string, string[]>
        {
            [propertyName] = [errorMessage]
        };
    }
}

public static class ValidationExtensions
{
    public static async Task ValidateAndThrowProblemAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).Distinct().ToArray());

        throw new RequestValidationException(errors);
    }
}
