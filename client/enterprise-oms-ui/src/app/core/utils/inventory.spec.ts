import { StockMovement } from '../models/inventory.models';
import { describeMovement, formatChange, MOVEMENT_TYPE_LABELS, stockStatus } from './inventory';

describe('stockStatus', () => {
  it('says "out" for zero on hand, even though a sold-out row is also low', () => {
    expect(stockStatus({ quantityOnHand: 0, isLowStock: true })).toBe('out');
  });

  it('says "low" when the quantity has reached the reorder level', () => {
    expect(stockStatus({ quantityOnHand: 5, isLowStock: true })).toBe('low');
  });

  it('says "ok" otherwise', () => {
    expect(stockStatus({ quantityOnHand: 12, isLowStock: false })).toBe('ok');
  });
});

describe('formatChange', () => {
  it('puts a plus sign on stock coming in', () => {
    expect(formatChange(10)).toBe('+10');
  });

  it('uses a real minus sign for stock going out', () => {
    expect(formatChange(-3)).toBe('−3');
  });

  it('shows a plain zero', () => {
    expect(formatChange(0)).toBe('0');
  });
});

describe('MOVEMENT_TYPE_LABELS', () => {
  it('has a plain-words label for every kind of movement', () => {
    expect(MOVEMENT_TYPE_LABELS).toEqual({ Receipt: 'Received', Issue: 'Issued', Adjustment: 'Counted' });
  });
});

describe('describeMovement', () => {
  const movement = (overrides: Partial<StockMovement>): StockMovement => ({
    id: 1,
    productId: 3,
    productSku: 'LAP-001',
    productName: 'Business Laptop',
    warehouseId: 10,
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Warehouse',
    type: 'Receipt',
    quantityChange: 10,
    quantityAfter: 22,
    reference: null,
    notes: null,
    created: '2026-10-17T09:00:00Z',
    createdBy: '5',
    ...overrides,
  });

  it('describes a receipt', () => {
    expect(describeMovement(movement({}))).toBe('Received 10 × LAP-001 into WH-MAIN. On hand: 22.');
  });

  it('describes an issue with a positive quantity, not a negative one', () => {
    expect(describeMovement(movement({ type: 'Issue', quantityChange: -3, quantityAfter: 19 }))).toBe(
      'Issued 3 × LAP-001 from WH-MAIN. On hand: 19.',
    );
  });

  it('describes a count with the difference it found', () => {
    expect(describeMovement(movement({ type: 'Adjustment', quantityChange: -4, quantityAfter: 6 }))).toBe(
      'Counted LAP-001 in WH-MAIN: 6 on hand (−4).',
    );
  });
});