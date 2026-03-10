using FluentValidation;
using MediatR;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Common.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var errors = failures
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        if (errors.Count > 0)
        {
            throw new AppException(
                "validation_error",
                "Validation failed",
                400,
                "One or more validation errors occurred.",
                errors);
        }

        return await next();
    }
}
