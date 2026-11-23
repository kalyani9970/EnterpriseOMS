import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { PagedResult, ProductListItem, ProductListQuery, ProductSortField } from '../../../core/models/product.models';
import { Permissions } from '../../../core/security/permissions';
import { AuthService } from '../../../core/services/auth.service';
import { ProductsService } from '../../../core/services/products.service';
import { parseApiError } from '../../../core/utils/api-error';

type LoadStatus = 'loading' | 'loaded' | 'error';
type StatusFilter = 'all' | 'active' | 'inactive';

const SEARCH_DEBOUNCE_MS = 300;

/**
 * The product catalog: search, filter, sort and page, all done by the server so the list stays fast
 * however large the catalog grows. The component only holds the "what am I looking at" state.
 */
@Component({
  selector: 'app-product-list',
  imports: [RouterLink, DecimalPipe],
  templateUrl: './product-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductList implements OnInit {
  private readonly productsService = inject(ProductsService);
  private readonly auth = inject(AuthService);

  protected readonly canCreate = computed(() => this.auth.hasPermission(Permissions.Products.Create));
  protected readonly canUpdate = computed(() => this.auth.hasPermission(Permissions.Products.Update));

  // ---- what the user asked to see ----
  protected readonly search = signal('');
  protected readonly category = signal('');
  protected readonly statusFilter = signal<StatusFilter>('all');
  protected readonly sortBy = signal<ProductSortField>('name');
  protected readonly descending = signal(false);
  protected readonly pageNumber = signal(1);
  protected readonly pageSize = signal(20);

  // ---- what we got back ----
  protected readonly page = signal<PagedResult<ProductListItem> | null>(null);
  protected readonly loadStatus = signal<LoadStatus>('loading');
  protected readonly loadError = signal('');
  protected readonly categories = signal<string[]>([]);

  // ---- feedback about row actions ----
  protected readonly notice = signal<{ kind: 'success' | 'error'; text: string } | null>(null);
  protected readonly busyProductId = signal<number | null>(null);

  protected readonly pageSizes = [10, 20, 50];

  protected readonly rangeStart = computed(() => {
    const current = this.page();
    return current && current.totalCount > 0 ? (current.pageNumber - 1) * this.pageSize() + 1 : 0;
  });

  protected readonly rangeEnd = computed(() => {
    const current = this.page();
    return current ? Math.min(current.pageNumber * this.pageSize(), current.totalCount) : 0;
  });

  private readonly refresh$ = new Subject<void>();
  private readonly searchTyped$ = new Subject<string>();

  constructor() {
    // Every refresh cancels the request still in flight (switchMap), so a slow old answer can never
    // overwrite a newer one when the user types or clicks quickly.
    this.refresh$
      .pipe(
        tap(() => this.loadStatus.set('loading')),
        switchMap(() =>
          this.productsService.list(this.currentQuery()).pipe(
            map((page) => ({ page, error: null as string | null })),
            catchError((error: unknown) => of({ page: null, error: parseApiError(error).message })),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(({ page, error }) => {
        if (page) {
          this.page.set(page);
          this.loadStatus.set('loaded');
        } else {
          this.loadError.set(error ?? 'The product list could not be loaded.');
          this.loadStatus.set('error');
        }
      });

    // Wait until the user pauses typing before asking the server, instead of one request per keystroke.
    this.searchTyped$
      .pipe(debounceTime(SEARCH_DEBOUNCE_MS), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((text) => {
        this.search.set(text);
        this.pageNumber.set(1);
        this.refresh$.next();
      });
  }

  ngOnInit(): void {
    this.productsService.getCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),   // the filter is a convenience; the list still works without it
    });

    this.refresh$.next();
  }

  // ---------------- user actions ----------------

  protected onSearchInput(event: Event): void {
    this.searchTyped$.next((event.target as HTMLInputElement).value);
  }

  protected onCategoryChange(event: Event): void {
    this.category.set((event.target as HTMLSelectElement).value);
    this.restartFromFirstPage();
  }

  protected onStatusChange(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value as StatusFilter);
    this.restartFromFirstPage();
  }

  protected onPageSizeChange(event: Event): void {
    this.pageSize.set(Number((event.target as HTMLSelectElement).value));
    this.restartFromFirstPage();
  }

  /** Clicking the active column flips the direction; clicking another column sorts by it, ascending. */
  protected sortByField(field: ProductSortField): void {
    if (this.sortBy() === field) {
      this.descending.set(!this.descending());
    } else {
      this.sortBy.set(field);
      this.descending.set(false);
    }

    this.restartFromFirstPage();
  }

  protected goToPage(pageNumber: number): void {
    this.pageNumber.set(pageNumber);
    this.refresh$.next();
  }

  protected retry(): void {
    this.refresh$.next();
  }

  protected toggleActive(product: ProductListItem): void {
    this.busyProductId.set(product.id);
    this.notice.set(null);

    const request$ = product.isActive
      ? this.productsService.deactivate(product.id, product.version)
      : this.productsService.activate(product.id, product.version);

    request$.subscribe({
      next: () => {
        this.busyProductId.set(null);
        this.notice.set({
          kind: 'success',
          text: `${product.name} was ${product.isActive ? 'deactivated' : 'reactivated'}.`,
        });
        this.refresh$.next();
      },
      error: (error: unknown) => {
        const apiError = parseApiError(error);
        this.busyProductId.set(null);

        if (apiError.status === 409) {
          this.notice.set({
            kind: 'error',
            text: `${product.name} was changed by someone else. The list has been refreshed. Please try again.`,
          });
          this.refresh$.next();
        } else {
          this.notice.set({ kind: 'error', text: apiError.message });
        }
      },
    });
  }

  // ---------------- template helpers ----------------

  protected ariaSort(field: ProductSortField): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== field) return 'none';
    return this.descending() ? 'descending' : 'ascending';
  }

  protected sortIndicator(field: ProductSortField): string {
    if (this.sortBy() !== field) return '';
    return this.descending() ? '▼' : '▲';
  }

  protected hasActiveFilters(): boolean {
    return this.search() !== '' || this.category() !== '' || this.statusFilter() !== 'all';
  }

  // ---------------- internals ----------------

  private restartFromFirstPage(): void {
    this.pageNumber.set(1);
    this.refresh$.next();
  }

  private currentQuery(): ProductListQuery {
    const status = this.statusFilter();

    return {
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
      search: this.search(),
      category: this.category(),
      isActive: status === 'all' ? null : status === 'active',
      sortBy: this.sortBy(),
      descending: this.descending(),
    };
  }
}