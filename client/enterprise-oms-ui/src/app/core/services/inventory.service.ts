import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import {
  AdjustStockRequest,
  IssueStockRequest,
  PagedResult,
  ReceiveStockRequest,
  StockLevelItem,
  StockLevelQuery,
  StockMovement,
  StockMovementQuery,
} from '../models/inventory.models';

/**
 * Talks to /api/inventory. The organization is never sent: the server takes it from the signed-in
 * user's token, and the database filters by it. Stock is only ever changed through receipts, issues
 * and counts: there is no "set the quantity" call, so every change leaves a ledger line.
 */
@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getStock(query: StockLevelQuery): Observable<PagedResult<StockLevelItem>> {
    // Only send filters that are actually set, so URLs stay short and the server uses its defaults.
    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('descending', query.descending)
      .set('lowStockOnly', query.lowStockOnly);

    if (query.warehouseId) {
      params = params.set('warehouseId', query.warehouseId);
    }

    if (query.productId) {
      params = params.set('productId', query.productId);
    }

    const search = query.search?.trim();
    if (search) {
      params = params.set('search', search);
    }

    return this.http.get<PagedResult<StockLevelItem>>(`${this.baseUrl}/inventory/stock`, { params });
  }

  getMovements(query: StockMovementQuery): Observable<PagedResult<StockMovement>> {
    let params = new HttpParams().set('pageNumber', query.pageNumber).set('pageSize', query.pageSize);

    if (query.productId) {
      params = params.set('productId', query.productId);
    }

    if (query.warehouseId) {
      params = params.set('warehouseId', query.warehouseId);
    }

    if (query.type) {
      params = params.set('type', query.type);
    }

    return this.http.get<PagedResult<StockMovement>>(`${this.baseUrl}/inventory/movements`, { params });
  }

  receive(request: ReceiveStockRequest): Observable<StockMovement> {
    return this.http.post<StockMovement>(`${this.baseUrl}/inventory/receipts`, request);
  }

  issue(request: IssueStockRequest): Observable<StockMovement> {
    return this.http.post<StockMovement>(`${this.baseUrl}/inventory/issues`, request);
  }

  adjust(request: AdjustStockRequest): Observable<StockMovement> {
    return this.http.post<StockMovement>(`${this.baseUrl}/inventory/adjustments`, request);
  }
}