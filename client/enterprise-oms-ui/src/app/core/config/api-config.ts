import { InjectionToken } from '@angular/core';

/**
 * Base URL of the Enterprise OMS API. Injected wherever HTTP calls are made,
 * so tests (and later, per-environment builds) can override it in one place.
 * https://localhost:7190 is the https profile in the API's launchSettings.json.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => 'https://localhost:7190/api',
});