using EnterpriseOMS.Application.Modules.Products.Queries.GetProductsList;
using FluentValidation.TestHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Validators
{
    public class GetProductsListQueryValidatorTests
    {
        private readonly GetProductsListQueryValidator _validator = new();

        [Fact]
        public void Defaults_AreValid_SoAPlainGetWorks()
        {
            _validator.TestValidate(new GetProductsListQuery()).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void PageNumber_BelowOne_Fails(int pageNumber)
        {
            var query = new GetProductsListQuery { PageNumber = pageNumber };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.PageNumber);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        [InlineData(1000)]
        public void PageSize_OutOfRange_Fails(int pageSize)
        {
            var query = new GetProductsListQuery { PageSize = pageSize };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.PageSize);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void PageSize_AtTheLimits_IsAccepted(int pageSize)
        {
            var query = new GetProductsListQuery { PageSize = pageSize };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Search_Longer_Than100_Fails()
        {
            var query = new GetProductsListQuery { Search = new string('x', 101) };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.Search);
        }

        [Fact]
        public void Category_Longer_Than100_Fails()
        {
            var query = new GetProductsListQuery { Category = new string('x', 101) };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.Category);
        }

        [Theory]
        [InlineData("password")]
        [InlineData("")]
        [InlineData("name; DROP TABLE Products")]
        public void SortBy_NotOnTheWhitelist_Fails(string sortBy)
        {
            var query = new GetProductsListQuery { SortBy = sortBy };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.SortBy);
        }

        [Fact]
        public void SortBy_Null_Fails()
        {
            var query = new GetProductsListQuery { SortBy = null! };

            _validator.TestValidate(query).ShouldHaveValidationErrorFor(x => x.SortBy);
        }

        [Theory]
        [InlineData("sku")]
        [InlineData("NAME")]
        [InlineData("Price")]
        [InlineData("created")]
        public void SortBy_OnTheWhitelist_IsAccepted_IgnoringCase(string sortBy)
        {
            var query = new GetProductsListQuery { SortBy = sortBy };

            _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
        }
    }
}
