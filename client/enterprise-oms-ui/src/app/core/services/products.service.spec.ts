import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-config';
import { ProductListQuery } from '../models/product.models';
import { ProductsService } from './products.service';

describe('ProductsService', () => {
  let service: ProductsService;
  let http: HttpTestingController;
  let api: string;

  const baseQuery: ProductListQuery = { pageNumber: 1, pageSize: 20, sortBy: 'name', descending: false };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    service = TestBed.inject(ProductsService);
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  const listRequest = () => http.expectOne((request) => request.url === `${api}/products`);

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
    service.list({ ...baseQuery, search: '   ', category: '', isActive: null }).subscribe();

    const params = listRequest();
    expect(params.request.params.has('search')).toBe(false);
    expect(params.request.params.has('category')).toBe(false);
    expect(params.request.params.has('isActive')).toBe(false);
    params.flush({});
  });

  it('list sends the filters that are set, trimming the search text', () => {
    service
      .list({ ...baseQuery, search: '  wireless ', category: 'Accessories', isActive: true, sortBy: 'price', descending: true })
      .subscribe();

    const request = listRequest();
    expect(request.request.params.get('search')).toBe('wireless');
    expect(request.request.params.get('category')).toBe('Accessories');
    expect(request.request.params.get('isActive')).toBe('true');
    expect(request.request.params.get('sortBy')).toBe('price');
    expect(request.request.params.get('descending')).toBe('true');
    request.flush({});
  });

  it('list can ask for inactive products specifically (false is a real value, not "unset")', () => {
    service.list({ ...baseQuery, isActive: false }).subscribe();

    const request = listRequest();
    expect(request.request.params.get('isActive')).toBe('false');
    request.flush({});
  });

  it('getCategories, getById and create use the expected URLs', () => {
    service.getCategories().subscribe();
    http.expectOne(`${api}/products/categories`).flush([]);

    service.getById(7).subscribe();
    http.expectOne(`${api}/products/7`).flush({});

    const body = {
      sku: 'A-1',
      name: 'Item',
      description: null,
      category: null,
      unitOfMeasure: 'Piece' as const,
      unitPrice: 1,
      costPrice: 1,
      reorderLevel: 0,
    };
    service.create(body).subscribe();
    const create = http.expectOne(`${api}/products`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual(body);
    create.flush({});
  });

  it('update puts to the product URL with the version in the body', () => {
    service
      .update(7, {
        version: 'v1',
        name: 'Item',
        description: null,
        category: null,
        unitOfMeasure: 'Box',
        unitPrice: 2,
        costPrice: 1,
        reorderLevel: 3,
      })
      .subscribe();

    const request = http.expectOne(`${api}/products/7`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.version).toBe('v1');
    request.flush({});
  });

  it('deactivate and activate post the version the caller last saw', () => {
    service.deactivate(7, 'v1').subscribe();
    const deactivate = http.expectOne(`${api}/products/7/deactivate`);
    expect(deactivate.request.method).toBe('POST');
    expect(deactivate.request.body).toEqual({ version: 'v1' });
    deactivate.flush({});

    service.activate(7, 'v2').subscribe();
    const activate = http.expectOne(`${api}/products/7/activate`);
    expect(activate.request.body).toEqual({ version: 'v2' });
    activate.flush({});
  });
});