import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-config';
import { StockLevelQuery, StockMovementQuery } from '../models/inventory.models';
import { InventoryService } from './inventory.service';

describe('InventoryService', () => {
  let service: InventoryService;
  let http: HttpTestingController;
  let api: string;

  const stockQuery: StockLevelQuery = { pageNumber: 1, pageSize: 20, lowStockOnly: false, sortBy: 'sku', descending: false };
  const movementQuery: StockMovementQuery = { pageNumber: 1, pageSize: 20 };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    service = TestBed.inject(InventoryService);
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  const stockRequest = () => http.expectOne((request) => request.url === `${api}/inventory/stock`);
  const movementsRequest = () => http.expectOne((request) => request.url === `${api}/inventory/movements`);

  it('getStock always sends paging, sorting and the low-stock switch', () => {
    service.getStock(stockQuery).subscribe();

    const request = stockRequest();
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('sortBy')).toBe('sku');
    expect(request.request.params.get('descending')).toBe('false');
    expect(request.request.params.get('lowStockOnly')).toBe('false');
    request.flush({});
  });

  it('getStock leaves out filters that are not set', () => {
    service.getStock({ ...stockQuery, search: '   ', warehouseId: null, productId: null }).subscribe();

    const request = stockRequest();
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('warehouseId')).toBe(false);
    expect(request.request.params.has('productId')).toBe(false);
    request.flush({});
  });

  it('getStock sends the filters that are set, trimming the search text', () => {
    service
      .getStock({ ...stockQuery, search: '  lap ', warehouseId: 3, productId: 7, lowStockOnly: true, sortBy: 'quantity', descending: true })
      .subscribe();

    const request = stockRequest();
    expect(request.request.params.get('search')).toBe('lap');
    expect(request.request.params.get('warehouseId')).toBe('3');
    expect(request.request.params.get('productId')).toBe('7');
    expect(request.request.params.get('lowStockOnly')).toBe('true');
    expect(request.request.params.get('sortBy')).toBe('quantity');
    expect(request.request.params.get('descending')).toBe('true');
    request.flush({});
  });

  it('getMovements sends paging only when no filter is set', () => {
    service.getMovements({ ...movementQuery, productId: null, warehouseId: null, type: null }).subscribe();

    const request = movementsRequest();
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.has('productId')).toBe(false);
    expect(request.request.params.has('warehouseId')).toBe(false);
    expect(request.request.params.has('type')).toBe(false);
    request.flush({});
  });

  it('getMovements sends the filters that are set, the kind by its name', () => {
    service.getMovements({ ...movementQuery, productId: 7, warehouseId: 3, type: 'Issue' }).subscribe();

    const request = movementsRequest();
    expect(request.request.params.get('productId')).toBe('7');
    expect(request.request.params.get('warehouseId')).toBe('3');
    expect(request.request.params.get('type')).toBe('Issue');
    request.flush({});
  });

  it('receive posts to the receipts endpoint', () => {
    const body = { productId: 7, warehouseId: 3, quantity: 10, reference: 'PO-1', notes: null };

    service.receive(body).subscribe();

    const request = http.expectOne(`${api}/inventory/receipts`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });

  it('issue posts to the issues endpoint', () => {
    const body = { productId: 7, warehouseId: 3, quantity: 4, reference: null, notes: 'Shipped' };

    service.issue(body).subscribe();

    const request = http.expectOne(`${api}/inventory/issues`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });

  it('adjust posts the COUNTED quantity and the reason to the adjustments endpoint', () => {
    const body = { productId: 7, warehouseId: 3, countedQuantity: 12, reason: 'Cycle count' };

    service.adjust(body).subscribe();

    const request = http.expectOne(`${api}/inventory/adjustments`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });
});