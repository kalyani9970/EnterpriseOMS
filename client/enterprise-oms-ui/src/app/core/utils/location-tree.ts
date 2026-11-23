import { STORAGE_LOCATION_TYPES, StorageLocation, StorageLocationType } from '../models/warehouse.models';

/** A location plus what a tree view needs to draw it. */
export interface LocationNode {
  location: StorageLocation;
  /** 0 for a top-level location, 1 for its children, and so on. Used for indentation. */
  depth: number;
  /** How many of its direct children are still active (a location with any cannot be deactivated). */
  activeChildCount: number;
  /** The location it sits inside, or null for a top-level location. */
  parent: StorageLocation | null;
}

/** The level of a type: Zone = 1 ... Bin = 5 (its position in the ordered list). */
export function levelOf(type: StorageLocationType): number {
  return STORAGE_LOCATION_TYPES.indexOf(type) + 1;
}

/**
 * The types a new location may have inside the given parent: only levels BELOW the parent's.
 * Mirrors the server's rule (levels may be skipped). A Bin has no valid child type, so the result
 * is empty. With no parent (a top-level location) any type is allowed.
 */
export function allowedChildTypes(parent: StorageLocation | null): StorageLocationType[] {
  if (parent === null) return [...STORAGE_LOCATION_TYPES];

  return STORAGE_LOCATION_TYPES.filter((type) => levelOf(type) > levelOf(parent.type));
}

/**
 * Turns the server's flat list (already in tree order: every parent comes before its children)
 * into rows with their depth, parent and number of active children.
 */
export function toTreeNodes(locations: readonly StorageLocation[]): LocationNode[] {
  const byId = new Map(locations.map((location) => [location.id, location]));
  const depthOf = new Map<number, number>();

  const activeChildren = new Map<number, number>();
  for (const location of locations) {
    if (location.parentId !== null && location.isActive) {
      activeChildren.set(location.parentId, (activeChildren.get(location.parentId) ?? 0) + 1);
    }
  }

  return locations.map((location) => {
    const parent = location.parentId === null ? null : (byId.get(location.parentId) ?? null);
    const depth = parent === null ? 0 : (depthOf.get(parent.id) ?? 0) + 1;
    depthOf.set(location.id, depth);

    return { location, depth, activeChildCount: activeChildren.get(location.id) ?? 0, parent };
  });
}