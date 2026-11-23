using EnterpriseOMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseOMS.Application.Common.Behaviors;

/// <summary>Logs every request name + the user issuing it, before it hits the handler.</summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId;

        _logger.LogInformation(
            "Handling {RequestName} for UserId: {UserId}",
            requestName, userId);

        // Passing the token down means a cancelled HTTP request stops the work in the handler too.
        var response = await next(cancellationToken);

        _logger.LogInformation("Handled {RequestName}", requestName);

        return response;
    }
}