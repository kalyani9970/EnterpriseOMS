using FluentValidation;
using MediatR;

namespace EnterpriseOMS.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: runs BEFORE the handler for every request that
/// has a registered FluentValidation validator. If validation fails, it throws OUR
/// ValidationException (EnterpriseOMS.Application.Common.Exceptions), which the API's
/// ExceptionHandlingMiddleware maps to HTTP 400. Handlers never validate manually.

/// .NET concept: both FluentValidation and our Application layer define a type called
/// ValidationException. Because "using FluentValidation;" is imported here, the bare name
/// would bind to FluentValidation's type, so we qualify ours through "Exceptions.".
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new Exceptions.ValidationException(failures);
        }

        return await next();
    }
}