/**
 * Permission codes, mirroring Permissions.cs on the API. The server enforces them;
 * the client uses them only to hide UI the user could not use anyway.
 * Keep this file in step with the server's catalog as modules are added.
 */
export const Permissions = {
  Users: {
    View: 'users.view',
    Create: 'users.create',
    Update: 'users.update',
    Delete: 'users.delete',
  },
  Roles: {
    View: 'roles.view',
    ManagePermissions: 'roles.manage_permissions',
  },
} as const;