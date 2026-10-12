using EnterpriseOMS.Application.Common.Behaviors;
using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using MediatR;

namespace EnterpriseOMS.Application.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    private static RegisterCommand ValidCommand() => new()
    {
        FullName = "Jane Doe",
        Email = "jane@example.com",
        Password = "Passw0rd1",
        OrganizationId = 1
    };

    private static ValidationBehavior<RegisterCommand, AuthResultDto> CreateBehavior(
        params FluentValidation.IValidator<RegisterCommand>[] validators) => new(validators);

    [Fact]
    public async Task NoValidatorsRegistered_CallsNext()
    {
        var behavior = CreateBehavior();
        var nextCalled = false;

        // In current MediatR the delegate receives a CancellationToken, so the lambda takes one parameter.
        RequestHandlerDelegate<AuthResultDto> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(new AuthResultDto { Email = "x@example.com" });
        };

        var result = await behavior.Handle(ValidCommand(), next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("x@example.com", result.Email);
    }

    [Fact]
    public async Task ValidRequest_CallsNext()
    {
        var behavior = CreateBehavior(new RegisterCommandValidator());
        var nextCalled = false;

        RequestHandlerDelegate<AuthResultDto> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(new AuthResultDto());
        };

        await behavior.Handle(ValidCommand(), next, CancellationToken.None);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvalidRequest_ThrowsValidationException_AndNeverCallsNext()
    {
        var behavior = CreateBehavior(new RegisterCommandValidator());
        var command = ValidCommand();
        command.Password = "weak";
        var nextCalled = false;

        RequestHandlerDelegate<AuthResultDto> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(new AuthResultDto());
        };

        // ValidationException here is EnterpriseOMS.Application.Common.Exceptions.ValidationException,
        // the only one in scope, because FluentValidation's namespace is not imported.
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(command, next, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.True(exception.Errors.ContainsKey("Password"));
    }
}