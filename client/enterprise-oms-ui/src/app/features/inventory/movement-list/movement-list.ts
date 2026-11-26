import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { Subject, catchError, map, of, switchMap, tap } from 'rxjs';
import {
  PagedResult,
  STOCK_MOVEMENT_TYPES,
  StockMovement,
  StockMovementQuery,
  StockMovementType,
  WarehouseRef,
} from '../../../core/models/inventory.models';
import { InventoryService } from '../../../core/services/inventory.service';
import { WarehousesService } from '../../../core/services/warehouses.service';
import { parseApiError } from '../../../core/utils/api-error';
import { parseUtc } from '../../../core/utils/dates';
import { MOVEMENT_TYPE_LABELS, formatChange } from '../../../core/utils/inventory';

type LoadStatus = 'loading' | 'loaded' | 'error';

/**
 * The stock ledger: every change to every stock level, newest first, each with the running balance it left
 * behind. It can be opened for one product in one warehouse (the "History" link on a stock row passes them in
 * the address), and narrowed by warehouse and by kind of movement. Ledger lines are never edited or deleted:
 * a mistake is corrected by a new line (a count), so this screen is a trustworthy audit trail.
 */
@Component({
  selector: 'app-movement-list',
  imports: [DatePipe, RouterLink, RouterLinkActive],
  templateUrl: './movement-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MovementList implements OnInit {
  private readonly inventory = inject(InventoryService);
  private readonly warehousesService = inject(WarehousesService);
  private readonly route = inject(ActivatedRoute);

  protected readonly kinds = STOCK_MOVEMENT_TYPES;
  protected readonly kindLabels = MOVEMENT_TYPE_LABELS;

  // ---- what the user asked to see ----
  protected readonly productId = signal<number | null>(null);
  protected readonly warehouseId = signal<number | null>(null);
  protected readonly type = signal<StockMovementType | null>(null);
  protected readonly pageNumber = signal(1);
  protected readonly pageSize = signal(20);

  // ---- what we got back ----
  protected readonly page = signal<PagedResult<StockMovement> | null>(null);
  protected readonly loadStatus = signal<LoadStatus>('loading');
  protected readonly loadError = signal('');
  protected readonly warehouses = signal<WarehouseRef[]>([]);

  protected readonly pageSizes = [10, 20, 50];

  /** Names the product the ledger is narrowed to: from the lines already loaded if possible, else its number. */
  protected readonly productLabel = computed(() => {
    const id = this.productId();

    if (id === null) return null;

    const match = this.page()?.items.find((m) => m.productId === id);

    return match ? `${match.productSku} · ${match.productName}` : `Product #${id}`;
  });

  protected readonly rangeStart = computed(() => {
    const current = this.page();
    return current && current.totalCount > 0 ? (current.pageNumber - 1) * this.pageSize() + 1 : 0;
  });

  protected readonly rangeEnd = computed(() => {
    const current = this.page();
    return current ? Math.min(current.pageNumber * this.pageSize(), current.totalCount) : 0;
  });

  private readonly refresh$ = new Subject<void>();

  constructor() {
    // Every refresh cancels the request still in flight (switchMap), so a slow old answer can never
    // overwrite a newer one.
    this.refresh$
      .pipe(
        tap(() => this.loadStatus.set('loading')),
        switchMap(() =>
          this.inventory.getMovements(this.currentQuery()).pipe(
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
          this.loadError.set(error ?? 'The ledger could not be loaded.');
          this.loadStatus.set('error');
        }
      });
  }

  ngOnInit(): void {
    // The "History" link on a stock row opens this screen already narrowed, through the address.
    const params = this.route.snapshot.queryParamMap;
    this.productId.set(toPositiveInt(params.get('productId')));
    this.warehouseId.set(toPositiveInt(params.get('warehouseId')));

    const kind = params.get('type');
    this.type.set(this.kinds.find((k) => k === kind) ?? null);

    // The warehouse filter is a convenience: if the user may not list warehouses, the filter just stays hidden.
    this.warehousesService
      .list({ pageNumber: 1, pageSize: 100, sortBy: 'name', descending: false, isActive: null })
      .subscribe({
        next: (page) => this.warehouses.set(page.items.map((w) => ({ id: w.id, code: w.code, name: w.name }))),
        error: () => this.warehouses.set([]),
      });

    this.refresh$.next();
  }

  // ---------------- filters and paging ----------------

  protected onKindChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;

    this.type.set(this.kinds.find((k) => k === value) ?? null);
    this.restartFromFirstPage();
  }

  protected onWarehouseChange(event: Event): void {
    this.warehouseId.set(toPositiveInt((event.target as HTMLSelectElement).value));
    this.restartFromFirstPage();
  }

  protected clearProduct(): void {
    this.productId.set(null);
    this.restartFromFirstPage();
  }

  protected onPageSizeChange(event: Event): void {
    this.pageSize.set(Number((event.target as HTMLSelectElement).value));
    this.restartFromFirstPage();
  }

  protected goToPage(pageNumber: number): void {
    this.pageNumber.set(pageNumber);
    this.refresh$.next();
  }

  protected retry(): void {
    this.refresh$.next();
  }

  // ---------------- template helpers ----------------

  protected when(movement: StockMovement): Date {
    return parseUtc(movement.created);
  }

  protected change(movement: StockMovement): string {
    return formatChange(movement.quantityChange);
  }

  protected hasActiveFilters(): boolean {
    return this.productId() !== null || this.warehouseId() !== null || this.type() !== null;
  }

  // ---------------- internals ----------------

  private restartFromFirstPage(): void {
    this.pageNumber.set(1);
    this.refresh$.next();
  }

  private currentQuery(): StockMovementQuery {
    return {
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
      productId: this.productId(),
      warehouseId: this.warehouseId(),
      type: this.type(),
    };
  }
}

function toPositiveInt(text: string | null): number | null {
  const value = Number(text);

  return text !== null && text !== '' && Number.isInteger(value) && value > 0 ? value : null;
}