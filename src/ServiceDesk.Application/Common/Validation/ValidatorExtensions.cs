using FluentValidation;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Validation;

// AGENTS.md §5 (handler step 2), §11: every failure goes into Error.FieldErrors under its property name; the Code is
// Validation.<Property> of the first failure.
public static class ValidatorExtensions
{
    public static async Task<Result> ValidateToResultAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var validation = await validator.ValidateAsync(instance, ct);
        if (validation.IsValid)
        {
            return Result.Success();
        }

        return Error.Validation(validation.Errors
            .Select(failure => (Field: failure.PropertyName, Message: failure.ErrorMessage))
            .ToList());
    }
}
