using EnterpriseOMS.Application.Common.Behaviors;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Auth.Commands.Register;
using EnterpriseOMS.Application.Modules.Products.Commands.CreateProduct;
using EnterpriseOMS.Application.Modules.Products.Commands.UpdateProduct;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Behaviors
{
    public class TransactionBehaviorTests
    {
        /// <summary>Records how the behavior used the unit of work, without needing a database.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int Calls { get; private set; }
            public bool InsideTransaction { get; private set; }
            public CancellationToken ReceivedToken { get; private set; }

            public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
            {
                Calls++;
                ReceivedToken = cancellationToken;
                InsideTransaction = true;

                try
                {
                    return await action();
                }
                finally
                {
                    InsideTransaction = false;
                }
            }
        }

        private sealed class TestCommand : IRequest<string>, ITransactionalCommand
        {
        }

        [Fact]
        public async Task Handle_RunsTheHandlerInsideTheUnitOfWork_AndReturnsItsResult()
        {
            var unitOfWork = new FakeUnitOfWork();
            var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork);
            var handlerRanInsideTransaction = false;

            RequestHandlerDelegate<string> next = _ =>
            {
                handlerRanInsideTransaction = unitOfWork.InsideTransaction;
                return Task.FromResult("done");
            };

            var result = await behavior.Handle(new TestCommand(), next, CancellationToken.None);

            Assert.Equal("done", result);
            Assert.True(handlerRanInsideTransaction);
            Assert.Equal(1, unitOfWork.Calls);
            Assert.False(unitOfWork.InsideTransaction);   // closed again afterwards
        }

        [Fact]
        public async Task Handle_PassesTheCancellationTokenToBothTheUnitOfWorkAndTheHandler()
        {
            var unitOfWork = new FakeUnitOfWork();
            var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork);
            using var source = new CancellationTokenSource();
            var tokenSeenByHandler = CancellationToken.None;

            RequestHandlerDelegate<string> next = token =>
            {
                tokenSeenByHandler = token;
                return Task.FromResult("done");
            };

            await behavior.Handle(new TestCommand(), next, source.Token);

            Assert.Equal(source.Token, unitOfWork.ReceivedToken);
            Assert.Equal(source.Token, tokenSeenByHandler);
        }

        [Fact]
        public async Task Handle_WhenTheHandlerThrows_TheExceptionReachesTheCaller_AndTheTransactionIsLeft()
        {
            var unitOfWork = new FakeUnitOfWork();
            var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork);

            RequestHandlerDelegate<string> next = _ => throw new InvalidOperationException("boom");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => behavior.Handle(new TestCommand(), next, CancellationToken.None));

            Assert.Equal("boom", exception.Message);
            Assert.Equal(1, unitOfWork.Calls);
            Assert.False(unitOfWork.InsideTransaction);
        }

        [Fact]
        public void OnlyMultiStepCommandsCarryTheTransactionalMarker()
        {
            // Register saves in two steps, so it must be atomic. Create and Update are single-save, so
            // wrapping them in an extra transaction would only add overhead.
            Assert.True(typeof(ITransactionalCommand).IsAssignableFrom(typeof(RegisterCommand)));
            Assert.False(typeof(ITransactionalCommand).IsAssignableFrom(typeof(CreateProductCommand)));
            Assert.False(typeof(ITransactionalCommand).IsAssignableFrom(typeof(UpdateProductCommand)));
        }
    }
}
