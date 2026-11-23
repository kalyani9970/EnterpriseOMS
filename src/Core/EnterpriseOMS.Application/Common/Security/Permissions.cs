namespace EnterpriseOMS.Application.Common.Security;

/// <summary>
/// Central catalog of permission codes used across [HasPermission(...)] attributes.
/// Using constants (not magic strings) means typos become compile errors instead of silent
/// authorization bypasses. The seeder discovers every constant here by reflection, so adding
/// one is enough: the next startup inserts it and grants it to Admin.
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

    public static class Organizations
    {
        public const string View = "organizations.view";
        public const string Update = "organizations.update";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Create = "products.create";
        public const string Update = "products.update";   // also covers deactivating and reactivating
    }

    public static class Warehouses
    {
        public const string View = "warehouses.view";
        public const string Create = "warehouses.create";
        public const string Update = "warehouses.update";   // also covers storage locations, deactivating and reactivating
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string Adjust = "inventory.adjust";   // receiving, issuing and counting stock
    }

    // Orders, Procurement, etc. constants are appended here as each module is built.
}