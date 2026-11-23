using EnterpriseOMS.Domain.Entities.Inventory;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.AdjustStock
{
    public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
    {
        public AdjustStockCommandValidator()
        {
            RuleFor(x => x.ProductId).ValidProductId();
            RuleFor(x => x.WarehouseId).ValidWarehouseId();
            RuleFor(x => x.CountedQuantity).ValidCountedQuantity();

            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("A reason is required for a stock adjustment.")
                .MaximumLength(StockMovement.NotesMaxLength)
                .WithMessage($"Reason must be {StockMovement.NotesMaxLength} characters or fewer.");
        }
    }
}
