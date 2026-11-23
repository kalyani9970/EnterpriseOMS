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
    /// A permission matrix for the warehouses API. The generic endpoint-security tests prove every endpoint
    /// is protected; these prove each one demands the RIGHT permission, so a "view" user can never change
    /// a warehouse or its locations.
    /// </summary>
    public class WarehouseEndpointPermissionTests
    {
        [Theory]
        [InlineData(nameof(WarehousesController.GetList), Permissions.Warehouses.View)]
        [InlineData(nameof(WarehousesController.GetById), Permissions.Warehouses.View)]
        [InlineData(nameof(WarehousesController.GetLocations), Permissions.Warehouses.View)]
        [InlineData(nameof(WarehousesController.Create), Permissions.Warehouses.Create)]
        [InlineData(nameof(WarehousesController.Update), Permissions.Warehouses.Update)]
        [InlineData(nameof(WarehousesController.Deactivate), Permissions.Warehouses.Update)]
        [InlineData(nameof(WarehousesController.Activate), Permissions.Warehouses.Update)]
        [InlineData(nameof(WarehousesController.CreateLocation), Permissions.Warehouses.Update)]
        [InlineData(nameof(WarehousesController.UpdateLocation), Permissions.Warehouses.Update)]
        [InlineData(nameof(WarehousesController.DeactivateLocation), Permissions.Warehouses.Update)]
        [InlineData(nameof(WarehousesController.ActivateLocation), Permissions.Warehouses.Update)]
        public void Endpoint_RequiresExactlyTheExpectedPermission(string action, string permission)
        {
            var method = typeof(WarehousesController).GetMethod(action)!;

            var policies = method
                .GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                .Select(attribute => attribute.Policy!)
                .ToList();

            Assert.Equal(new[] { PermissionPolicyProvider.PolicyPrefix + permission }, policies);
        }

        [Fact]
        public void EveryWarehouseEndpoint_IsCoveredByTheTableAbove()
        {
            var endpointCount = typeof(WarehousesController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Count(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

            // Add a warehouse endpoint and this fails until its permission is added to the table.
            Assert.Equal(11, endpointCount);
        }
    }
}
