import { PagedResult, UnitOfMeasure } from './product.models';

export type { PagedResult };

/** Why stock changed. The numbers 1-3 on the API are sent as these names. */
export type StockMovementType = 'Receipt' | 'Issue' | 'Adjustment';

export const STOCK_MOVEMENT_TYPES: readonly StockMovementType[] = ['Receipt', 'Issue', 'Adjustment'];

/** The columns the stock overview can be sorted by (the API accepts only these). */
export type StockSortField = 'sku' | 'name' | 'quantity' | 'warehouse';

/** Limits shared with the server's rules, so the forms can explain problems before sending. */
export const INVENTORY_LIMITS = {
  maxQuantity: 1_000_000_000,
  referenceMaxLength: 100,
  notesMaxLength: 500,
} as const;

/** One row of the stock overview (StockLevelListItemDto): how much of a product is in a warehouse. */
export interface StockLevelItem {
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: UnitOfMeasure;
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  quantityOnHand: number;
  reorderLevel: number;
  /** True when the quantity has reached the product's reorder level (or is below it). */
  isLowStock: boolean;
}

/** One line of the stock ledger (StockMovementDto). */
export interface StockMovement {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  type: StockMovementType;
  /** Signed: positive for stock in, negative for stock out. */
  quantityChange: number;
  /** The stock level right after this movement: a running balance. */
  quantityAfter: number;
  reference: string | null;
  notes: string | null;
  /** UTC. The API may leave off the trailing "Z", so read it with parseUtc, never new Date(). */
  created: string;
  /** The id of the user who recorded it. */
  createdBy: string | null;
}

export interface StockLevelQuery {
  pageNumber: number;
  pageSize: number;
  warehouseId?: number | null;
  productId?: number | null;
  search?: string;
  lowStockOnly: boolean;
  sortBy: StockSortField;
  descending: boolean;
}

export interface StockMovementQuery {
  pageNumber: number;
  pageSize: number;
  productId?: number | null;
  warehouseId?: number | null;
  type?: StockMovementType | null;
}

export interface ReceiveStockRequest {
  productId: number;
  warehouseId: number;
  quantity: number;
  reference: string | null;
  notes: string | null;
}

export interface IssueStockRequest {
  productId: number;
  warehouseId: number;
  quantity: number;
  reference: string | null;
  notes: string | null;
}

/** A stock count: you say what you COUNTED, and the system records the difference. The reason is required. */
export interface AdjustStockRequest {
  productId: number;
  warehouseId: number;
  countedQuantity: number;
  reason: string;
}

/** What the movement form needs to know about a product, and a warehouse, to show and send them. */
export interface ProductRef {
  id: number;
  sku: string;
  name: string;
}

export interface WarehouseRef {
  id: number;
  code: string;
  name: string;
}

export type MovementMode = 'receive' | 'issue' | 'adjust';