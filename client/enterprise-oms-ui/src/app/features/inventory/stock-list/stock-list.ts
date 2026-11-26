import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import {
  MovementMode,
  PagedResult,
  ProductRef,
  StockLevelItem,
  StockLevelQuery,
  StockMovement,
  StockSortField,
  WarehouseRef,
} from '../../../core/models/inventory.models';
import { Permissions } from '../../../core/security/permissions';
import { AuthService } from '../../../core/services/auth.service';
import { InventoryService } from '../../../core/services/inventory.service';
import { WarehousesService } from '../../../core/services/warehouses.service';
import { parseApiError } from '../../../core/utils/api-error';
import { StockStatus, describeMovement, stockStatus } from '../../../core/utils/inventory';
import { StockMovementForm } from '../stock-movement-form/stock-movement-form';

type LoadStatus = 'loading' | 'loaded' | 'error';

/** The movement form currently open, and what it was opened for. The key makes a fresh form for every opening. */
interface PanelState {
  key: number;
  mode: MovementMode;
  product: ProductRef | null;
  warehouse: WarehouseRef | null;
}

const SEARCH_DEBOUNCE_MS = 300;

/**
 * The stock overview: how much of each product is in each warehouse, with low stock called out. Search,
 * filter, sort and paging are done by the server. Receiving, issuing and counting open a form right on this
 * screen, and after each one the list refreshes so the new numbers are what the user sees.
 */
@Component({
  selector: 'app-stock-list',
  imports: [RouterLink, RouterLinkActive, StockMovementForm],
  templateUrl: './stock-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockList implements OnInit {
  private readonly inventory = inject(InventoryService);
  private readonly warehousesService = inject(WarehousesService);
  private readonly auth = inject(AuthService);

  protected readonly canAdjust = computed(() => this.auth.hasPermission(Permissions.Inventory.Adjust));

  // ---- what the user asked to see ----
  protected readonly search = signal('');
  protected readonly warehouseId = signal<number | null>(null);
  protected readonly lowStockOnly = signal(false);
  protected readonly sortBy = signal<StockSortField>('sku');
  protected readonly descending = signal(false);
  protected readonly pageNumber = signal(1);
  protected readonly pageSize = signal(20);

  // ---- what we got back ----
  protected readonly page = signal<PagedResult<StockLevelItem> | null>(null);
  protected readonly loadStatus = signal<LoadStatus>('loading');
  protected readonly loadError = signal('');
  protected readonly warehouses = signal<WarehouseRef[]>([]);

  // ---- feedback and the open form ----
  protected readonly notice = signal<string | null>(null);
  protected readonly panel = signal<PanelState | null>(null);
  protected readonly panels = computed(() => {
    const current = this.panel();
    return current ? [current] : [];
  });

  /** The number on hand for the row the open form is about, kept fresh by the refresh after a conflict. */
  protected readonly panelQuantity = computed(() => {
    const current = this.panel();
    const items = this.page()?.items;

    if (!current || !current.product || !current.warehouse || !items) return null;

    const product = current.product;
    const warehouse = current.warehouse;

    return items.find((i) => i.productId === product.id && i.warehouseId === warehouse.id)?.quantityOnHand ?? null;
  });

  protected readonly pageSizes = [10, 20, 50];

  protected readonly rangeStart = computed(() => {
    const current = this.page();
    return current && current.totalCount > 0 ? (current.pageNumber - 1) * this.pageSize() + 1 : 0;
  });

  protected readonly rangeEnd = computed(() => {
    const current = this.page();
    return current ? Math.min(current.pageNumber * this.pageSize(), current.totalCount) : 0;
  });

  private panelCounter = 0;
  private readonly refresh$ = new Subject<void>();
  private readonly searchTyped$ = new Subject<string>();

  constructor() {
    // Every refresh cancels the request still in flight (switchMap), so a slow old answer can never
    // overwrite a newer one when the user types or clicks quickly.
    this.refresh$
      .pipe(
        tap(() => this.loadStatus.set('loading')),
        switchMap(() =>
          this.inventory.getStock(this.currentQuery()).pipe(
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
          this.loadError.set(error ?? 'The stock list could not be loaded.');
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
    // The warehouse filter is a convenience: if the user may not list warehouses, the filter just stays hidden.
    this.warehousesService
      .list({ pageNumber: 1, pageSize: 100, sortBy: 'name', descending: false, isActive: null })
      .subscribe({
        next: (page) => this.warehouses.set(page.items.map((w) => ({ id: w.id, code: w.code, name: w.name }))),
        error: () => this.warehouses.set([]),
      });

    this.refresh$.next();
  }

  // ---------------- filters, sorting, paging ----------------

  protected onSearchInput(event: Event): void {
    this.searchTyped$.next((event.target as HTMLInputElement).value);
  }

  protected onWarehouseChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    this.warehouseId.set(Number.isInteger(value) && value > 0 ? value : null);
    this.restartFromFirstPage();
  }

  protected onLowStockChange(event: Event): void {
    this.lowStockOnly.set((event.target as HTMLInputElement).checked);
    this.restartFromFirstPage();
  }

  protected onPageSizeChange(event: Event): void {
    this.pageSize.set(Number((event.target as HTMLSelectElement).value));
    this.restartFromFirstPage();
  }

  /** Clicking the active column flips the direction; clicking another column sorts by it, ascending. */
  protected sortByField(field: StockSortField): void {
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

  // ---------------- receiving, issuing and counting ----------------

  /** Opens the form for one row (product and warehouse known), or, with no row, to receive stock that was never here before. */
  protected openForm(mode: MovementMode, row: StockLevelItem | null = null): void {
    this.notice.set(null);
    this.panel.set({
      key: ++this.panelCounter,
      mode,
      product: row ? { id: row.productId, sku: row.productSku, name: row.productName } : null,
      warehouse: row ? { id: row.warehouseId, code: row.warehouseCode, name: row.warehouseName } : null,
    });
  }

  protected closeForm(): void {
    this.panel.set(null);
  }

  protected onSaved(movement: StockMovement): void {
    this.panel.set(null);
    this.notice.set(describeMovement(movement));
    this.refresh$.next();
  }

  /** Someone changed the same stock at the same moment: refresh, so the form shows the number as it is now. */
  protected onConflict(): void {
    this.refresh$.next();
  }

  // ---------------- template helpers ----------------

  protected statusOf(item: StockLevelItem): StockStatus {
    return stockStatus(item);
  }

  protected ariaSort(field: StockSortField): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== field) return 'none';
    return this.descending() ? 'descending' : 'ascending';
  }

  protected sortIndicator(field: StockSortField): string {
    if (this.sortBy() !== field) return '';
    return this.descending() ? '▼' : '▲';
  }

  protected hasActiveFilters(): boolean {
    return this.search() !== '' || this.warehouseId() !== null || this.lowStockOnly();
  }

  // ---------------- internals ----------------

  private restartFromFirstPage(): void {
    this.pageNumber.set(1);
    this.refresh$.next();
  }

  private currentQuery(): StockLevelQuery {
    return {
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
      search: this.search(),
      warehouseId: this.warehouseId(),
      lowStockOnly: this.lowStockOnly(),
      sortBy: this.sortBy(),
      descending: this.descending(),
    };
  }
}