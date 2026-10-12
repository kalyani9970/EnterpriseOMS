using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace EnterpriseOMS.Api.Middleware
{
    /// <summary>
    /// Single place that maps exceptions to HTTP responses, so controllers contain no
    /// try/catch. Unexpected errors never leak internals outside Development.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(exception, "Exception after the response had already started.");
                return;
            }

            ProblemDetails problem = exception switch
            {
                ValidationException validation => new ValidationProblemDetails(validation.Errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                },
                NotFoundException notFound => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Resource not found.",
                    Detail = notFound.Message
                },
                ForbiddenAccessException forbidden => new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden.",
                    Detail = forbidden.Message
                },
                DomainException domain => new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Business rule violation.",
                    Detail = domain.Message
                },
                DbUpdateConcurrencyException => new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Concurrency conflict.",
                    Detail = "The record was modified by another user. Reload and try again."
                },
                _ => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = _environment.IsDevelopment() ? exception.ToString() : null
                }
            };

            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

            if (problem.Status >= 500)
                _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", problem.Extensions["traceId"]);
            else
                _logger.LogWarning("Request failed with {Status}: {Message}", problem.Status, exception.Message);

            context.Response.StatusCode = problem.Status!.Value;

            // Passing problem.GetType() makes sure derived properties (ValidationProblemDetails.Errors)
            // are serialized, not just the base ProblemDetails fields.
            await context.Response.WriteAsJsonAsync(
                problem,
                problem.GetType(),
                (JsonSerializerOptions?)null,
                "application/problem+json");
        }
    }
}
