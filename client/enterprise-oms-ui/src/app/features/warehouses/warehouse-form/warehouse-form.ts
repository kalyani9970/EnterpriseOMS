import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { WAREHOUSE_LIMITS, Warehouse } from '../../../core/models/warehouse.models';
import { Permissions } from '../../../core/security/permissions';
import { AuthService } from '../../../core/services/auth.service';
import { WarehousesService } from '../../../core/services/warehouses.service';
import { parseApiError } from '../../../core/utils/api-error';
import { codeFormat, countryCode } from '../../../core/validators/warehouse.validators';
import { LocationTree } from '../location-tree/location-tree';

type LoadStatus = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * One form for both "New warehouse" (/warehouses/new) and "Edit warehouse" (/warehouses/:id).
 * Editing sends back the Version it loaded. If somebody else saved first, the server answers 409 and
 * this screen offers to reload the latest copy instead of silently overwriting their work.
 * In edit mode the warehouse's storage locations are shown (and editable) below the form.
 */
@Component({
  selector: 'app-warehouse-form',
  imports: [ReactiveFormsModule, RouterLink, LocationTree],
  templateUrl: './warehouse-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WarehouseForm implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly warehousesService = inject(WarehousesService);
  private readonly auth = inject(AuthService);

  protected readonly limits = WAREHOUSE_LIMITS;

  /** null = creating a new warehouse. */
  protected readonly warehouseId = signal<number | null>(null);
  protected readonly isEdit = computed(() => this.warehouseId() !== null);
  protected readonly canUpdate = computed(() => this.auth.hasPermission(Permissions.Warehouses.Update));

  protected readonly warehouse = signal<Warehouse | null>(null);
  protected readonly loadStatus = signal<LoadStatus>('loaded');

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly conflict = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly form = this.formBuilder.group({
    code: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(WAREHOUSE_LIMITS.codeMaxLength), codeFormat]],
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(WAREHOUSE_LIMITS.nameMaxLength)]],
    address: this.formBuilder.group({
      line1: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(WAREHOUSE_LIMITS.lineMaxLength)]],
      line2: ['', [Validators.maxLength(WAREHOUSE_LIMITS.lineMaxLength)]],
      city: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(WAREHOUSE_LIMITS.cityMaxLength)]],
      region: ['', [Validators.maxLength(WAREHOUSE_LIMITS.regionMaxLength)]],
      postalCode: ['', [Validators.maxLength(WAREHOUSE_LIMITS.postalCodeMaxLength)]],
      countryCode: ['', [Validators.required, countryCode]],
    }),
  });

  protected get address() {
    return this.form.controls.address.controls;
  }

  ngOnInit(): void {
    const idText = this.route.snapshot.paramMap.get('id');
    const id = idText === null ? null : Number(idText);

    if (id === null) {
      return;   // create mode: nothing to load
    }

    if (!Number.isInteger(id) || id <= 0) {
      this.loadStatus.set('notFound');
      return;
    }

    this.warehouseId.set(id);
    this.load();
  }

  protected load(): void {
    const id = this.warehouseId();
    if (id === null) return;

    this.loadStatus.set('loading');

    this.warehousesService.getById(id).subscribe({
      next: (warehouse) => {
        this.showWarehouse(warehouse);
        this.conflict.set(false);
        this.errorMessage.set(null);
        this.loadStatus.set('loaded');
      },
      error: (error: unknown) => {
        const apiError = parseApiError(error);
        this.errorMessage.set(apiError.message);
        this.loadStatus.set(apiError.status === 404 ? 'notFound' : 'error');
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saved.set(false);
    this.errorMessage.set(null);
    this.conflict.set(false);
    this.serverErrors.set({});

    const value = this.form.getRawValue();
    const name = value.name.trim();
    const address = {
      line1: value.address.line1.trim(),
      line2: emptyToNull(value.address.line2),
      city: value.address.city.trim(),
      region: emptyToNull(value.address.region),
      postalCode: emptyToNull(value.address.postalCode),
      countryCode: value.address.countryCode.trim().toUpperCase(),
    };

    const current = this.warehouse();
    const id = this.warehouseId();

    if (id !== null && current) {
      this.warehousesService.update(id, { version: current.version, name, address }).subscribe({
        next: (updated) => {
          this.showWarehouse(updated);
          this.saving.set(false);
          this.saved.set(true);
        },
        error: (error: unknown) => this.onSaveError(error),
      });
    } else {
      this.warehousesService.create({ code: value.code.trim(), name, address }).subscribe({
        next: () => {
          this.saving.set(false);
          void this.router.navigate(['/warehouses']);
        },
        error: (error: unknown) => this.onSaveError(error),
      });
    }
  }

  /** Throws away the user's unsaved edits and shows what is stored now. */
  protected reloadLatest(): void {
    this.saved.set(false);
    this.load();
  }

  protected toggleActive(): void {
    const current = this.warehouse();
    if (!current) return;

    this.saving.set(true);
    this.saved.set(false);
    this.errorMessage.set(null);
    this.conflict.set(false);

    const request$ = current.isActive
      ? this.warehousesService.deactivate(current.id, current.version)
      : this.warehousesService.activate(current.id, current.version);

    request$.subscribe({
      next: (updated) => {
        this.showWarehouse(updated);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (error: unknown) => this.onSaveError(error),
    });
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && (control.touched || control.dirty);
  }

  protected serverError(field: string): string | undefined {
    return this.serverErrors()[field]?.[0];
  }

  private onSaveError(error: unknown): void {
    const apiError = parseApiError(error);

    this.saving.set(false);
    this.errorMessage.set(apiError.message);
    this.serverErrors.set(apiError.fieldErrors);
    this.conflict.set(apiError.status === 409);
  }

  /** Fills the form with a stored warehouse. The code is read-only once the warehouse exists. */
  private showWarehouse(warehouse: Warehouse): void {
    this.warehouse.set(warehouse);

    this.form.reset({
      code: warehouse.code,
      name: warehouse.name,
      address: {
        line1: warehouse.address.line1,
        line2: warehouse.address.line2 ?? '',
        city: warehouse.address.city,
        region: warehouse.address.region ?? '',
        postalCode: warehouse.address.postalCode ?? '',
        countryCode: warehouse.address.countryCode,
      },
    });

    this.form.controls.code.disable();

    // A user who may view but not change warehouses gets the same page, read-only.
    if (!this.canUpdate()) {
      this.form.disable();
    }
  }
}

function emptyToNull(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : trimmed;
}