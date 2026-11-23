import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-config';
import { Organization } from '../models/organization.models';
import { OrganizationService } from './organization.service';

describe('OrganizationService', () => {
  let service: OrganizationService;
  let http: HttpTestingController;
  let api: string;

  const organization: Organization = {
    id: 3,
    name: 'Acme Traders',
    code: 'acme-traders',
    isActive: true,
    created: '2026-10-17T09:30:00Z',
    userCount: 1,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    service = TestBed.inject(OrganizationService);
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  it('getCurrent reads the caller\'s own organization (no id in the URL)', () => {
    let result: Organization | undefined;

    service.getCurrent().subscribe((o) => (result = o));

    const request = http.expectOne(`${api}/organizations/current`);
    expect(request.request.method).toBe('GET');
    request.flush(organization);

    expect(result).toEqual(organization);
  });

  it('rename sends only the new name and returns the updated organization', () => {
    let result: Organization | undefined;

    service.rename('Acme Holdings').subscribe((o) => (result = o));

    const request = http.expectOne(`${api}/organizations/current`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Acme Holdings' });
    request.flush({ ...organization, name: 'Acme Holdings' });

    expect(result?.name).toBe('Acme Holdings');
  });
});