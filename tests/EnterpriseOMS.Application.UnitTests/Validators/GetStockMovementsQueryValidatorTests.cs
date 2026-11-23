using EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockMovements;
using EnterpriseOMS.Domain.Entities.Inventory;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class GetStockMovementsQueryValidatorTests
    {
        private readonly GetStockMovementsQueryValidator _validator = new();

        [Fact]
        public void Defaults_AreValid_SoAPlainGetWorks()
        {
            _validator.TestValidate(new GetStockMovementsQuery()).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void PageNumber_BelowOne_Fails(int pageNumber)
        {
            var query = new GetStockMovementsQuery { PageNumber = pageNumber };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.PageNumber);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        [InlineData(1000)]
        public void PageSize_OutOfRange_Fails(int pageSize)
        {
            var query = new GetStockMovementsQuery { PageSize = pageSize };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.PageSize);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void PageSize_AtTheLimits_IsAccepted(int pageSize)
        {
            var query = new GetStockMovementsQuery { PageSize = pageSize };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(StockMovementType.Receipt)]
        [InlineData(StockMovementType.Issue)]
        [InlineData(StockMovementType.Adjustment)]
        public void Type_ARealKindOfMovement_IsAccepted(StockMovementType type)
        {
            var query = new GetStockMovementsQuery { Type = type };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(99)]
        public void Type_NotAKindOfMovement_Fails(int type)
        {
            var query = new GetStockMovementsQuery { Type = (StockMovementType)type };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.Type);
        }

        [Fact]
        public void Type_LeftOut_IsAccepted_MeaningEveryKind()
        {
            var query = new GetStockMovementsQuery { Type = null };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void TheFilters_AreAllOptional()
        {
            var query = new GetStockMovementsQuery { ProductId = 5, WarehouseId = 3, Type = StockMovementType.Issue };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }
    }
}
