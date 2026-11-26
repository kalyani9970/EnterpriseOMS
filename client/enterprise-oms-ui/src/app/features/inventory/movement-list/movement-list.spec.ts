import { formatDate } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { PagedResult, StockMovement } from '../../../core/models/inventory.models';
import { textOf } from '../../../core/testing/dom-helpers';
import { parseUtc } from '../../../core/utils/dates';
import { MovementList } from './movement-list';

describe('MovementList', () => {
  let http: HttpTestingController;
  let api: string;
  let harness: RouterTestingHarness;
  let root: HTMLElement;

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
    quantityAfter: 10,
    reference: null,
    notes: null,
    created: '2026-10-17T09:30:00',
    createdBy: '5',
    ...overrides,
  });

  const page = (
    items: StockMovement[],
    overrides: Partial<PagedResult<StockMovement>> = {},
  ): PagedResult<StockMovement> => ({
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

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'inventory/movements', component: MovementList },
          { path: 'inventory', component: MovementList },
        ]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => {
    // If a test fails with a request still open, verify() throws. Resetting the module in a finally block means that
    // one failure stays ONE failure, instead of breaking every test that runs after it.
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  const movementsRequest = (): TestRequest =>
    http.expectOne((request) => request.url === `${api}/inventory/movements`);
  const warehousesRequest = (): TestRequest => http.expectOne((request) => request.url === `${api}/warehouses`);

  /** Navigates to the screen (optionally with a query string) without answering anything yet. */
  async function navigate(url = '/inventory/movements'): Promise<void> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, MovementList);
    root = harness.routeNativeElement as HTMLElement;
    harness.detectChanges();
  }

  /** Navigates, then answers the warehouses and the first page of the ledger. */
  async function open(url = '/inventory/movements', firstPage: PagedResult<StockMovement> = page([movement()])): Promise<void> {
    await navigate(url);
    warehousesRequest().flush(warehousesPage);
    movementsRequest().flush(firstPage);
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  const click = (element: Element | null) => (element as HTMLElement).click();
  const buttonWithText = (text: string) =>
    Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text) ?? null;

  function choose(selector: string, value: string): void {
    const select = root.querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  const cells = (row = 0) => Array.from(root.querySelectorAll('tbody tr')[row].children) as HTMLElement[];

  // ---------- showing the ledger ----------

  it('shows a loading message, then the ledger lines', async () => {
    await navigate();
    expect(textOf(root)).toContain('Loading the ledger…');

    warehousesRequest().flush(warehousesPage);
    movementsRequest().flush(page([movement({ productSku: 'MSE-011', productName: 'Wireless Mouse' })]));
    await harness.fixture.whenStable();
    harness.detectChanges();

    const text = textOf(root);
    expect(text).toContain('MSE-011');
    expect(text).toContain('Wireless Mouse');
    expect(text).toContain('WH-MAIN');
    expect(text).not.toContain('Loading the ledger…');
  });

  it('describes each line in plain words: the kind, the signed change, and the running balance', async () => {
    await open(
      '/inventory/movements',
      page([
        movement({ id: 3, type: 'Issue', quantityChange: -3, quantityAfter: 7 }),
        movement({ id: 2, type: 'Adjustment', quantityChange: 2, quantityAfter: 10 }),
        movement({ id: 1, type: 'Receipt', quantityChange: 8, quantityAfter: 8 }),
      ]),
    );

    expect(cells(0)[3].textContent?.trim()).toBe('Issued');
    expect(cells(0)[4].textContent?.trim()).toBe('−3');
    expect(cells(0)[5].textContent?.trim()).toBe('7');
    expect(cells(1)[3].textContent?.trim()).toBe('Counted');
    expect(cells(1)[4].textContent?.trim()).toBe('+2');
    expect(cells(2)[3].textContent?.trim()).toBe('Received');
    expect(cells(2)[4].textContent?.trim()).toBe('+8');
  });

  it('colours stock coming in green and stock going out red', async () => {
    await open(
      '/inventory/movements',
      page([movement({ id: 2, quantityChange: -3, quantityAfter: 7 }), movement({ id: 1, quantityChange: 10 })]),
    );

    expect(cells(0)[4].classList.contains('text-red-700')).toBe(true);
    expect(cells(1)[4].classList.contains('text-green-700')).toBe(true);
  });

  it('shows the reference, the notes and who recorded the line', async () => {
    await open(
      '/inventory/movements',
      page([
        movement({ id: 2, reference: 'PO-5001', notes: 'First delivery', createdBy: '5' }),
        movement({ id: 1, reference: null, notes: null, createdBy: null }),
      ]),
    );

    expect(textOf(cells(0)[6])).toContain('PO-5001');
    expect(textOf(cells(0)[6])).toContain('First delivery');
    expect(textOf(cells(0)[7])).toBe('User #5');
    expect(textOf(cells(1)[7])).toBe('—');
  });

  it('reads the date as UTC even when the API leaves the "Z" off', async () => {
    await open('/inventory/movements', page([movement({ created: '2026-10-17T09:30:00' })]));

    // Angular's date formatting puts a narrow no-break space before "AM"/"PM"; textOf turns every space into a plain one.
    const expected = formatDate(parseUtc('2026-10-17T09:30:00'), 'medium', 'en-US').replace(/\s/g, ' ');

    expect(textOf(cells(0)[0])).toBe(expected);
  });

  it('asks the server for the newest 20 lines with no filters', async () => {
    await navigate();
    warehousesRequest().flush(warehousesPage);

    const request = movementsRequest();
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.has('productId')).toBe(false);
    expect(request.request.params.has('warehouseId')).toBe(false);
    expect(request.request.params.has('type')).toBe(false);
    request.flush(page([]));
  });

  it('links back to the stock overview and stays on the ledger', async () => {
    await open();

    const links = Array.from(root.querySelectorAll('nav a')).map((a) => a.getAttribute('href'));

    expect(links).toEqual(['/inventory', '/inventory/movements']);
  });

  // ---------- opened from a stock row's History link ----------

  it('applies the filters passed in the address', async () => {
    await navigate('/inventory/movements?productId=3&warehouseId=10&type=Issue');
    warehousesRequest().flush(warehousesPage);

    const request = movementsRequest();
    expect(request.request.params.get('productId')).toBe('3');
    expect(request.request.params.get('warehouseId')).toBe('10');
    expect(request.request.params.get('type')).toBe('Issue');
    request.flush(page([movement({ type: 'Issue', quantityChange: -1, quantityAfter: 9 })]));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(root.querySelector<HTMLSelectElement>('#kind')!.value).toBe('Issue');
    expect(root.querySelector<HTMLSelectElement>('#warehouseFilter')!.value).toBe('10');
  });

  it('names the product it is narrowed to once the lines arrive (and its number until then)', async () => {
    await navigate('/inventory/movements?productId=3');
    warehousesRequest().flush(warehousesPage);
    expect(textOf(root)).toContain('Product #3');

    movementsRequest().flush(page([movement({ productId: 3, productSku: 'LAP-001', productName: 'Business Laptop' })]));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(textOf(root)).toContain('LAP-001 · Business Laptop');
    expect(textOf(root)).not.toContain('Product #3');
  });

  it('ignores filters in the address that make no sense', async () => {
    await navigate('/inventory/movements?productId=abc&warehouseId=-4&type=Bogus');
    warehousesRequest().flush(warehousesPage);

    const request = movementsRequest();
    expect(request.request.params.has('productId')).toBe(false);
    expect(request.request.params.has('warehouseId')).toBe(false);
    expect(request.request.params.has('type')).toBe(false);
    request.flush(page([]));
  });

  it('shows the whole ledger again when the product filter is cleared', async () => {
    await open('/inventory/movements?productId=3');

    click(root.querySelector('button[aria-label="Show all products"]'));

    const request = movementsRequest();
    expect(request.request.params.has('productId')).toBe(false);
    request.flush(page([]));
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(root.querySelector('button[aria-label="Show all products"]')).toBeNull();
  });

  // ---------- filters, paging ----------

  it('filters by kind of movement and goes back to the first page, and removes the filter again', async () => {
    await open();

    choose('#kind', 'Receipt');
    const filtered = movementsRequest();
    expect(filtered.request.params.get('type')).toBe('Receipt');
    expect(filtered.request.params.get('pageNumber')).toBe('1');
    filtered.flush(page([]));

    choose('#kind', '');
    const unfiltered = movementsRequest();
    expect(unfiltered.request.params.has('type')).toBe(false);
    unfiltered.flush(page([]));
  });

  it('filters by warehouse', async () => {
    await open();

    choose('#warehouseFilter', '11');

    const request = movementsRequest();
    expect(request.request.params.get('warehouseId')).toBe('11');
    request.flush(page([]));
  });

  it('hides the warehouse filter (and still works) when the warehouses cannot be loaded', async () => {
    await navigate();

    warehousesRequest().flush({}, { status: 403, statusText: 'Forbidden' });
    movementsRequest().flush(page([movement()]));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(root.querySelector('#warehouseFilter')).toBeNull();
    expect(textOf(root)).toContain('LAP-001');
  });

  it('shows the range and moves between pages, disabling Previous and Next at the ends', async () => {
    await open('/inventory/movements', page([movement()], { pageNumber: 1, totalPages: 2, totalCount: 30, hasNextPage: true }));
    expect(textOf(root)).toContain('Showing 1–20 of 30');
    expect(buttonWithText('Previous')!.disabled).toBe(true);

    click(buttonWithText('Next'));
    const request = movementsRequest();
    expect(request.request.params.get('pageNumber')).toBe('2');
    request.flush(page([movement()], { pageNumber: 2, totalPages: 2, totalCount: 30, hasPreviousPage: true }));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(buttonWithText('Next')!.disabled).toBe(true);
    expect(buttonWithText('Previous')!.disabled).toBe(false);
  });

  it('changes the page size and returns to the first page', async () => {
    await open();

    choose('#pageSize', '50');

    const request = movementsRequest();
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('cancels a request that has been overtaken, so an old slow answer cannot replace a newer one', async () => {
    await open();

    choose('#kind', 'Receipt');
    const slow = movementsRequest();
    choose('#kind', 'Issue');
    const latest = movementsRequest();

    expect(slow.cancelled).toBe(true);

    latest.flush(page([movement({ productName: 'The Latest Answer' })]));
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(textOf(root)).toContain('The Latest Answer');
  });

  // ---------- empty and error states ----------

  it('says there are no movements yet when the ledger is empty', async () => {
    await open('/inventory/movements', page([]));

    expect(textOf(root)).toContain('No stock movements yet.');
  });

  it('says no movement matches when filters find nothing', async () => {
    await open('/inventory/movements?type=Issue', page([]));

    expect(textOf(root)).toContain('No movements match these filters.');
    expect(textOf(root)).not.toContain('No stock movements yet.');
  });

  it('shows the load error and retries on demand', async () => {
    await navigate();
    warehousesRequest().flush(warehousesPage);

    movementsRequest().flush({}, { status: 500, statusText: 'Server Error' });
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(textOf(root)).toContain('The server ran into a problem.');

    click(buttonWithText('Try again'));
    movementsRequest().flush(page([movement()]));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(textOf(root)).toContain('LAP-001');
  });
});