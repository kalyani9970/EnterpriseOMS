import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import {
  CreateProductRequest,
  PagedResult,
  Product,
  ProductListItem,
  ProductListQuery,
  UpdateProductRequest,
} from '../models/product.models';

/**
 * Talks to /api/products. The organization is never sent: the server takes it from the
 * signed-in user's token, and the database filters by it.
 */
@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  list(query: ProductListQuery): Observable<PagedResult<ProductListItem>> {
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

    if (query.category) {
      params = params.set('category', query.category);
    }

    if (query.isActive !== null && query.isActive !== undefined) {
      params = params.set('isActive', query.isActive);
    }

    return this.http.get<PagedResult<ProductListItem>>(`${this.baseUrl}/products`, { params });
  }

  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/products/categories`);
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`${this.baseUrl}/products/${id}`);
  }

  create(request: CreateProductRequest): Observable<Product> {
    return this.http.post<Product>(`${this.baseUrl}/products`, request);
  }

  update(id: number, request: UpdateProductRequest): Observable<Product> {
    return this.http.put<Product>(`${this.baseUrl}/products/${id}`, request);
  }

  deactivate(id: number, version: string): Observable<Product> {
    return this.http.post<Product>(`${this.baseUrl}/products/${id}/deactivate`, { version });
  }

  activate(id: number, version: string): Observable<Product> {
    return this.http.post<Product>(`${this.baseUrl}/products/${id}/activate`, { version });
  }
}