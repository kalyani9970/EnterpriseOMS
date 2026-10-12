using EnterpriseOMS.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Security
{
    public class PermissionAuthorizationTests
    {
        private static ClaimsPrincipal CreateUser(params string[] permissions)
        {
            var claims = permissions.Select(p => new Claim("permission", p));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        }

        private static async Task<bool> EvaluateAsync(ClaimsPrincipal user, string requiredPermission)
        {
            var requirement = new PermissionRequirement(requiredPermission);
            var context = new AuthorizationHandlerContext(new IAuthorizationRequirement[] { requirement }, user, resource: null);

            await new PermissionAuthorizationHandler().HandleAsync(context);

            return context.HasSucceeded;
        }

        [Fact]
        public async Task Handler_UserHasPermission_Succeeds()
        {
            var user = CreateUser("users.view", "roles.view");

            Assert.True(await EvaluateAsync(user, "roles.view"));
        }

        [Fact]
        public async Task Handler_UserLacksPermission_DoesNotSucceed()
        {
            var user = CreateUser("users.view");

            Assert.False(await EvaluateAsync(user, "roles.view"));
        }

        [Fact]
        public async Task Handler_UserWithNoPermissions_DoesNotSucceed()
        {
            Assert.False(await EvaluateAsync(CreateUser(), "users.view"));
        }

        [Fact]
        public async Task Handler_PermissionMatchIsExact_NotPrefixBased()
        {
            var user = CreateUser("users.view_all");

            Assert.False(await EvaluateAsync(user, "users.view"));
        }

        [Fact]
        public async Task PolicyProvider_PermissionPrefix_BuildsPolicyWithRequirement()
        {
            var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

            var policy = await provider.GetPolicyAsync("Permission:users.view");

            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements,
                r => r is PermissionRequirement p && p.Permission == "users.view");
            Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        }

        [Fact]
        public async Task PolicyProvider_UnknownPolicyName_FallsBackToBaseBehaviour()
        {
            var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

            var policy = await provider.GetPolicyAsync("SomethingElse");

            Assert.Null(policy);
        }

        [Fact]
        public void HasPermissionAttribute_MapsToPermissionPolicyName()
        {
            var attribute = new HasPermissionAttribute("orders.create");

            Assert.Equal("Permission:orders.create", attribute.Policy);
        }
    }
}
