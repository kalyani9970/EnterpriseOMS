using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Authorization
{
    /// <summary>
    /// Usage: [HasPermission(Permissions.Users.View)]
    /// A thin wrapper over [Authorize(Policy = "Permission:users.view")] that keeps
    /// the policy-name convention in one place.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission)
        : base($"{PermissionPolicyProvider.PolicyPrefix}{permission}")
        {
        }
    }
}
