import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-config';
import { Address, WarehouseListQuery } from '../models/warehouse.models';
import { WarehousesService } from './warehouses.service';

describe('WarehousesService', () => {
  let service: WarehousesService;
  let http: HttpTestingController;
  let api: string;

  const baseQuery: WarehouseListQuery = { pageNumber: 1, pageSize: 20, sortBy: 'name', descending: false };
  const address: Address = { line1: '1 Test Street', line2: null, city: 'Utrecht', region: null, postalCode: null, countryCode: 'NL' };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    service = TestBed.inject(WarehousesService);
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  const listRequest = () => http.expectOne((request) => request.url === `${api}/warehouses`);

  it('list always sends paging and sorting', () => {
    service.list(baseQuery).subscribe();

    const request = listRequest();
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('sortBy')).toBe('name');
    expect(request.request.params.get('descending')).toBe('false');
    request.flush({});
  });

  it('list leaves out filters that are not set', () => {
    service.list({ ...baseQuery, search: '   ', isActive: null }).subscribe();

    const request = listRequest();
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('isActive')).toBe(false);
    request.flush({});
  });

  it('list sends the filters that are set, trimming the search text', () => {
    service.list({ ...baseQuery, search: '  rotter ', isActive: true, sortBy: 'city', descending: true }).subscribe();

    const request = listRequest();
    expect(request.request.params.get('search')).toBe('rotter');
    expect(request.request.params.get('isActive')).toBe('true');
    expect(request.request.params.get('sortBy')).toBe('city');
    expect(request.request.params.get('descending')).toBe('true');
    request.flush({});
  });

  it('list can ask for inactive warehouses specifically (false is a real value, not "unset")', () => {
    service.list({ ...baseQuery, isActive: false }).subscribe();

    const request = listRequest();
    expect(request.request.params.get('isActive')).toBe('false');
    request.flush({});
  });

  it('getById and create use the expected URLs and bodies', () => {
    service.getById(7).subscribe();
    http.expectOne(`${api}/warehouses/7`).flush({});

    service.create({ code: 'WH-1', name: 'One', address }).subscribe();
    const create = http.expectOne(`${api}/warehouses`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ code: 'WH-1', name: 'One', address });
    create.flush({});
  });

  it('update puts to the warehouse URL with the version in the body', () => {
    service.update(7, { version: 'v1', name: 'Renamed', address }).subscribe();

    const request = http.expectOne(`${api}/warehouses/7`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ version: 'v1', name: 'Renamed', address });
    request.flush({});
  });

  it('deactivate and activate post the version the caller last saw', () => {
    service.deactivate(7, 'v1').subscribe();
    const deactivate = http.expectOne(`${api}/warehouses/7/deactivate`);
    expect(deactivate.request.method).toBe('POST');
    expect(deactivate.request.body).toEqual({ version: 'v1' });
    deactivate.flush({});

    service.activate(7, 'v2').subscribe();
    const activate = http.expectOne(`${api}/warehouses/7/activate`);
    expect(activate.request.body).toEqual({ version: 'v2' });
    activate.flush({});
  });

  it('getLocations asks for everything by default, or only active ones on request', () => {
    service.getLocations(7).subscribe();
    const all = http.expectOne((r) => r.url === `${api}/warehouses/7/locations`);
    expect(all.request.params.get('activeOnly')).toBe('false');
    all.flush([]);

    service.getLocations(7, true).subscribe();
    const active = http.expectOne((r) => r.url === `${api}/warehouses/7/locations`);
    expect(active.request.params.get('activeOnly')).toBe('true');
    active.flush([]);
  });

  it('createLocation posts to the warehouse, with the parent in the body', () => {
    service.createLocation(7, { parentId: 3, code: 'A-01', name: null, type: 'Aisle' }).subscribe();

    const request = http.expectOne(`${api}/warehouses/7/locations`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ parentId: 3, code: 'A-01', name: null, type: 'Aisle' });
    request.flush({});
  });

  it('updateLocation, deactivateLocation and activateLocation go through the warehouse URL', () => {
    service.updateLocation(7, 9, { version: 'v1', name: 'Renamed' }).subscribe();
    const update = http.expectOne(`${api}/warehouses/7/locations/9`);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ version: 'v1', name: 'Renamed' });
    update.flush({});

    service.deactivateLocation(7, 9, 'v2').subscribe();
    const deactivate = http.expectOne(`${api}/warehouses/7/locations/9/deactivate`);
    expect(deactivate.request.body).toEqual({ version: 'v2' });
    deactivate.flush({});

    service.activateLocation(7, 9, 'v3').subscribe();
    const activate = http.expectOne(`${api}/warehouses/7/locations/9/activate`);
    expect(activate.request.body).toEqual({ version: 'v3' });
    activate.flush({});
  });
});