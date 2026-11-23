import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import {
  CreateStorageLocationRequest,
  CreateWarehouseRequest,
  PagedResult,
  StorageLocation,
  UpdateStorageLocationRequest,
  UpdateWarehouseRequest,
  Warehouse,
  WarehouseListItem,
  WarehouseListQuery,
} from '../models/warehouse.models';

/**
 * Talks to /api/warehouses. The organization is never sent: the server takes it from the
 * signed-in user's token, and the database filters by it.
 */
@Injectable({ providedIn: 'root' })
export class WarehousesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  private url(path = ''): string {
    return `${this.baseUrl}/warehouses${path}`;
  }

  list(query: WarehouseListQuery): Observable<PagedResult<WarehouseListItem>> {
    // Only send filters that are actually set, so URLs stay short and the server uses its defaults.
    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('descending', query.descending);

    const search = query.search?.trim();
    if (search) {
      params = params.set('search', search);
    }

    if (query.isActive !== null && query.isActive !== undefined) {
      params = params.set('isActive', query.isActive);
    }

    return this.http.get<PagedResult<WarehouseListItem>>(this.url(), { params });
  }

  getById(id: number): Observable<Warehouse> {
    return this.http.get<Warehouse>(this.url(`/${id}`));
  }

  create(request: CreateWarehouseRequest): Observable<Warehouse> {
    return this.http.post<Warehouse>(this.url(), request);
  }

  update(id: number, request: UpdateWarehouseRequest): Observable<Warehouse> {
    return this.http.put<Warehouse>(this.url(`/${id}`), request);
  }

  deactivate(id: number, version: string): Observable<Warehouse> {
    return this.http.post<Warehouse>(this.url(`/${id}/deactivate`), { version });
  }

  activate(id: number, version: string): Observable<Warehouse> {
    return this.http.post<Warehouse>(this.url(`/${id}/activate`), { version });
  }

  // ---------------- storage locations ----------------

  /** All locations of a warehouse, already in tree order (each parent followed by its descendants). */
  getLocations(warehouseId: number, activeOnly = false): Observable<StorageLocation[]> {
    const params = new HttpParams().set('activeOnly', activeOnly);

    return this.http.get<StorageLocation[]>(this.url(`/${warehouseId}/locations`), { params });
  }

  createLocation(warehouseId: number, request: CreateStorageLocationRequest): Observable<StorageLocation> {
    return this.http.post<StorageLocation>(this.url(`/${warehouseId}/locations`), request);
  }

  updateLocation(
    warehouseId: number,
    locationId: number,
    request: UpdateStorageLocationRequest,
  ): Observable<StorageLocation> {
    return this.http.put<StorageLocation>(this.url(`/${warehouseId}/locations/${locationId}`), request);
  }

  deactivateLocation(warehouseId: number, locationId: number, version: string): Observable<StorageLocation> {
    return this.http.post<StorageLocation>(this.url(`/${warehouseId}/locations/${locationId}/deactivate`), { version });
  }

  activateLocation(warehouseId: number, locationId: number, version: string): Observable<StorageLocation> {
    return this.http.post<StorageLocation>(this.url(`/${warehouseId}/locations/${locationId}/activate`), { version });
  }
}