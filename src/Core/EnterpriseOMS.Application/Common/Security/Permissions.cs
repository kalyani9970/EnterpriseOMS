using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Security
{
    /// <summary>
    /// Central catalog of permission codes used across [Authorize(Policy = ...)]
    /// attributes. Using constants (not magic strings) means typos become
    /// compile errors instead of silent authorization bypasses.
    /// This grows as each module is added — Auth module seeds the first batch.
    /// </summary>
    public static class Permissions
    {
        public static class Users
        {
            public const string View = "users.view";
            public const string Create = "users.create";
            public const string Update = "users.update";
            public const string Delete = "users.delete";
        }

        public static class Roles
        {
            public const string View = "roles.view";
            public const string ManagePermissions = "roles.manage_permissions";
        }

        // Orders, Inventory, Products, etc. constants will be appended here
        // as each module is built, keeping one authoritative source of truth.
    }
}
