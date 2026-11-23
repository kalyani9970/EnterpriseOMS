using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Inventory.Commands.IssueStock
{
    public class IssueStockCommandValidator : AbstractValidator<IssueStockCommand>
    {
        public IssueStockCommandValidator()
        {
            RuleFor(x => x.ProductId).ValidProductId();
            RuleFor(x => x.WarehouseId).ValidWarehouseId();
            RuleFor(x => x.Quantity).ValidQuantity();
            RuleFor(x => x.Reference).ValidReference();
            RuleFor(x => x.Notes).ValidNotes();
        }
    }
}
