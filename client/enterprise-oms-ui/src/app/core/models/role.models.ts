/** Mirrors RoleDto on the API. */
export interface Role {
  id: number;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  permissions: string[];
}