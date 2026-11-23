using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Domain.Entities.Tenancy;
using EnterpriseOMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseOMS.Application.IntegrationTests.Persistence;

/// <summary>
/// IUnitOfWork is what the TransactionBehavior uses to make multi-step commands all-or-nothing.
/// These tests use real database transactions: what was saved inside a failed block must vanish.
/// </summary>
public class UnitOfWorkTests : IntegrationTestBase
{
    private async Task<bool> OrganizationExistsAsync(string code)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(o => o.Code == code);
    }

    [Fact]
    public async Task Commits_WhenTheActionSucceeds()
    {
        using (var scope = App.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                db.Organizations.Add(Organization.Create("Committed", "committed"));
                await db.SaveChangesAsync();
                return true;
            }, CancellationToken.None);
        }

        Assert.True(await OrganizationExistsAsync("committed"));
    }

    [Fact]
    public async Task RollsBack_EverythingSavedInside_WhenTheActionThrows()
    {
        using (var scope = App.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync<bool>(async () =>
                {
                    db.Organizations.Add(Organization.Create("Doomed", "doomed"));
                    await db.SaveChangesAsync();

                    throw new InvalidOperationException("something failed afterwards");
                }, CancellationToken.None));
        }

        Assert.False(await OrganizationExistsAsync("doomed"));
    }

    [Fact]
    public async Task ReturnsWhatTheActionReturns()
    {
        using var scope = App.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var result = await unitOfWork.ExecuteInTransactionAsync(
            () => Task.FromResult("the result"),
            CancellationToken.None);

        Assert.Equal("the result", result);
    }

    [Fact]
    public async Task TheActionRunsInsideATransaction_WhichIsClosedAfterwards()
    {
        using var scope = App.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Null(db.Database.CurrentTransaction);

        var wasInsideTransaction = await unitOfWork.ExecuteInTransactionAsync(
            () => Task.FromResult(db.Database.CurrentTransaction is not null),
            CancellationToken.None);

        Assert.True(wasInsideTransaction);
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ANestedCall_JoinsTheOuterTransaction_AndRollsBackWithIt()
    {
        using (var scope = App.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync<bool>(async () =>
                {
                    var outerTransactionId = db.Database.CurrentTransaction!.TransactionId;

                    await unitOfWork.ExecuteInTransactionAsync<bool>(async () =>
                    {
                        // The inner call must join the outer transaction, not start its own.
                        Assert.Equal(
                            outerTransactionId,
                            db.Database.CurrentTransaction!.TransactionId);

                        db.Organizations.Add(
                            Organization.Create("Inner Work", "inner-work"));

                        await db.SaveChangesAsync();

                        return true;
                    }, CancellationToken.None);

                    throw new InvalidOperationException(
                        "the OUTER step failed after the inner one finished");
                }, CancellationToken.None));
        }

        // The inner block "succeeded", but because it belonged to the outer transaction
        // it was undone too.
        Assert.False(await OrganizationExistsAsync("inner-work"));
    }

    [Fact]
    public async Task SignUp_IsAllOrNothing_WhenALaterStepFails()
    {
        using (var scope = App.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var register = scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>();

            // This is what the TransactionBehavior does around RegisterCommand:
            // the handler makes two separate saves (organization + user, then the refresh token).
            // If anything fails afterwards, BOTH saves must be undone.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync<AuthResultDto>(async () =>
                {
                    await register.Handle(
                        new RegisterCommand
                        {
                            FullName = "Ann Lee",
                            Email = "ann@atomic.test",
                            Password = "Passw0rd1",
                            OrganizationName = "Atomic Co"
                        },
                        CancellationToken.None);

                    throw new InvalidOperationException(
                        "a later step failed");
                }, CancellationToken.None));
        }

        ActAnonymously();

        using var verifyScope = App.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(
            await verifyDb.Organizations.AnyAsync(o => o.Code == "atomic-co"));

        Assert.False(
            await verifyDb.Users.AnyAsync(
                u => u.NormalizedEmail == "ANN@ATOMIC.TEST"));

        Assert.Equal(
            0,
            await verifyDb.RefreshTokens.CountAsync());
    }
}