/** Mirrors the UnitOfMeasure enum on the API, which sends and receives enums as names. */
export type UnitOfMeasure = 'Piece' | 'Kilogram' | 'Litre' | 'Metre' | 'Box' | 'Pack' | 'Pallet';

export const UNITS_OF_MEASURE: readonly UnitOfMeasure[] = [
  'Piece',
  'Kilogram',
  'Litre',
  'Metre',
  'Box',
  'Pack',
  'Pallet',
];

/** The columns a product list can be sorted by (the API accepts only these). */
export type ProductSortField = 'sku' | 'name' | 'price' | 'created';

/** Limits shared with the server's rules, so the form can explain problems before sending. */
export const PRODUCT_LIMITS = {
  skuMaxLength: 50,
  nameMaxLength: 200,
  descriptionMaxLength: 2000,
  categoryMaxLength: 100,
  maxPrice: 999_999_999.99,
  maxReorderLevel: 1_000_000,
} as const;

/** One row of the product list (ProductListItemDto). */
export interface ProductListItem {
  id: number;
  sku: string;
  name: string;
  category: string | null;
  unitOfMeasure: UnitOfMeasure;
  unitPrice: number;
  reorderLevel: number;
  isActive: boolean;
  version: string;
}

/** Full product detail (ProductDto). */
export interface Product extends ProductListItem {
  description: string | null;
  costPrice: number;
  created: string;
  lastModified: string | null;
}

/** The page envelope the API wraps around every list (PaginatedList). */
export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ProductListQuery {
  pageNumber: number;
  pageSize: number;
  search?: string;
  category?: string;
  /** null or undefined = both active and inactive. */
  isActive?: boolean | null;
  sortBy: ProductSortField;
  descending: boolean;
}

export interface CreateProductRequest {
  sku: string;
  name: string;
  description: string | null;
  category: string | null;
  unitOfMeasure: UnitOfMeasure;
  unitPrice: number;
  costPrice: number;
  reorderLevel: number;
}

/** No sku: it never changes. The version is the one the user last saw, which proves the edit is current. */
export interface UpdateProductRequest {
  version: string;
  name: string;
  description: string | null;
  category: string | null;
  unitOfMeasure: UnitOfMeasure;
  unitPrice: number;
  costPrice: number;
  reorderLevel: number;
}