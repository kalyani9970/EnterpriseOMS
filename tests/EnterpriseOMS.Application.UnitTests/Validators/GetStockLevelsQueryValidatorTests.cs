using EnterpriseOMS.Application.Modules.Inventory.Queries.GetStockLevels;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class GetStockLevelsQueryValidatorTests
    {
        private readonly GetStockLevelsQueryValidator _validator = new();

        [Fact]
        public void Defaults_AreValid_SoAPlainGetWorks()
        {
            _validator.TestValidate(new GetStockLevelsQuery()).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void PageNumber_BelowOne_Fails(int pageNumber)
        {
            var query = new GetStockLevelsQuery { PageNumber = pageNumber };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.PageNumber);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        [InlineData(1000)]
        public void PageSize_OutOfRange_Fails(int pageSize)
        {
            var query = new GetStockLevelsQuery { PageSize = pageSize };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.PageSize);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void PageSize_AtTheLimits_IsAccepted(int pageSize)
        {
            var query = new GetStockLevelsQuery { PageSize = pageSize };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Search_LongerThan100_Fails()
        {
            var query = new GetStockLevelsQuery { Search = new string('x', 101) };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.Search);
        }

        [Theory]
        [InlineData("password")]
        [InlineData("")]
        [InlineData("quantity; DROP TABLE StockLevels")]
        public void SortBy_NotOnTheWhitelist_Fails(string sortBy)
        {
            var query = new GetStockLevelsQuery { SortBy = sortBy };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.SortBy);
        }

        [Fact]
        public void SortBy_Null_Fails()
        {
            var query = new GetStockLevelsQuery { SortBy = null! };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.SortBy);
        }

        [Theory]
        [InlineData("sku")]
        [InlineData("NAME")]
        [InlineData("Quantity")]
        [InlineData("warehouse")]
        public void SortBy_OnTheWhitelist_IsAccepted_IgnoringCase(string sortBy)
        {
            var query = new GetStockLevelsQuery { SortBy = sortBy };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void TheFilters_AreAllOptional()
        {
            var query = new GetStockLevelsQuery { WarehouseId = 3, ProductId = 5, Search = "lap", LowStockOnly = true };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }
    }
}
