/** Mirrors OrganizationDto on the API. */
export interface Organization {
  id: number;
  name: string;
  /** Stable, URL-safe identifier such as "acme-traders". Never changes. */
  code: string;
  isActive: boolean;
  /** ISO date-time string. */
  created: string;
  userCount: number;
}