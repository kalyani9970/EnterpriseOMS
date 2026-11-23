import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  STORAGE_LOCATION_TYPES,
  StorageLocation,
  StorageLocationType,
  WAREHOUSE_LIMITS,
} from '../../../core/models/warehouse.models';
import { WarehousesService } from '../../../core/services/warehouses.service';
import { parseApiError } from '../../../core/utils/api-error';
import { LocationNode, allowedChildTypes, toTreeNodes } from '../../../core/utils/location-tree';
import { codeFormat } from '../../../core/validators/warehouse.validators';

type LoadStatus = 'loading' | 'loaded' | 'error';

/**
 * The storage locations of ONE warehouse, drawn as an indented tree and editable in place.
 *
 * The server sends a flat list already in tree order (every parent directly followed by its
 * descendants); this component only has to indent it. The tree rules (a location sits inside a
 * HIGHER level; a branch is retired children-first and restored parent-first) are enforced by the
 * server. The screen mirrors them so a user is told why a button is disabled instead of hitting an
 * error afterwards.
 */
@Component({
  selector: 'app-location-tree',
  imports: [ReactiveFormsModule],
  templateUrl: './location-tree.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LocationTree implements OnInit {
  private readonly warehouses = inject(WarehousesService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  readonly warehouseId = input.required<number>();

  /** Whether the user may add, rename, deactivate and reactivate locations (warehouses.update). */
  readonly canEdit = input(false);

  /** Locations cannot be added to an inactive warehouse. */
  readonly warehouseActive = input(true);

  protected readonly limits = WAREHOUSE_LIMITS;

  protected readonly locations = signal<StorageLocation[]>([]);
  protected readonly nodes = computed<LocationNode[]>(() => toTreeNodes(this.locations()));
  protected readonly loadStatus = signal<LoadStatus>('loading');
  protected readonly loadError = signal('');

  protected readonly notice = signal<{ kind: 'success' | 'error'; text: string } | null>(null);
  protected readonly busyId = signal<number | null>(null);

  // ---- adding a location ----
  /** null = the add panel is closed; parent null = a top-level location. */
  protected readonly adding = signal<{ parent: StorageLocation | null } | null>(null);
  protected readonly allowedTypes = computed<StorageLocationType[]>(() =>
    allowedChildTypes(this.adding()?.parent ?? null),
  );
  protected readonly addForm = this.formBuilder.group({
    code: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(WAREHOUSE_LIMITS.locationCodeMaxLength), codeFormat]],
    name: ['', [Validators.maxLength(WAREHOUSE_LIMITS.locationNameMaxLength)]],
    type: ['Zone' as StorageLocationType, [Validators.required]],
  });
  protected readonly addSaving = signal(false);
  protected readonly addError = signal<string | null>(null);
  protected readonly addServerErrors = signal<Record<string, string[]>>({});

  // ---- renaming a location ----
  protected readonly renamingId = signal<number | null>(null);
  protected readonly renameControl = this.formBuilder.control('', [
    Validators.maxLength(WAREHOUSE_LIMITS.locationNameMaxLength),
  ]);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loadStatus.set('loading');
    this.fetch();
  }

  // ---------------- adding ----------------

  protected startAdd(parent: StorageLocation | null): void {
    const types = allowedChildTypes(parent);

    this.adding.set({ parent });
    this.addForm.reset({ code: '', name: '', type: types[0] ?? STORAGE_LOCATION_TYPES[0] });
    this.addError.set(null);
    this.addServerErrors.set({});
    this.notice.set(null);
  }

  protected cancelAdd(): void {
    this.adding.set(null);
  }

  protected saveAdd(): void {
    const panel = this.adding();

    if (!panel) return;

    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }

    this.addSaving.set(true);
    this.addError.set(null);
    this.addServerErrors.set({});

    const value = this.addForm.getRawValue();

    this.warehouses
      .createLocation(this.warehouseId(), {
        parentId: panel.parent?.id ?? null,
        code: value.code.trim(),
        name: emptyToNull(value.name),
        type: value.type,
      })
      .subscribe({
        next: (created) => {
          this.addSaving.set(false);
          this.adding.set(null);
          this.notice.set({ kind: 'success', text: `Added ${created.code}.` });
          this.fetch();
        },
        error: (error: unknown) => {
          const apiError = parseApiError(error);

          this.addSaving.set(false);
          this.addError.set(apiError.message);
          this.addServerErrors.set(apiError.fieldErrors);
        },
      });
  }

  // ---------------- renaming ----------------

  protected startRename(location: StorageLocation): void {
    this.renamingId.set(location.id);
    this.renameControl.setValue(location.name ?? '');
    this.notice.set(null);
  }

  protected cancelRename(): void {
    this.renamingId.set(null);
  }

  protected saveRename(location: StorageLocation): void {
    if (this.renameControl.invalid) {
      this.renameControl.markAsTouched();
      return;
    }

    this.busyId.set(location.id);

    this.warehouses
      .updateLocation(this.warehouseId(), location.id, {
        version: location.version,
        name: emptyToNull(this.renameControl.value),
      })
      .subscribe({
        next: () => {
          this.busyId.set(null);
          this.renamingId.set(null);
          this.notice.set({ kind: 'success', text: `Renamed ${location.code}.` });
          this.fetch();
        },
        error: (error: unknown) => {
          this.busyId.set(null);
          this.onActionError(error, location);
          this.renamingId.set(null);
        },
      });
  }

  // ---------------- activating / deactivating ----------------

  protected toggleActive(node: LocationNode): void {
    const location = node.location;

    this.busyId.set(location.id);
    this.notice.set(null);

    const request$ = location.isActive
      ? this.warehouses.deactivateLocation(this.warehouseId(), location.id, location.version)
      : this.warehouses.activateLocation(this.warehouseId(), location.id, location.version);

    request$.subscribe({
      next: () => {
        this.busyId.set(null);
        this.notice.set({
          kind: 'success',
          text: `${location.code} was ${location.isActive ? 'deactivated' : 'reactivated'}.`,
        });
        this.fetch();
      },
      error: (error: unknown) => {
        this.busyId.set(null);
        this.onActionError(error, location);
      },
    });
  }

  // ---------------- template helpers ----------------

  /** Why "Deactivate" is unavailable right now, or null when it is available. */
  protected deactivateBlockedReason(node: LocationNode): string | null {
    return node.activeChildCount > 0
      ? `Deactivate its ${node.activeChildCount} active sub-location(s) first.`
      : null;
  }

  /** Why "Reactivate" is unavailable right now, or null when it is available. */
  protected reactivateBlockedReason(node: LocationNode): string | null {
    return node.parent !== null && !node.parent.isActive ? 'Reactivate the parent location first.' : null;
  }

  protected canAddInside(node: LocationNode): boolean {
    return (
      this.canEdit() &&
      this.warehouseActive() &&
      node.location.isActive &&
      allowedChildTypes(node.location).length > 0
    );
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && (control.touched || control.dirty);
  }

  protected addServerError(field: string): string | undefined {
    return this.addServerErrors()[field]?.[0];
  }

  // ---------------- internals ----------------

  /** Reloads the list without blanking the screen, so rows do not flicker after every action. */
  private fetch(): void {
    this.warehouses.getLocations(this.warehouseId()).subscribe({
      next: (locations) => {
        this.locations.set(locations);
        this.loadStatus.set('loaded');
      },
      error: (error: unknown) => {
        this.loadError.set(parseApiError(error).message);
        this.loadStatus.set('error');
      },
    });
  }

  /** A conflict means someone else changed the row first: say so and refresh. Anything else shows the server's message. */
  private onActionError(error: unknown, location: StorageLocation): void {
    const apiError = parseApiError(error);

    if (apiError.status === 409) {
      this.notice.set({
        kind: 'error',
        text: `${location.code} was changed by someone else. The list has been refreshed. Please try again.`,
      });
      this.fetch();
    } else {
      this.notice.set({ kind: 'error', text: apiError.message });
    }
  }
}

function emptyToNull(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : trimmed;
}