import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { MovementMode, ProductRef, StockMovement, WarehouseRef } from '../../../core/models/inventory.models';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { StockMovementForm } from './stock-movement-form';

describe('StockMovementForm', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<StockMovementForm>;

  // What the form emitted, captured in plain arrays so the assertions read naturally.
  let saved: StockMovement[];
  let cancelled: unknown[];
  let conflict: unknown[];

  const laptop: ProductRef = { id: 3, sku: 'LAP-001', name: 'Business Laptop' };
  const main: WarehouseRef = { id: 10, code: 'WH-MAIN', name: 'Main Warehouse' };

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
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

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

  const root = () => fixture.nativeElement as HTMLElement;
  const field = (id: string) => root().querySelector<HTMLInputElement>(`#${id}`);
  const buttonWithText = (text: string) =>
    Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim() === text) ?? null;

  /** Opens the form for a known product and warehouse (the way a stock row opens it). */
  function open(
    mode: MovementMode,
    options: { currentQuantity?: number | null; product?: ProductRef | null; warehouse?: WarehouseRef | null } = {},
  ): void {
    fixture = TestBed.createComponent(StockMovementForm);
    fixture.componentRef.setInput('mode', mode);
    fixture.componentRef.setInput('product', 'product' in options ? options.product : laptop);
    fixture.componentRef.setInput('warehouse', 'warehouse' in options ? options.warehouse : main);
    fixture.componentRef.setInput('currentQuantity', options.currentQuantity ?? null);

    saved = [];
    cancelled = [];
    conflict = [];
    fixture.componentInstance.saved.subscribe((m) => saved.push(m));
    fixture.componentInstance.cancelled.subscribe(() => cancelled.push(true));
    fixture.componentInstance.conflict.subscribe(() => conflict.push(true));

    fixture.detectChanges();
  }

  const warehousesRequest = (): TestRequest => http.expectOne((request) => request.url === `${api}/warehouses`);

  /** Opens the form with nothing chosen yet (receiving stock that was never here before) and answers the warehouse list. */
  function openForPicking(): void {
    open('receive', { product: null, warehouse: null });
    warehousesRequest().flush({
      items: [
        { id: 10, code: 'WH-MAIN', name: 'Main Warehouse' },
        { id: 11, code: 'WH-EAST', name: 'East Warehouse' },
      ],
      pageNumber: 1,
      totalPages: 1,
      totalCount: 2,
      hasPreviousPage: false,
      hasNextPage: false,
    });
    fixture.detectChanges();
  }

  const productsPage = (items: ProductRef[]) => ({
    items: items.map((p) => ({ ...p, category: null, unitOfMeasure: 'Piece', unitPrice: 1, reorderLevel: 5, isActive: true, version: 'v1' })),
    pageNumber: 1,
    totalPages: 1,
    totalCount: items.length,
    hasPreviousPage: false,
    hasNextPage: false,
  });

  // ---------- what the form shows ----------

  it('says what it is for: the kind of movement, the product and the warehouse', () => {
    open('receive', { currentQuantity: 12 });

    const text = textOf(root());
    expect(text).toContain('Receive stock');
    expect(text).toContain('LAP-001');
    expect(text).toContain('Business Laptop');
    expect(text).toContain('WH-MAIN');
    expect(text).toContain('On hand now:');
    expect(text).toContain('12');
  });

  it('shows only the fields of a receipt: quantity, reference and notes', () => {
    open('receive');

    expect(field('quantity')).not.toBeNull();
    expect(field('reference')).not.toBeNull();
    expect(field('notes')).not.toBeNull();
    expect(field('countedQuantity')).toBeNull();
    expect(field('reason')).toBeNull();
  });

  it('shows only the fields of a count: the counted quantity and the reason', () => {
    open('adjust', { currentQuantity: 10 });

    expect(textOf(root())).toContain('Count stock');
    expect(field('countedQuantity')).not.toBeNull();
    expect(field('reason')).not.toBeNull();
    expect(field('quantity')).toBeNull();
    expect(field('reference')).toBeNull();
  });

  it('starts a count from the number the system says, so only a different count needs typing', () => {
    open('adjust', { currentQuantity: 10 });

    expect(field('countedQuantity')!.value).toBe('10');
    expect(textOf(root())).toContain('The system says');
  });

  it('cancels without sending anything', () => {
    open('receive');

    buttonWithText('Cancel')!.click();

    expect(cancelled).toHaveLength(1);
    http.expectNone(() => true);
  });

  // ---------- receiving ----------

  it('records a receipt, trimming the reference and sending blank notes as null', () => {
    open('receive');
    typeInto(root(), '#quantity', '10');
    typeInto(root(), '#reference', '  PO-1  ');

    submitForm(root());

    const request = http.expectOne(`${api}/inventory/receipts`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ productId: 3, warehouseId: 10, quantity: 10, reference: 'PO-1', notes: null });

    request.flush(movement());
    expect(saved).toHaveLength(1);
    expect(saved[0].quantityAfter).toBe(22);
  });

  it('disables the button and says "Saving…" while the request is in flight', () => {
    open('receive');
    typeInto(root(), '#quantity', '10');

    submitForm(root());
    fixture.detectChanges();

    const button = root().querySelector<HTMLButtonElement>('button[type=submit]')!;
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Saving…');

    http.expectOne(`${api}/inventory/receipts`).flush(movement());
  });

  it.each([
    ['0', 'Quantity must be between 1 and 1,000,000,000.'],
    ['-4', 'Quantity must be between 1 and 1,000,000,000.'],
    ['1000000001', 'Quantity must be between 1 and 1,000,000,000.'],
    ['2.5', 'Quantity must be a whole number.'],
  ])('refuses a quantity of %s and sends nothing', (quantity, message) => {
    open('receive');
    typeInto(root(), '#quantity', quantity);

    submitForm(root());
    fixture.detectChanges();

    expect(textOf(root())).toContain(message);
    http.expectNone(`${api}/inventory/receipts`);
  });

  it('refuses a reference longer than 100 characters', () => {
    open('receive');
    typeInto(root(), '#quantity', '1');
    typeInto(root(), '#reference', 'x'.repeat(101));

    submitForm(root());
    fixture.detectChanges();

    expect(textOf(root())).toContain('Reference must be 100 characters or fewer.');
    http.expectNone(`${api}/inventory/receipts`);
  });

  it('shows a business-rule refusal from the server in plain words, and lets the user try again', () => {
    open('receive');
    typeInto(root(), '#quantity', '5');

    submitForm(root());
    http.expectOne(`${api}/inventory/receipts`).flush(
      { title: 'Business rule violation.', detail: 'Stock cannot be received for an inactive product.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    fixture.detectChanges();

    expect(textOf(root())).toContain('Stock cannot be received for an inactive product.');
    expect(saved).toHaveLength(0);
    expect(root().querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(false);
  });

  it('puts a server field error under its field', () => {
    open('receive');
    typeInto(root(), '#quantity', '5');

    submitForm(root());
    http.expectOne(`${api}/inventory/receipts`).flush(
      { title: 'One or more validation errors occurred.', errors: { Quantity: ['Quantity cannot exceed 1,000,000,000.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(textOf(field('quantity')!.parentElement!)).toContain('Quantity cannot exceed 1,000,000,000.');
  });

  it('explains a conflict (someone changed the same stock) and asks the screen to refresh, without saving', () => {
    open('receive', { currentQuantity: 12 });
    typeInto(root(), '#quantity', '5');

    submitForm(root());
    http.expectOne(`${api}/inventory/receipts`).flush(
      { title: 'Concurrency conflict.', detail: 'Stock (3) was changed by someone else. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(textOf(root())).toContain('Someone else changed this stock at the same moment.');
    expect(textOf(root())).toContain('Nothing was recorded, so please try again.');
    expect(conflict).toHaveLength(1);
    expect(saved).toHaveLength(0);
  });

  // ---------- issuing ----------

  it('records an issue', () => {
    open('issue', { currentQuantity: 12 });
    typeInto(root(), '#quantity', '4');
    typeInto(root(), '#notes', 'Shipped to a customer');

    submitForm(root());

    const request = http.expectOne(`${api}/inventory/issues`);
    expect(request.request.body).toEqual({
      productId: 3,
      warehouseId: 10,
      quantity: 4,
      reference: null,
      notes: 'Shipped to a customer',
    });
    request.flush(movement({ type: 'Issue', quantityChange: -4, quantityAfter: 8 }));
    expect(saved).toHaveLength(1);
  });

  it('says "Only N on hand." while the quantity is more than the stock, and sends nothing', () => {
    open('issue', { currentQuantity: 5 });
    typeInto(root(), '#quantity', '6');
    fixture.detectChanges();

    expect(textOf(root())).toContain('Only 5 on hand.');

    submitForm(root());
    http.expectNone(`${api}/inventory/issues`);
  });

  it('allows issuing exactly what is on hand', () => {
    open('issue', { currentQuantity: 5 });
    typeInto(root(), '#quantity', '5');
    fixture.detectChanges();

    expect(textOf(root())).not.toContain('Only 5 on hand.');

    submitForm(root());
    http.expectOne(`${api}/inventory/issues`).flush(movement({ type: 'Issue', quantityChange: -5, quantityAfter: 0 }));
  });

  it('does not guess when it does not know what is on hand: the server decides', () => {
    open('issue');
    typeInto(root(), '#quantity', '500');

    submitForm(root());

    http.expectOne(`${api}/inventory/issues`).flush(
      { title: 'Business rule violation.', detail: 'Not enough stock: 5 on hand, 500 requested.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    fixture.detectChanges();

    expect(textOf(root())).toContain('Not enough stock: 5 on hand, 500 requested.');
  });

  // ---------- counting ----------

  it('records a count, sending what was counted and the trimmed reason', () => {
    open('adjust', { currentQuantity: 10 });
    typeInto(root(), '#countedQuantity', '12');
    typeInto(root(), '#reason', '  Cycle count  ');

    submitForm(root());

    const request = http.expectOne(`${api}/inventory/adjustments`);
    expect(request.request.body).toEqual({ productId: 3, warehouseId: 10, countedQuantity: 12, reason: 'Cycle count' });
    request.flush(movement({ type: 'Adjustment', quantityChange: 2, quantityAfter: 12 }));
    expect(saved).toHaveLength(1);
  });

  it('requires a reason for a count, and sends nothing without one', () => {
    open('adjust', { currentQuantity: 10 });
    typeInto(root(), '#countedQuantity', '12');
    typeInto(root(), '#reason', '   ');

    submitForm(root());
    fixture.detectChanges();

    expect(textOf(root())).toContain('A reason is required for a stock count.');
    http.expectNone(`${api}/inventory/adjustments`);
  });

  it('accepts a count of zero (an empty shelf is a real count)', () => {
    open('adjust', { currentQuantity: 10 });
    typeInto(root(), '#countedQuantity', '0');
    typeInto(root(), '#reason', 'Shelf found empty');

    submitForm(root());

    expect(http.expectOne(`${api}/inventory/adjustments`).request.body.countedQuantity).toBe(0);
  });

  it('refuses a negative or fractional count', () => {
    open('adjust', { currentQuantity: 10 });
    typeInto(root(), '#countedQuantity', '-1');
    typeInto(root(), '#reason', 'x');

    submitForm(root());
    fixture.detectChanges();
    expect(textOf(root())).toContain('The counted quantity must be between 0 and 1,000,000,000.');

    typeInto(root(), '#countedQuantity', '3.5');
    submitForm(root());
    fixture.detectChanges();
    expect(textOf(root())).toContain('The counted quantity must be a whole number.');

    http.expectNone(`${api}/inventory/adjustments`);
  });

  it('explains that a count equal to the system\'s number has nothing to adjust', () => {
    open('adjust', { currentQuantity: 10 });
    typeInto(root(), '#reason', 'Recount');

    submitForm(root());
    http.expectOne(`${api}/inventory/adjustments`).flush(
      { title: 'Business rule violation.', detail: 'The counted quantity equals the current stock. Nothing to adjust.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    fixture.detectChanges();

    expect(textOf(root())).toContain('Nothing to adjust.');
  });

  it('tells the user to COUNT AGAIN after a conflict, not just to retry', () => {
    open('adjust', { currentQuantity: 10 });
    typeInto(root(), '#countedQuantity', '7');
    typeInto(root(), '#reason', 'Cycle count');

    submitForm(root());
    http.expectOne(`${api}/inventory/adjustments`).flush(
      { detail: 'Stock (3) was changed by someone else.' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(textOf(root())).toContain('count again');
    expect(textOf(root())).toContain('Your count was not saved.');
    expect(conflict).toHaveLength(1);
  });

  // ---------- choosing a product and a warehouse (first receipts) ----------

  it('asks for a product and a warehouse when it was not opened for a specific row', () => {
    openForPicking();

    expect(field('product-search')).not.toBeNull();
    expect(Array.from(root().querySelectorAll('#warehouse option')).map((o) => o.textContent?.trim())).toEqual([
      'Choose a warehouse',
      'WH-MAIN · Main Warehouse',
      'WH-EAST · East Warehouse',
    ]);
  });

  it('only offers active warehouses when receiving', () => {
    open('receive', { product: null, warehouse: null });

    const request = warehousesRequest();
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({ items: [], pageNumber: 1, totalPages: 1, totalCount: 0, hasPreviousPage: false, hasNextPage: false });
  });

  it('searches products only after the user pauses typing, and only among active ones', () => {
    openForPicking();
    vi.useFakeTimers();

    for (const text of ['l', 'la', 'lap']) {
      typeInto(root(), '#product-search', text);
      vi.advanceTimersByTime(100);
    }

    http.expectNone((request) => request.url === `${api}/products`);   // still inside the pause window

    vi.advanceTimersByTime(300);
    const request = http.expectOne((r) => r.url === `${api}/products`);   // exactly ONE request for three keystrokes
    expect(request.request.params.get('search')).toBe('lap');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush(productsPage([laptop]));
    fixture.detectChanges();

    expect(textOf(root())).toContain('LAP-001');
    expect(textOf(root())).toContain('Business Laptop');
  });

  it('shows the chosen product and lets the user change it', () => {
    openForPicking();
    vi.useFakeTimers();
    typeInto(root(), '#product-search', 'lap');
    vi.advanceTimersByTime(300);
    http.expectOne((r) => r.url === `${api}/products`).flush(productsPage([laptop]));
    fixture.detectChanges();
    vi.useRealTimers();

    root().querySelector<HTMLButtonElement>('ul[aria-label="Matching products"] button')!.click();
    fixture.detectChanges();

    expect(root().querySelector('ul[aria-label="Matching products"]')).toBeNull();
    expect(field('product-search')).toBeNull();
    expect(textOf(root())).toContain('Business Laptop');

    buttonWithText('Change')!.click();
    fixture.detectChanges();
    expect(field('product-search')).not.toBeNull();
  });

  it('will not record anything until a product and a warehouse are chosen', () => {
    openForPicking();
    typeInto(root(), '#quantity', '5');

    submitForm(root());
    fixture.detectChanges();

    expect(textOf(root())).toContain('Choose a product and a warehouse first.');
    http.expectNone(`${api}/inventory/receipts`);
  });

  it('sends the product and warehouse the user chose', () => {
    openForPicking();
    vi.useFakeTimers();
    typeInto(root(), '#product-search', 'lap');
    vi.advanceTimersByTime(300);
    http.expectOne((r) => r.url === `${api}/products`).flush(productsPage([laptop]));
    fixture.detectChanges();
    vi.useRealTimers();
    root().querySelector<HTMLButtonElement>('ul[aria-label="Matching products"] button')!.click();
    fixture.detectChanges();

    const select = field('warehouse') as unknown as HTMLSelectElement;
    select.value = '11';
    select.dispatchEvent(new Event('change'));
    typeInto(root(), '#quantity', '7');

    submitForm(root());

    const request = http.expectOne(`${api}/inventory/receipts`);
    expect(request.request.body).toMatchObject({ productId: 3, warehouseId: 11, quantity: 7 });
    request.flush(movement({ warehouseId: 11, warehouseCode: 'WH-EAST' }));
  });

  it('says so when the warehouses cannot be loaded', () => {
    open('receive', { product: null, warehouse: null });

    warehousesRequest().flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(textOf(root())).toContain('The warehouses could not be loaded.');
  });
});