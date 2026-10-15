import { AuthResult } from '../models/auth.models';

interface TokenClaims {
  sub: string;
  email: string;
  full_name: string;
  org_id: string;
  role: string;
  permission?: string | string[];
  exp: number;
}

const base64Url = (value: object): string =>
  btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** Builds an unsigned JWT for tests. The client only decodes the payload, so no signature is needed. */
export function makeAccessToken(overrides: Partial<TokenClaims> = {}): string {
  const claims: TokenClaims = {
    sub: '7',
    email: 'jane@example.com',
    full_name: 'Jane Doe',
    org_id: '1',
    role: 'Admin',
    permission: ['users.view', 'roles.view'],
    exp: Math.floor(Date.now() / 1000) + 900,
    ...overrides,
  };

  return `${base64Url({ alg: 'HS256', typ: 'JWT' })}.${base64Url(claims)}.signature`;
}

export function makeAuthResult(accessToken: string, refreshToken: string): AuthResult {
  return {
    userId: 7,
    fullName: 'Jane Doe',
    email: 'jane@example.com',
    role: 'Admin',
    accessToken,
    refreshToken,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
  };
}