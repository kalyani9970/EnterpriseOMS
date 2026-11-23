import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PRODUCT_LIMITS, Product, UNITS_OF_MEASURE, UnitOfMeasure } from '../../../core/models/product.models';
import { Permissions } from '../../../core/security/permissions';
import { AuthService } from '../../../core/services/auth.service';
import { ProductsService } from '../../../core/services/products.service';
import { parseApiError } from '../../../core/utils/api-error';
import { maxTwoDecimals, skuFormat, wholeNumber } from '../../../core/validators/product.validators';

type LoadStatus = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * One form for both "New product" (/products/new) and "Edit product" (/products/:id).
 * Editing sends back the Version it loaded. If somebody else saved first, the server answers 409 and
 * this screen offers to reload the latest copy instead of silently overwriting their work.
 */
@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductForm implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productsService = inject(ProductsService);
  private readonly auth = inject(AuthService);

  protected readonly units = UNITS_OF_MEASURE;
  protected readonly limits = PRODUCT_LIMITS;

  /** null = creating a new product. */
  protected readonly productId = signal<number | null>(null);
  protected readonly isEdit = computed(() => this.productId() !== null);
  protected readonly canUpdate = computed(() => this.auth.hasPermission(Permissions.Products.Update));

  protected readonly product = signal<Product | null>(null);
  protected readonly loadStatus = signal<LoadStatus>('loaded');
  protected readonly categories = signal<string[]>([]);

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly conflict = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly form = this.formBuilder.group({
    sku: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(PRODUCT_LIMITS.skuMaxLength), skuFormat]],
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(PRODUCT_LIMITS.nameMaxLength)]],
    description: ['', [Validators.maxLength(PRODUCT_LIMITS.descriptionMaxLength)]],
    category: ['', [Validators.maxLength(PRODUCT_LIMITS.categoryMaxLength)]],
    unitOfMeasure: ['Piece' as UnitOfMeasure, [Validators.required]],
    unitPrice: [0, [Validators.required, Validators.min(0), Validators.max(PRODUCT_LIMITS.maxPrice), maxTwoDecimals]],
    costPrice: [0, [Validators.required, Validators.min(0), Validators.max(PRODUCT_LIMITS.maxPrice), maxTwoDecimals]],
    reorderLevel: [0, [Validators.required, Validators.min(0), Validators.max(PRODUCT_LIMITS.maxReorderLevel), wholeNumber]],
  });

  ngOnInit(): void {
    const idText = this.route.snapshot.paramMap.get('id');
    const id = idText === null ? null : Number(idText);

    this.productsService.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });

    if (id === null) {
      return;   // create mode: nothing to load
    }

    if (!Number.isInteger(id) || id <= 0) {
      this.loadStatus.set('notFound');
      return;
    }

    this.productId.set(id);
    this.load();
  }

  protected load(): void {
    const id = this.productId();
    if (id === null) return;

    this.loadStatus.set('loading');

    this.productsService.getById(id).subscribe({
      next: (product) => {
        this.showProduct(product);
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
    const details = {
      name: value.name.trim(),
      description: emptyToNull(value.description),
      category: emptyToNull(value.category),
      unitOfMeasure: value.unitOfMeasure,
      unitPrice: value.unitPrice,
      costPrice: value.costPrice,
      reorderLevel: value.reorderLevel,
    };

    const current = this.product();
    const id = this.productId();

    if (id !== null && current) {
      this.productsService.update(id, { ...details, version: current.version }).subscribe({
        next: (updated) => {
          this.showProduct(updated);
          this.saving.set(false);
          this.saved.set(true);
        },
        error: (error: unknown) => this.onSaveError(error),
      });
    } else {
      this.productsService.create({ ...details, sku: value.sku.trim() }).subscribe({
        next: () => {
          this.saving.set(false);
          void this.router.navigate(['/products']);
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
    const current = this.product();
    if (!current) return;

    this.saving.set(true);
    this.saved.set(false);
    this.errorMessage.set(null);
    this.conflict.set(false);

    const request$ = current.isActive
      ? this.productsService.deactivate(current.id, current.version)
      : this.productsService.activate(current.id, current.version);

    request$.subscribe({
      next: (updated) => {
        this.showProduct(updated);
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

  /** Fills the form with a stored product. SKU is read-only once the product exists. */
  private showProduct(product: Product): void {
    this.product.set(product);

    this.form.reset({
      sku: product.sku,
      name: product.name,
      description: product.description ?? '',
      category: product.category ?? '',
      unitOfMeasure: product.unitOfMeasure,
      unitPrice: product.unitPrice,
      costPrice: product.costPrice,
      reorderLevel: product.reorderLevel,
    });

    this.form.controls.sku.disable();

    // A user who may view but not change products gets the same page, read-only.
    if (!this.canUpdate()) {
      this.form.disable();
    }
  }
}

function emptyToNull(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : trimmed;
}