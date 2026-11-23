import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { Organization } from '../models/organization.models';

/**
 * Talks to /api/organizations/current. The route has no id on purpose: the server takes the
 * organization from the signed-in user's token, so there is nothing for a caller to tamper with.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** Requires the "organizations.view" permission. */
  getCurrent(): Observable<Organization> {
    return this.http.get<Organization>(`${this.baseUrl}/organizations/current`);
  }

  /** Requires the "organizations.update" permission. Returns the updated organization. */
  rename(name: string): Observable<Organization> {
    return this.http.put<Organization>(`${this.baseUrl}/organizations/current`, { name });
  }
}