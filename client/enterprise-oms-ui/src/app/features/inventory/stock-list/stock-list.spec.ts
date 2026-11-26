import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../../core/config/api-config';
import { PagedResult, StockLevelItem, StockMovement } from '../../../core/models/inventory.models';
import { AuthService } from '../../../core/services/auth.service';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { StockList } from './stock-list';

describe('StockList', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<StockList>;

  const item = (overrides: Partial<StockLevelItem> = {}): StockLevelItem => ({
    productId: 3,
    productSku: 'LAP-001',
    productName: 'Business Laptop',
    unitOfMeasure: 'Piece',
    warehouseId: 10,
    warehouseCode: 'WH-MAIN',
    warehouseName: 'Main Warehouse',
    quantityOnHand: 12,
    reorderLevel: 5,
    isLowStock: false,
    ...overrides,
  });

  const page = (
    items: StockLevelItem[],
    overrides: Partial<PagedResult<StockLevelItem>> = {},
  ): PagedResult<StockLevelItem> => ({
    items,
    pageNumber: 1,
    totalPages: 1,
    totalCount: items.length,
    hasPreviousPage: false,
    hasNextPage: false,
    ...overrides,
  });

  const warehousesPage = {
    items: [
      { id: 10, code: 'WH-MAIN', name: 'Main Warehouse' },
      { id: 11, code: 'WH-EAST', name: 'East Warehouse' },
    ],
    pageNumber: 1,
    totalPages: 1,
    totalCount: 2,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  const movement = (overrides: Partial<StockMovement> = {}): StockMovement => ({
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

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => {
    vi.useRealTimers();

    // If a test fails with a request still open, verify() throws. Resetting the module in a finally block means that
    // one failure stays ONE failure, instead of breaking every test that runs after it.
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  const stockRequest = (): TestRequest => http.expectOne((request) => request.url === `${api}/inventory/stock`);
  const warehousesRequest = (): TestRequest => http.expectOne((request) => request.url === `${api}/warehouses`);
  const root = () => fixture.nativeElement as HTMLElement;

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  /** Creates the screen and answers its two initial requests (the warehouses for the filter, then the first page of stock). */
  async function open(
    firstPage: PagedResult<StockLevelItem> = page([item()]),
    permissions: string[] = ['inventory.view'],
  ): Promise<HTMLElement> {
    signIn(permissions);

    fixture = TestBed.createComponent(StockList);
    fixture.detectChanges();

    warehousesRequest().flush(warehousesPage);
    stockRequest().flush(firstPage);
    await fixture.whenStable();

    return root();
  }

  const ADJUSTER = ['inventory.view', 'inventory.adjust'];
  const click = (element: Element | null) => (element as HTMLElement).click();
  const buttonWithText = (text: string) =>
    Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim() === text) ?? null;
  /** A sortable column header's button. Its text carries the sort arrow ("SKU ▲"), so match on the label's start. */
  const headerButton = (label: string) =>
    Array.from(root().querySelectorAll<HTMLButtonElement>('th button')).find((b) => b.textContent?.trim().startsWith(label)) ?? null;
  const byLabel = (label: string) => root().querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);

  function choose(selector: string, value: string): void {
    const select = root().querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  // ---------- loading and showing ----------

  it('shows a loading message, then the stock', async () => {
    signIn(['inventory.view']);
    fixture = TestBed.createComponent(StockList);
    fixture.detectChanges();
    expect(textOf(root())).toContain('Loading stock…');

    warehousesRequest().flush(warehousesPage);
    stockRequest().flush(page([item()]));
    await fixture.whenStable();

    const text = textOf(root());
    expect(text).toContain('LAP-001');
    expect(text).toContain('Business Laptop');
    expect(text).toContain('WH-MAIN');
    expect(text).toContain('12');
    expect(text).toContain('Piece');
    expect(text).not.toContain('Loading stock…');
  });

  it('asks the server for the first page, sorted by SKU, with no filters', async () => {
    signIn(['inventory.view']);
    fixture = TestBed.createComponent(StockList);
    fixture.detectChanges();
    warehousesRequest().flush(warehousesPage);

    const request = stockRequest();
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('sortBy')).toBe('sku');
    expect(request.request.params.get('descending')).toBe('false');
    expect(request.request.params.get('lowStockOnly')).toBe('false');
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('warehouseId')).toBe(false);
    request.flush(page([]));
  });

  it('says whether each row is sold out, running low, or fine', async () => {
    await open(
      page([
        item({ productSku: 'A-OUT', quantityOnHand: 0, isLowStock: true }),
        item({ productSku: 'B-LOW', quantityOnHand: 5, isLowStock: true }),
        item({ productSku: 'C-OK', quantityOnHand: 12, isLowStock: false }),
      ]),
    );

    const statuses = Array.from(root().querySelectorAll('tbody tr')).map((row) => row.children[5].textContent?.trim());

    expect(statuses).toEqual(['Out of stock', 'Low', 'OK']);
  });

  it('shows the range and page position', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 3, totalCount: 45, hasNextPage: true }));

    expect(textOf(root())).toContain('Showing 1–20 of 45');
    expect(textOf(root())).toContain('Page 1 of 3');
  });

  it('points a user who may receive stock at the first delivery when there is none yet', async () => {
    await open(page([]), ADJUSTER);

    expect(textOf(root())).toContain('No stock recorded yet.');
    expect(textOf(root())).toContain('Use Receive stock to book in the first delivery.');
  });

  it('does not suggest receiving stock to a user who may only view', async () => {
    await open(page([]), ['inventory.view']);

    expect(textOf(root())).toContain('No stock recorded yet.');
    expect(textOf(root())).not.toContain('Receive stock');
  });

  it('shows the load error and retries on demand', async () => {
    signIn(['inventory.view']);
    fixture = TestBed.createComponent(StockList);
    fixture.detectChanges();
    warehousesRequest().flush(warehousesPage);

    stockRequest().flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    expect(textOf(root())).toContain('The server ran into a problem.');

    click(buttonWithText('Try again'));
    stockRequest().flush(page([item()]));
    await fixture.whenStable();

    expect(textOf(root())).toContain('LAP-001');
  });

  // ---------- search and filters ----------

  it('offers the warehouses as a filter', async () => {
    await open();

    expect(Array.from(root().querySelectorAll('#warehouseFilter option')).map((o) => o.textContent?.trim())).toEqual([
      'All warehouses',
      'WH-MAIN · Main Warehouse',
      'WH-EAST · East Warehouse',
    ]);
  });

  it('hides the warehouse filter (and still works) when the warehouses cannot be loaded', async () => {
    signIn(['inventory.view']);
    fixture = TestBed.createComponent(StockList);
    fixture.detectChanges();

    warehousesRequest().flush({}, { status: 403, statusText: 'Forbidden' });
    stockRequest().flush(page([item()]));
    await fixture.whenStable();

    expect(root().querySelector('#warehouseFilter')).toBeNull();
    expect(textOf(root())).toContain('LAP-001');
  });

  it('filters by warehouse and goes back to the first page', async () => {
    await open();

    choose('#warehouseFilter', '11');

    const request = stockRequest();
    expect(request.request.params.get('warehouseId')).toBe('11');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('shows only low stock when asked', async () => {
    await open();

    const checkbox = root().querySelector<HTMLInputElement>('#lowStock')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));

    const request = stockRequest();
    expect(request.request.params.get('lowStockOnly')).toBe('true');
    request.flush(page([]));
  });

  it('waits until the user pauses typing before searching, then searches once from page 1', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 3, totalCount: 45, hasNextPage: true }));
    click(buttonWithText('Next'));
    stockRequest().flush(page([item()], { pageNumber: 2, totalPages: 3, totalCount: 45, hasPreviousPage: true, hasNextPage: true }));
    await fixture.whenStable();

    vi.useFakeTimers();
    for (const text of ['l', 'la', 'lap']) {
      typeInto(root(), '#search', text);
      vi.advanceTimersByTime(100);
    }

    http.expectNone((request) => request.url === `${api}/inventory/stock`);   // still inside the pause window

    vi.advanceTimersByTime(300);
    const request = stockRequest();   // exactly ONE request for the three keystrokes
    expect(request.request.params.get('search')).toBe('lap');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('shows "no match" (not "no stock yet") when filters find nothing', async () => {
    await open();

    const checkbox = root().querySelector<HTMLInputElement>('#lowStock')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    stockRequest().flush(page([]));
    await fixture.whenStable();

    expect(textOf(root())).toContain('No stock matches your search or filters.');
    expect(textOf(root())).not.toContain('No stock recorded yet.');
  });

  // ---------- sorting and paging ----------

  it('sorts by a column on click, and flips the direction on a second click', async () => {
    await open();

    click(headerButton('SKU'));
    const first = stockRequest();
    expect(first.request.params.get('sortBy')).toBe('sku');
    expect(first.request.params.get('descending')).toBe('true');   // SKU was already the sort column, so it flips
    first.flush(page([item()]));
    await fixture.whenStable();
    expect(root().querySelector('th[aria-sort="descending"]')).not.toBeNull();

    click(headerButton('SKU'));
    const second = stockRequest();
    expect(second.request.params.get('descending')).toBe('false');
    second.flush(page([item()]));
  });

  it('starts a different column in ascending order', async () => {
    await open();

    click(headerButton('On hand'));

    const request = stockRequest();
    expect(request.request.params.get('sortBy')).toBe('quantity');
    expect(request.request.params.get('descending')).toBe('false');
    request.flush(page([item()]));
  });

  it('moves between pages with Previous and Next, and disables them at the ends', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 2, totalCount: 30, hasNextPage: true }));
    expect(buttonWithText('Previous')!.disabled).toBe(true);
    expect(buttonWithText('Next')!.disabled).toBe(false);

    click(buttonWithText('Next'));
    const request = stockRequest();
    expect(request.request.params.get('pageNumber')).toBe('2');
    request.flush(page([item()], { pageNumber: 2, totalPages: 2, totalCount: 30, hasPreviousPage: true }));
    await fixture.whenStable();

    expect(buttonWithText('Next')!.disabled).toBe(true);
    expect(buttonWithText('Previous')!.disabled).toBe(false);
  });

  it('changes the page size and returns to the first page', async () => {
    await open();

    choose('#pageSize', '50');

    const request = stockRequest();
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('cancels a request that has been overtaken, so an old slow answer cannot replace a newer one', async () => {
    await open();

    choose('#warehouseFilter', '10');
    const slow = stockRequest();
    choose('#warehouseFilter', '11');
    const latest = stockRequest();

    expect(slow.cancelled).toBe(true);

    latest.flush(page([item({ productName: 'East Item' })]));
    await fixture.whenStable();
    expect(textOf(root())).toContain('East Item');
  });

  // ---------- permissions and row actions ----------

  it('shows only the History link to a user who may view but not change stock', async () => {
    await open(page([item()]), ['inventory.view']);

    expect(buttonWithText('Receive stock')).toBeNull();
    expect(byLabel('Receive LAP-001 into WH-MAIN')).toBeNull();
    expect(byLabel('Issue LAP-001 from WH-MAIN')).toBeNull();
    expect(byLabel('Count LAP-001 in WH-MAIN')).toBeNull();
    expect(root().querySelector('a[aria-label="History of LAP-001 in WH-MAIN"]')).not.toBeNull();
  });

  it('shows Receive, Issue and Count (and the header button) to a user who may change stock', async () => {
    await open(page([item()]), ADJUSTER);

    expect(buttonWithText('Receive stock')).not.toBeNull();
    expect(byLabel('Receive LAP-001 into WH-MAIN')).not.toBeNull();
    expect(byLabel('Issue LAP-001 from WH-MAIN')).not.toBeNull();
    expect(byLabel('Count LAP-001 in WH-MAIN')).not.toBeNull();
  });

  it('cannot issue from a row with nothing on hand', async () => {
    await open(page([item({ quantityOnHand: 0, isLowStock: true })]), ADJUSTER);

    expect(byLabel('Issue LAP-001 from WH-MAIN')!.disabled).toBe(true);
    expect(byLabel('Receive LAP-001 into WH-MAIN')!.disabled).toBe(false);
  });

  it('links History to the ledger, already narrowed to that product and warehouse', async () => {
    await open(page([item({ productId: 3, warehouseId: 10 })]), ['inventory.view']);

    const link = root().querySelector<HTMLAnchorElement>('a[aria-label="History of LAP-001 in WH-MAIN"]')!;

    expect(link.getAttribute('href')).toBe('/inventory/movements?productId=3&warehouseId=10');
  });

  // ---------- the movement form, on this screen ----------

  it('opens the form for a row, showing what it is about and how many are on hand, and closes it on Cancel', async () => {
    await open(page([item({ quantityOnHand: 12 })]), ADJUSTER);

    click(byLabel('Issue LAP-001 from WH-MAIN'));
    fixture.detectChanges();

    const form = root().querySelector<HTMLElement>('form[aria-label="Issue stock"]')!;
    expect(form).not.toBeNull();
    expect(textOf(form)).toContain('LAP-001');
    expect(textOf(form)).toContain('WH-MAIN');
    expect(textOf(form)).toContain('On hand now:');
    expect(textOf(form)).toContain('12');

    click(buttonWithText('Cancel'));
    fixture.detectChanges();
    expect(root().querySelector('form')).toBeNull();
  });

  it('after a receipt: closes the form, confirms in plain words, and refreshes the numbers', async () => {
    await open(page([item({ quantityOnHand: 12 })]), ADJUSTER);

    click(byLabel('Receive LAP-001 into WH-MAIN'));
    fixture.detectChanges();
    typeInto(root(), '#quantity', '10');
    submitForm(root());

    const receipt = http.expectOne(`${api}/inventory/receipts`);
    expect(receipt.request.body).toMatchObject({ productId: 3, warehouseId: 10, quantity: 10 });
    receipt.flush(movement({ type: 'Receipt', quantityChange: 10, quantityAfter: 22 }));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Received 10 × LAP-001 into WH-MAIN. On hand: 22.');
    expect(root().querySelector('form')).toBeNull();

    stockRequest().flush(page([item({ quantityOnHand: 22 })]));   // the list reloads, so the new number shows
    await fixture.whenStable();
    expect(textOf(root().querySelector<HTMLElement>('tbody')!)).toContain('22');
  });

  it('after an issue: confirms how many were issued', async () => {
    await open(page([item({ quantityOnHand: 12 })]), ADJUSTER);

    click(byLabel('Issue LAP-001 from WH-MAIN'));
    fixture.detectChanges();
    typeInto(root(), '#quantity', '3');
    submitForm(root());

    http.expectOne(`${api}/inventory/issues`).flush(movement({ type: 'Issue', quantityChange: -3, quantityAfter: 9 }));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Issued 3 × LAP-001 from WH-MAIN. On hand: 9.');
    stockRequest().flush(page([item({ quantityOnHand: 9 })]));
  });

  it('after a count: confirms the number counted and the difference it found', async () => {
    await open(page([item({ quantityOnHand: 12 })]), ADJUSTER);

    click(byLabel('Count LAP-001 in WH-MAIN'));
    fixture.detectChanges();
    typeInto(root(), '#countedQuantity', '9');
    typeInto(root(), '#reason', 'Cycle count');
    submitForm(root());

    http.expectOne(`${api}/inventory/adjustments`).flush(movement({ type: 'Adjustment', quantityChange: -3, quantityAfter: 9 }));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Counted LAP-001 in WH-MAIN: 9 on hand (−3).');
    stockRequest().flush(page([item({ quantityOnHand: 9 })]));
  });

  it('on a conflict: keeps the form open, refreshes the list, and shows the number as it is now', async () => {
    await open(page([item({ quantityOnHand: 5 })]), ADJUSTER);

    click(byLabel('Issue LAP-001 from WH-MAIN'));
    fixture.detectChanges();
    expect(textOf(root().querySelector<HTMLElement>('form')!)).toContain('5');
    typeInto(root(), '#quantity', '2');
    submitForm(root());

    http.expectOne(`${api}/inventory/issues`).flush(
      { detail: 'Stock (3) was changed by someone else.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    // The list reloads, and somebody else has meanwhile received more stock.
    stockRequest().flush(page([item({ quantityOnHand: 9 })]));
    await fixture.whenStable();
    fixture.detectChanges();

    const form = root().querySelector<HTMLElement>('form[aria-label="Issue stock"]')!;
    expect(form).not.toBeNull();
    expect(textOf(form)).toContain('Someone else changed this stock at the same moment.');
    expect(textOf(form)).toContain('On hand now:');
    expect(textOf(form)).toContain('9');
  });

  it('opens a form to receive stock that was never here before, with a product and warehouse to choose', async () => {
    await open(page([item()]), ADJUSTER);

    click(buttonWithText('Receive stock'));
    fixture.detectChanges();

    const request = warehousesRequest();   // the form asks for the (active) warehouses to choose from
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush(warehousesPage);
    fixture.detectChanges();

    expect(root().querySelector('form[aria-label="Receive stock"]')).not.toBeNull();
    expect(root().querySelector('#product-search')).not.toBeNull();
    expect(root().querySelector('#warehouse')).not.toBeNull();
  });

  it('shows one form at a time: opening another row replaces the first', async () => {
    await open(
      page([item({ productSku: 'LAP-001' }), item({ productId: 4, productSku: 'MSE-011', productName: 'Wireless Mouse' })]),
      ADJUSTER,
    );

    click(byLabel('Receive LAP-001 into WH-MAIN'));
    fixture.detectChanges();
    click(byLabel('Count MSE-011 in WH-MAIN'));
    fixture.detectChanges();

    expect(root().querySelectorAll('form')).toHaveLength(1);
    const form = root().querySelector<HTMLElement>('form')!;
    expect(form.getAttribute('aria-label')).toBe('Count stock');
    expect(textOf(form)).toContain('MSE-011');
    expect(root().querySelector('#countedQuantity')).not.toBeNull();   // a fresh form for the new mode, not the old one
  });
});