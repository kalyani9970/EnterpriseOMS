import { PagedResult } from './product.models';

export type { PagedResult };

/** The levels of a warehouse, biggest to smallest. A location may only sit inside a HIGHER level. */
export type StorageLocationType = 'Zone' | 'Aisle' | 'Rack' | 'Shelf' | 'Bin';

/** In level order: the position in this list is the level (Zone = 1 ... Bin = 5), as on the API. */
export const STORAGE_LOCATION_TYPES: readonly StorageLocationType[] = ['Zone', 'Aisle', 'Rack', 'Shelf', 'Bin'];

/** The columns a warehouse list can be sorted by (the API accepts only these). */
export type WarehouseSortField = 'code' | 'name' | 'city' | 'created';

/** Limits shared with the server's rules, so the forms can explain problems before sending. */
export const WAREHOUSE_LIMITS = {
  codeMaxLength: 30,
  nameMaxLength: 200,
  lineMaxLength: 200,
  cityMaxLength: 100,
  regionMaxLength: 100,
  postalCodeMaxLength: 20,
  locationCodeMaxLength: 30,
  locationNameMaxLength: 200,
} as const;

/** A postal address (AddressDto). Optional parts are null when blank. */
export interface Address {
  line1: string;
  line2: string | null;
  city: string;
  region: string | null;
  postalCode: string | null;
  /** Two-letter ISO country code, always upper case from the server. */
  countryCode: string;
}

/** One row of the warehouse list (WarehouseListItemDto). */
export interface WarehouseListItem {
  id: number;
  code: string;
  name: string;
  city: string;
  countryCode: string;
  isActive: boolean;
  version: string;
  locationCount: number;
}

/** Full warehouse detail (WarehouseDto). */
export interface Warehouse {
  id: number;
  code: string;
  name: string;
  address: Address;
  isActive: boolean;
  version: string;
  locationCount: number;
  created: string;
  lastModified: string | null;
}

export interface WarehouseListQuery {
  pageNumber: number;
  pageSize: number;
  search?: string;
  /** null or undefined = both active and inactive. */
  isActive?: boolean | null;
  sortBy: WarehouseSortField;
  descending: boolean;
}

export interface CreateWarehouseRequest {
  code: string;
  name: string;
  address: Address;
}

/** No code: it never changes. The version is the one the user last saw, which proves the edit is current. */
export interface UpdateWarehouseRequest {
  version: string;
  name: string;
  address: Address;
}

/** One place inside a warehouse (StorageLocationDto). Clients build the tree from parentId. */
export interface StorageLocation {
  id: number;
  warehouseId: number;
  parentId: number | null;
  code: string;
  name: string | null;
  type: StorageLocationType;
  isActive: boolean;
  version: string;
  created: string;
  lastModified: string | null;
}

export interface CreateStorageLocationRequest {
  parentId: number | null;
  code: string;
  name: string | null;
  type: StorageLocationType;
}

/** Only the name can change: a location's code, type and parent are fixed once it exists. */
export interface UpdateStorageLocationRequest {
  version: string;
  name: string | null;
}