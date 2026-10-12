using EnterpriseOMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Authorization
{
    /// <summary>
    /// Succeeds when the user's token carries a "permission" claim equal to the required one.
    /// If we never call Succeed, the requirement fails and the framework returns 403.
    /// </summary>
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
        {
            var hasPermission = context.User.HasClaim(
                c => c.Type == CustomClaimTypes.Permission && c.Value == requirement.Permission);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
