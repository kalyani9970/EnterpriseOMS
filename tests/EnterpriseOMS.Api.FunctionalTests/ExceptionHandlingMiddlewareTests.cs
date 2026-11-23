using EnterpriseOMS.Api.Middleware;
using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Domain.Common.Exceptions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EnterpriseOMS.Api.FunctionalTests
{
    /// <summary>
    /// Runs the real middleware against a fake HttpContext: for every kind of exception, which status code
    /// and which JSON does the client get? These tests would have caught the Phase 1 bug where a validation
    /// failure came back as 500 instead of 400.
    /// </summary>
    public class ExceptionHandlingMiddlewareTests
    {
        private sealed class FakeEnvironment : IHostEnvironment
        {
            public FakeEnvironment(string environmentName) => EnvironmentName = environmentName;

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; } = "Tests";
            public string ContentRootPath { get; set; } = "/";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        private sealed record Result(int Status, string ContentType, JsonElement Body);

        private static async Task<Result> RunAsync(Exception toThrow, string environment = "Production")
        {
            var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
            context.Response.Body = new MemoryStream();

            RequestDelegate next = _ => throw toThrow;
            var middleware = new ExceptionHandlingMiddleware(
                next,
                NullLogger<ExceptionHandlingMiddleware>.Instance,
                new FakeEnvironment(environment));

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(context.Response.Body);

            return new Result(context.Response.StatusCode, context.Response.ContentType!, document.RootElement.Clone());
        }

        [Fact]
        public async Task ValidationException_Becomes400_WithTheFieldErrors()
        {
            var exception = new ValidationException(new[]
            {
            new ValidationFailure("Password", "Password is too weak."),
            new ValidationFailure("Email", "This email is already registered.")
        });

            var result = await RunAsync(exception);

            Assert.Equal(400, result.Status);
            Assert.Equal("One or more validation errors occurred.", result.Body.GetProperty("title").GetString());
            Assert.Equal("Password is too weak.", result.Body.GetProperty("errors").GetProperty("Password")[0].GetString());
            Assert.Equal("This email is already registered.", result.Body.GetProperty("errors").GetProperty("Email")[0].GetString());
        }

        [Fact]
        public async Task NotFoundException_Becomes404_WithItsMessage()
        {
            var result = await RunAsync(new NotFoundException("Product", 5));

            Assert.Equal(404, result.Status);
            Assert.Equal("Entity \"Product\" (5) was not found.", result.Body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task ForbiddenAccessException_Becomes403()
        {
            var result = await RunAsync(new ForbiddenAccessException());

            Assert.Equal(403, result.Status);
            Assert.Equal("Forbidden.", result.Body.GetProperty("title").GetString());
        }

        [Fact]
        public async Task DomainException_Becomes422_WithTheBusinessRuleMessage()
        {
            var result = await RunAsync(new DomainException("Unit price cannot be negative."));

            Assert.Equal(422, result.Status);
            Assert.Equal("Business rule violation.", result.Body.GetProperty("title").GetString());
            Assert.Equal("Unit price cannot be negative.", result.Body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task ConcurrencyException_Becomes409_WithAReloadHint()
        {
            var result = await RunAsync(new ConcurrencyException("Product", 5));

            Assert.Equal(409, result.Status);
            Assert.Equal("Concurrency conflict.", result.Body.GetProperty("title").GetString());
            Assert.Equal("Product (5) was changed by someone else. Reload it and try again.", result.Body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task DbUpdateConcurrencyException_Becomes409_Too()
        {
            var result = await RunAsync(new DbUpdateConcurrencyException());

            Assert.Equal(409, result.Status);
            Assert.Equal("The record was modified by another user. Reload and try again.", result.Body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task UnknownException_Becomes500_AndLeaksNothingInProduction()
        {
            var result = await RunAsync(new InvalidOperationException("secret connection string"));

            Assert.Equal(500, result.Status);
            Assert.Equal("An unexpected error occurred.", result.Body.GetProperty("title").GetString());
            Assert.False(result.Body.TryGetProperty("detail", out _));
            Assert.DoesNotContain("secret connection string", result.Body.GetRawText());
        }

        [Fact]
        public async Task UnknownException_IncludesDetailsInDevelopment_ToHelpTheDeveloper()
        {
            var result = await RunAsync(new InvalidOperationException("something broke"), environment: "Development");

            Assert.Equal(500, result.Status);
            Assert.Contains("something broke", result.Body.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task EveryErrorResponse_IsProblemJson_AndCarriesATraceId()
        {
            var result = await RunAsync(new NotFoundException("Product", 1));

            Assert.Equal("application/problem+json", result.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(result.Body.GetProperty("traceId").GetString()));
        }

        [Fact]
        public async Task WhenNothingGoesWrong_TheRequestPassesThroughUntouched()
        {
            var context = new DefaultHttpContext();
            var nextCalled = false;
            RequestDelegate next = _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            await new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance, new FakeEnvironment("Production"))
                .InvokeAsync(context);

            Assert.True(nextCalled);
            Assert.Equal(200, context.Response.StatusCode);
        }
    }
}
