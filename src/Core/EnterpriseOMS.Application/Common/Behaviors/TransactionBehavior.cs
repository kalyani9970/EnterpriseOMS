using EnterpriseOMS.Application.Common.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Behaviors
{
    /// <summary>
    /// Wraps every command that carries the ITransactionalCommand marker in a database transaction.
    /// Queries and single-save commands are untouched. In the pipeline it runs AFTER validation, so a
    /// transaction is never opened for a request that was going to be rejected anyway.
    /// The "where ... ITransactionalCommand" constraint is how MediatR/DI know to apply it only to those commands.
    /// </summary>
    public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ITransactionalCommand
    {
        private readonly IUnitOfWork _unitOfWork;

        public TransactionBehavior(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            return _unitOfWork.ExecuteInTransactionAsync(() => next(cancellationToken), cancellationToken);
        }
    }
}
