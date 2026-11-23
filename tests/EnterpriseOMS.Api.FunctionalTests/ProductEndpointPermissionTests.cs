using EnterpriseOMS.Api.Controllers;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Api.FunctionalTests
{
    /// <summary>
    /// A permission matrix for the products API. The generic endpoint-security tests prove every endpoint
    /// is protected; these prove each one demands the RIGHT permission, so a "view" user can never create.
    /// </summary>
    public class ProductEndpointPermissionTests
    {
        [Theory]
        [InlineData(nameof(ProductsController.GetList), Permissions.Products.View)]
        [InlineData(nameof(ProductsController.GetCategories), Permissions.Products.View)]
        [InlineData(nameof(ProductsController.GetById), Permissions.Products.View)]
        [InlineData(nameof(ProductsController.Create), Permissions.Products.Create)]
        [InlineData(nameof(ProductsController.Update), Permissions.Products.Update)]
        [InlineData(nameof(ProductsController.Deactivate), Permissions.Products.Update)]
        [InlineData(nameof(ProductsController.Activate), Permissions.Products.Update)]
        public void Endpoint_RequiresExactlyTheExpectedPermission(string action, string permission)
        {
            var method = typeof(ProductsController).GetMethod(action)!;

            var policies = method
                .GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                .Select(attribute => attribute.Policy!)
                .ToList();

            Assert.Equal(new[] { PermissionPolicyProvider.PolicyPrefix + permission }, policies);
        }

        [Fact]
        public void EveryProductEndpoint_IsCoveredByTheTableAbove()
        {
            var endpointCount = typeof(ProductsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Count(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

            // Add a product endpoint and this fails until its permission is added to the table.
            Assert.Equal(7, endpointCount);
        }
    }
}
