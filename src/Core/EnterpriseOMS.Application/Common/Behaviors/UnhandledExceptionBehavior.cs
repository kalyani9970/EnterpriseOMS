using EnterpriseOMS.Domain.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseOMS.Application.Common.Behaviors;

/// <summary>
/// Logs genuinely UNEXPECTED exceptions with full context, then rethrows so the global exception
/// middleware in the API layer can still map them to a proper HTTP response.
/// Expected outcomes (bad input, not found, forbidden, a stale edit, a broken business rule) are
/// normal control flow, not errors, so they are not logged as "unhandled".
/// </summary>
public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception ex) when (ex is not Exceptions.ValidationException
                                        and not Exceptions.NotFoundException
                                        and not Exceptions.ForbiddenAccessException
                                        and not Exceptions.ConcurrencyException
                                        and not DomainException)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogError(ex, "Unhandled Exception for Request {Name} {@Request}", requestName, request);
            throw;
        }
    }
}