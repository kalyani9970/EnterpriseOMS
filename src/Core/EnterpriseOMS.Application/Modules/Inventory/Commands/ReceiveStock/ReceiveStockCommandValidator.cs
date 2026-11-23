using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.ReceiveStock
{
    public class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
    {
        public ReceiveStockCommandValidator()
        {
            RuleFor(x => x.ProductId).ValidProductId();
            RuleFor(x => x.WarehouseId).ValidWarehouseId();
            RuleFor(x => x.Quantity).ValidQuantity();
            RuleFor(x => x.Reference).ValidReference();
            RuleFor(x => x.Notes).ValidNotes();
        }
    }
}
