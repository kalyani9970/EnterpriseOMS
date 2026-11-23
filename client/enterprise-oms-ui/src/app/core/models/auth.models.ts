/** Mirrors AuthResultDto on the API (ASP.NET Core serialises to camelCase). */
export interface AuthResult {
  userId: number;
  fullName: string;
  email: string;
  role: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

/** The signed-in user, derived from the claims inside the access token. */
export interface AuthUser {
  id: number;
  email: string;
  fullName: string;
  role: string;
  organizationId: number;
  permissions: string[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

/**
 * Sign-up always creates a NEW organization and makes the registrant its administrator.
 * There is deliberately no organization ID or role to choose.
 */
export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  organizationName: string;
}