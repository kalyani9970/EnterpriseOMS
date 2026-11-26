import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import {
  INVENTORY_LIMITS,
  MovementMode,
  ProductRef,
  StockMovement,
  WarehouseRef,
} from '../../../core/models/inventory.models';
import { ProductListItem } from '../../../core/models/product.models';
import { InventoryService } from '../../../core/services/inventory.service';
import { ProductsService } from '../../../core/services/products.service';
import { WarehousesService } from '../../../core/services/warehouses.service';
import { parseApiError } from '../../../core/utils/api-error';
import { wholeNumber } from '../../../core/validators/product.validators';

const SEARCH_DEBOUNCE_MS = 300;

/**
 * One form for the three ways stock changes:
 *  - RECEIVE: stock came in (a delivery).
 *  - ISSUE: stock went out (used, shipped, written off).
 *  - ADJUST: a stock COUNT. You enter what you counted; the system records the difference, with a reason.
 *
 * When opened from a stock row, the product and warehouse are given. When opened to receive stock that was
 * never here before, the user picks them. Every refusal from the server is shown in plain words, and a
 * conflict (someone changed the same stock at the same moment) tells the user what to do next.
 */
@Component({
  selector: 'app-stock-movement-form',
  imports: [ReactiveFormsModule],
  templateUrl: './stock-movement-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockMovementForm implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly inventory = inject(InventoryService);
  private readonly productsService = inject(ProductsService);
  private readonly warehousesService = inject(WarehousesService);

  readonly mode = input.required<MovementMode>();

  /** Set when the form is opened from a stock row. Left out to let the user choose (first receipts). */
  readonly product = input<ProductRef | null>(null);
  readonly warehouse = input<WarehouseRef | null>(null);

  /** What the system currently says is on hand, if known. Shown for context, and used to catch over-issuing early. */
  readonly currentQuantity = input<number | null>(null);

  readonly saved = output<StockMovement>();
  readonly cancelled = output<void>();

  /** Fired when the server reports a conflict, so the screen can refresh the numbers behind this form. */
  readonly conflict = output<void>();

  protected readonly limits = INVENTORY_LIMITS;

  // ---- choosing a product and a warehouse (only when they were not given) ----
  protected readonly pickedProduct = signal<ProductRef | null>(null);
  protected readonly productResults = signal<ProductListItem[]>([]);
  protected readonly warehouseOptions = signal<WarehouseRef[]>([]);
  protected readonly pickedWarehouseId = signal<number | null>(null);
  protected readonly pickerError = signal<string | null>(null);

  protected readonly effectiveProduct = computed(() => this.product() ?? this.pickedProduct());
  protected readonly effectiveWarehouse = computed(
    () => this.warehouse() ?? this.warehouseOptions().find((w) => w.id === this.pickedWarehouseId()) ?? null,
  );
  protected readonly needsProductPicker = computed(() => this.product() === null);
  protected readonly needsWarehousePicker = computed(() => this.warehouse() === null);

  protected readonly title = computed(() => {
    switch (this.mode()) {
      case 'receive':
        return 'Receive stock';
      case 'issue':
        return 'Issue stock';
      default:
        return 'Count stock';
    }
  });

  protected readonly submitLabel = computed(() => {
    switch (this.mode()) {
      case 'receive':
        return 'Record receipt';
      case 'issue':
        return 'Record issue';
      default:
        return 'Record count';
    }
  });

  // ---- saving ----
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly conflictNotice = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly form = this.formBuilder.group({
    quantity: [1, [Validators.required, Validators.min(1), Validators.max(INVENTORY_LIMITS.maxQuantity), wholeNumber]],
    reference: ['', [Validators.maxLength(INVENTORY_LIMITS.referenceMaxLength)]],
    notes: ['', [Validators.maxLength(INVENTORY_LIMITS.notesMaxLength)]],
    countedQuantity: [0, [Validators.required, Validators.min(0), Validators.max(INVENTORY_LIMITS.maxQuantity), wholeNumber]],
    reason: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(INVENTORY_LIMITS.notesMaxLength)]],
  });

  private readonly searchTyped$ = new Subject<string>();

  constructor() {
    // Wait until the user pauses typing before searching products, and drop answers that have been overtaken.
    this.searchTyped$
      .pipe(
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        switchMap((text) =>
          text.trim() === ''
            ? of<ProductListItem[]>([])
            : this.productsService
                .list({
                  pageNumber: 1,
                  pageSize: 8,
                  search: text,
                  // Stock can only be received for an ACTIVE product, but issued and counted for any.
                  isActive: this.mode() === 'receive' ? true : null,
                  sortBy: 'name',
                  descending: false,
                })
                .pipe(
                  switchMap((page) => of(page.items)),
                  catchError(() => of<ProductListItem[]>([])),
                ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.productResults.set(items));
  }

  ngOnInit(): void {
    // Only the fields of THIS kind of movement take part in validation.
    if (this.mode() === 'adjust') {
      this.form.controls.quantity.disable();
      this.form.controls.reference.disable();
      this.form.controls.notes.disable();

      // Start from what the system says, so the user only changes the number they actually counted differently.
      this.form.controls.countedQuantity.setValue(this.currentQuantity() ?? 0);
    } else {
      this.form.controls.countedQuantity.disable();
      this.form.controls.reason.disable();
    }

    if (this.needsWarehousePicker()) {
      this.warehousesService
        .list({
          pageNumber: 1,
          pageSize: 100,
          sortBy: 'name',
          descending: false,
          isActive: this.mode() === 'receive' ? true : null,
        })
        .subscribe({
          next: (page) =>
            this.warehouseOptions.set(page.items.map((w) => ({ id: w.id, code: w.code, name: w.name }))),
          error: () => this.pickerError.set('The warehouses could not be loaded. Please try again.'),
        });
    }
  }

  // ---------------- choosing ----------------

  protected onProductSearch(event: Event): void {
    this.searchTyped$.next((event.target as HTMLInputElement).value);
  }

  protected pickProduct(product: ProductListItem): void {
    this.pickedProduct.set({ id: product.id, sku: product.sku, name: product.name });
    this.productResults.set([]);
    this.pickerError.set(null);
  }

  protected changeProduct(): void {
    this.pickedProduct.set(null);
  }

  protected onWarehouseChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    this.pickedWarehouseId.set(Number.isInteger(value) && value > 0 ? value : null);
    this.pickerError.set(null);
  }

  // ---------------- saving ----------------

  protected save(): void {
    const product = this.effectiveProduct();
    const warehouse = this.effectiveWarehouse();

    if (!product || !warehouse) {
      this.pickerError.set('Choose a product and a warehouse first.');
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.exceedsStockOnHand()) {
      this.form.controls.quantity.markAsTouched();
      return;   // the hint under the field already says how many are on hand
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.conflictNotice.set(false);
    this.serverErrors.set({});
    this.pickerError.set(null);

    const value = this.form.getRawValue();
    const target = { productId: product.id, warehouseId: warehouse.id };

    const request$ =
      this.mode() === 'receive'
        ? this.inventory.receive({
            ...target,
            quantity: value.quantity,
            reference: emptyToNull(value.reference),
            notes: emptyToNull(value.notes),
          })
        : this.mode() === 'issue'
          ? this.inventory.issue({
              ...target,
              quantity: value.quantity,
              reference: emptyToNull(value.reference),
              notes: emptyToNull(value.notes),
            })
          : this.inventory.adjust({ ...target, countedQuantity: value.countedQuantity, reason: value.reason.trim() });

    request$.subscribe({
      next: (movement) => {
        this.saving.set(false);
        this.saved.emit(movement);
      },
      error: (error: unknown) => this.onSaveError(error),
    });
  }

  /** True while issuing more than the system says is on hand: the server would refuse it, so say so first. */
  protected exceedsStockOnHand(): boolean {
    const onHand = this.currentQuantity();

    return this.mode() === 'issue' && onHand !== null && this.form.controls.quantity.value > onHand;
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

    if (apiError.status === 409) {
      // Someone else changed this stock at the same moment, so the number this form was based on is stale.
      this.conflictNotice.set(true);
      this.conflict.emit();
      return;
    }

    // Business-rule refusals (422) arrive in plain words, for example "Not enough stock: 5 on hand, 6 requested."
    this.errorMessage.set(apiError.message);
    this.serverErrors.set(apiError.fieldErrors);
  }
}

function emptyToNull(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : trimmed;
}