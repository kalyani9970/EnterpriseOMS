import { StorageLocation, StorageLocationType } from '../models/warehouse.models';
import { allowedChildTypes, levelOf, toTreeNodes } from './location-tree';

const location = (
  id: number,
  code: string,
  type: StorageLocationType,
  parentId: number | null = null,
  isActive = true,
): StorageLocation => ({
  id,
  warehouseId: 1,
  parentId,
  code,
  name: null,
  type,
  isActive,
  version: `v${id}`,
  created: '2026-10-17T09:00:00Z',
  lastModified: null,
});

describe('levelOf', () => {
  it('numbers the levels from Zone (1) down to Bin (5)', () => {
    expect(['Zone', 'Aisle', 'Rack', 'Shelf', 'Bin'].map((t) => levelOf(t as StorageLocationType))).toEqual([1, 2, 3, 4, 5]);
  });
});

describe('allowedChildTypes', () => {
  it('allows every type for a top-level location', () => {
    expect(allowedChildTypes(null)).toEqual(['Zone', 'Aisle', 'Rack', 'Shelf', 'Bin']);
  });

  it.each([
    ['Zone', ['Aisle', 'Rack', 'Shelf', 'Bin']],
    ['Aisle', ['Rack', 'Shelf', 'Bin']],
    ['Rack', ['Shelf', 'Bin']],
    ['Shelf', ['Bin']],
    ['Bin', []],
  ] as const)('inside a %s only the levels below it are allowed', (parentType, expected) => {
    expect(allowedChildTypes(location(1, 'P', parentType))).toEqual(expected);
  });

  it('returns a copy, so callers cannot change the shared list of types', () => {
    allowedChildTypes(null).pop();

    expect(allowedChildTypes(null)).toHaveLength(5);
  });
});

describe('toTreeNodes', () => {
  // In tree order, as the server sends it: A, A-01, A-01-R1, A-02, B
  const flat = [
    location(1, 'A', 'Zone'),
    location(2, 'A-01', 'Aisle', 1),
    location(3, 'A-01-R1', 'Rack', 2),
    location(4, 'A-02', 'Aisle', 1),
    location(5, 'B', 'Zone'),
  ];

  it('keeps the server order and computes each depth', () => {
    const nodes = toTreeNodes(flat);

    expect(nodes.map((n) => n.location.code)).toEqual(['A', 'A-01', 'A-01-R1', 'A-02', 'B']);
    expect(nodes.map((n) => n.depth)).toEqual([0, 1, 2, 1, 0]);
  });

  it('links each node to its parent', () => {
    const nodes = toTreeNodes(flat);

    expect(nodes[0].parent).toBeNull();
    expect(nodes[1].parent?.code).toBe('A');
    expect(nodes[2].parent?.code).toBe('A-01');
  });

  it('counts the ACTIVE direct children of each node', () => {
    const nodes = toTreeNodes(flat);

    expect(nodes.map((n) => n.activeChildCount)).toEqual([2, 1, 0, 0, 0]);
  });

  it('does not count inactive children', () => {
    const nodes = toTreeNodes([
      location(1, 'A', 'Zone'),
      location(2, 'A-01', 'Aisle', 1, false),
      location(3, 'A-02', 'Aisle', 1, true),
    ]);

    expect(nodes[0].activeChildCount).toBe(1);
  });

  it('treats a location whose parent is not in the list as top level', () => {
    const nodes = toTreeNodes([location(2, 'A-01', 'Aisle', 99)]);

    expect(nodes[0].depth).toBe(0);
    expect(nodes[0].parent).toBeNull();
  });

  it('handles an empty list', () => {
    expect(toTreeNodes([])).toEqual([]);
  });
});