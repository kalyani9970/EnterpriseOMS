import { StockLevelItem, StockMovement, StockMovementType } from '../models/inventory.models';

export type StockStatus = 'out' | 'low' | 'ok';

/** What a stock row should say: sold out, running low (at or below its reorder level), or fine. */
export function stockStatus(item: Pick<StockLevelItem, 'quantityOnHand' | 'isLowStock'>): StockStatus {
  if (item.quantityOnHand === 0) return 'out';

  return item.isLowStock ? 'low' : 'ok';
}

export const MOVEMENT_TYPE_LABELS: Record<StockMovementType, string> = {
  Receipt: 'Received',
  Issue: 'Issued',
  Adjustment: 'Counted',
};

/** "+10", "−3" (a real minus sign, not a hyphen) or "0": the signed change of a ledger line. */
export function formatChange(change: number): string {
  if (change > 0) return `+${change}`;
  if (change < 0) return `−${Math.abs(change)}`;

  return '0';
}

/** One plain sentence for a movement that was just recorded, for the confirmation message. */
export function describeMovement(movement: StockMovement): string {
  const where = movement.warehouseCode;

  switch (movement.type) {
    case 'Receipt':
      return `Received ${movement.quantityChange} × ${movement.productSku} into ${where}. On hand: ${movement.quantityAfter}.`;
    case 'Issue':
      return `Issued ${Math.abs(movement.quantityChange)} × ${movement.productSku} from ${where}. On hand: ${movement.quantityAfter}.`;
    default:
      return `Counted ${movement.productSku} in ${where}: ${movement.quantityAfter} on hand (${formatChange(movement.quantityChange)}).`;
  }
}