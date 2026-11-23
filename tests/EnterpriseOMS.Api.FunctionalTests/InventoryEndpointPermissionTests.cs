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
    /// A permission matrix for the inventory API. The generic endpoint-security tests prove every endpoint is
    /// protected; these prove each one demands the RIGHT permission, so a user who may only VIEW stock can
    /// never receive, issue or adjust it.
    /// </summary>
    public class InventoryEndpointPermissionTests
    {
        [Theory]
        [InlineData(nameof(InventoryController.GetStock), Permissions.Inventory.View)]
        [InlineData(nameof(InventoryController.GetMovements), Permissions.Inventory.View)]
        [InlineData(nameof(InventoryController.Receive), Permissions.Inventory.Adjust)]
        [InlineData(nameof(InventoryController.Issue), Permissions.Inventory.Adjust)]
        [InlineData(nameof(InventoryController.Adjust), Permissions.Inventory.Adjust)]
        public void Endpoint_RequiresExactlyTheExpectedPermission(string action, string permission)
        {
            var method = typeof(InventoryController).GetMethod(action)!;

            var policies = method
                .GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                .Select(attribute => attribute.Policy!)
                .ToList();

            Assert.Equal(new[] { PermissionPolicyProvider.PolicyPrefix + permission }, policies);
        }

        [Fact]
        public void EveryInventoryEndpoint_IsCoveredByTheTableAbove()
        {
            var endpointCount = typeof(InventoryController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Count(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

            // Add an inventory endpoint and this fails until its permission is added to the table.
            Assert.Equal(5, endpointCount);
        }

        [Fact]
        public void OnlyTheThreeStockChangingEndpoints_NeedTheAdjustPermission()
        {
            var adjustEndpoints = typeof(InventoryController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                    .Any(attribute => attribute.Policy == PermissionPolicyProvider.PolicyPrefix + Permissions.Inventory.Adjust))
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToList();

            Assert.Equal(new[] { "Adjust", "Issue", "Receive" }, adjustEndpoints);
        }
    }
}
