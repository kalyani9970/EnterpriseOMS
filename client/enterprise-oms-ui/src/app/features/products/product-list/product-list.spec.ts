import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../../core/config/api-config';
import { PagedResult, Product, ProductListItem } from '../../../core/models/product.models';
import { AuthService } from '../../../core/services/auth.service';
import { textOf } from '../../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { ProductList } from './product-list';

describe('ProductList', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<ProductList>;

  const item = (overrides: Partial<ProductListItem> = {}): ProductListItem => ({
    id: 1,
    sku: 'LAP-001',
    name: 'Business Laptop',
    category: 'Electronics',
    unitOfMeasure: 'Piece',
    unitPrice: 899,
    reorderLevel: 5,
    isActive: true,
    version: 'v1',
    ...overrides,
  });

  const page = (items: ProductListItem[], overrides: Partial<PagedResult<ProductListItem>> = {}): PagedResult<ProductListItem> => ({
    items,
    pageNumber: 1,
    totalPages: 1,
    totalCount: items.length,
    hasPreviousPage: false,
    hasNextPage: false,
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
    http.verify();
  });

  const listRequest = (): TestRequest => http.expectOne((request) => request.url === `${api}/products`);
  const root = () => fixture.nativeElement as HTMLElement;

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  /** Creates the screen and answers its two initial requests (categories, first page). */
  async function open(
    firstPage: PagedResult<ProductListItem> = page([item()]),
    permissions: string[] = ['products.view'],
  ): Promise<HTMLElement> {
    signIn(permissions);

    fixture = TestBed.createComponent(ProductList);
    fixture.detectChanges();

    http.expectOne(`${api}/products/categories`).flush(['Accessories', 'Electronics']);
    listRequest().flush(firstPage);
    await fixture.whenStable();

    return root();
  }

  const click = (element: Element | null) => (element as HTMLElement).click();
  const buttonWithText = (text: string) =>
    Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.includes(text)) ?? null;

  function choose(selector: string, value: string): void {
    const select = root().querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  // ---------- loading and showing ----------

  it('shows a loading message, then the products', async () => {
    signIn(['products.view']);
    fixture = TestBed.createComponent(ProductList);
    fixture.detectChanges();
    expect(textOf(root())).toContain('Loading products…');

    http.expectOne(`${api}/products/categories`).flush([]);
    listRequest().flush(page([item(), item({ id: 2, sku: 'MON-024', name: '24-inch Monitor', unitPrice: 179.99, isActive: false })]));
    await fixture.whenStable();

    const text = textOf(root());
    expect(text).toContain('Business Laptop');
    expect(text).toContain('LAP-001');
    expect(text).toContain('899.00');
    expect(text).toContain('24-inch Monitor');
    expect(text).toContain('179.99');
    expect(text).toContain('Active');
    expect(text).toContain('Inactive');
    expect(text).not.toContain('Loading products…');
  });

  it('asks the server for the first page, sorted by name, with no filters', async () => {
    signIn(['products.view']);
    fixture = TestBed.createComponent(ProductList);
    fixture.detectChanges();
    http.expectOne(`${api}/products/categories`).flush([]);

    const request = listRequest();
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('sortBy')).toBe('name');
    expect(request.request.params.get('descending')).toBe('false');
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('category')).toBe(false);
    expect(request.request.params.has('isActive')).toBe(false);
    request.flush(page([]));
  });

  it('shows an uncategorized product with a dash, and links its name to the edit page', async () => {
    await open(page([item({ category: null })]));

    expect(textOf(root().querySelector('tbody')!)).toContain('—');
    expect(root().querySelector('tbody a')?.getAttribute('href')).toBe('/products/1');
  });

  it('shows the range and page position', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 3, totalCount: 45, hasNextPage: true }));

    expect(textOf(root())).toContain('Showing 1–20 of 45');
    expect(textOf(root())).toContain('Page 1 of 3');
  });

  it('offers to create the first product when the catalog is empty (and the user may create)', async () => {
    await open(page([]), ['products.view', 'products.create']);

    expect(textOf(root())).toContain('No products yet.');
    expect(textOf(root())).toContain('Create the first one');
  });

  it('does not offer creation to users who cannot create', async () => {
    await open(page([]), ['products.view']);

    expect(textOf(root())).toContain('No products yet.');
    expect(textOf(root())).not.toContain('Create the first one');
  });

  it('shows the load error and retries on demand', async () => {
    signIn(['products.view']);
    fixture = TestBed.createComponent(ProductList);
    fixture.detectChanges();
    http.expectOne(`${api}/products/categories`).flush([]);

    listRequest().flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    expect(textOf(root())).toContain('The server ran into a problem.');

    click(buttonWithText('Try again'));
    listRequest().flush(page([item()]));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Business Laptop');
  });

  it('still works when the category list cannot be loaded', async () => {
    signIn(['products.view']);
    fixture = TestBed.createComponent(ProductList);
    fixture.detectChanges();

    http.expectOne(`${api}/products/categories`).flush({}, { status: 500, statusText: 'Server Error' });
    listRequest().flush(page([item()]));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Business Laptop');
  });

  // ---------- search, filters ----------

  it('waits until the user pauses typing before searching, then searches once from page 1', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 3, totalCount: 45, hasNextPage: true }));
    click(buttonWithText('Next'));
    listRequest().flush(page([item()], { pageNumber: 2, totalPages: 3, totalCount: 45, hasPreviousPage: true, hasNextPage: true }));
    await fixture.whenStable();

    vi.useFakeTimers();
    const input = root().querySelector<HTMLInputElement>('#search')!;
    for (const text of ['w', 'wi', 'wire']) {
      input.value = text;
      input.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(100);
    }

    http.expectNone((request) => request.url === `${api}/products`);   // still within the pause window

    vi.advanceTimersByTime(300);
    const request = listRequest();   // exactly ONE request for the three keystrokes
    expect(request.request.params.get('search')).toBe('wire');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('shows "no match" (not "no products yet") when a search finds nothing', async () => {
    await open();

    vi.useFakeTimers();
    const input = root().querySelector<HTMLInputElement>('#search')!;
    input.value = 'zzz';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(300);
    listRequest().flush(page([]));
    vi.useRealTimers();
    await fixture.whenStable();

    expect(textOf(root())).toContain('No products match your search or filters.');
    expect(textOf(root())).not.toContain('No products yet.');
  });

  it('filters by category and goes back to the first page', async () => {
    await open();

    choose('#category', 'Accessories');

    const request = listRequest();
    expect(request.request.params.get('category')).toBe('Accessories');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it.each([
    ['active', 'true'],
    ['inactive', 'false'],
  ])('filters by status "%s"', async (choice, expected) => {
    await open();

    choose('#status', choice);

    const request = listRequest();
    expect(request.request.params.get('isActive')).toBe(expected);
    request.flush(page([]));
  });

  it('returns to the first page whenever a filter changes', async () => {
    await open(page([item()], { totalPages: 3, totalCount: 45, hasNextPage: true }));
    click(buttonWithText('Next'));
    listRequest().flush(page([item()], { pageNumber: 2, totalPages: 3, totalCount: 45, hasPreviousPage: true, hasNextPage: true }));
    await fixture.whenStable();

    choose('#status', 'active');

    expect(listRequest().request.params.get('pageNumber')).toBe('1');
  });

  // ---------- sorting ----------

  it('sorts by a column on click, and flips the direction on a second click', async () => {
    await open();

    click(buttonWithText('SKU'));
    const first = listRequest();
    expect(first.request.params.get('sortBy')).toBe('sku');
    expect(first.request.params.get('descending')).toBe('false');
    first.flush(page([item()]));
    await fixture.whenStable();
    expect(root().querySelector('th[aria-sort="ascending"]')).not.toBeNull();

    click(buttonWithText('SKU'));
    const second = listRequest();
    expect(second.request.params.get('descending')).toBe('true');
    second.flush(page([item()]));
    await fixture.whenStable();
    expect(root().querySelector('th[aria-sort="descending"]')).not.toBeNull();
  });

  it('starts a different column in ascending order', async () => {
    await open();
    click(buttonWithText('Price'));
    listRequest().flush(page([item()]));
    click(buttonWithText('Price'));   // now descending
    listRequest().flush(page([item()]));

    click(buttonWithText('Name'));

    const request = listRequest();
    expect(request.request.params.get('sortBy')).toBe('name');
    expect(request.request.params.get('descending')).toBe('false');
    request.flush(page([item()]));
  });

  // ---------- paging ----------

  it('moves between pages with Previous and Next, and disables them at the ends', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 2, totalCount: 30, hasNextPage: true }));
    expect(buttonWithText('Previous')!.disabled).toBe(true);
    expect(buttonWithText('Next')!.disabled).toBe(false);

    click(buttonWithText('Next'));
    const request = listRequest();
    expect(request.request.params.get('pageNumber')).toBe('2');
    request.flush(page([item()], { pageNumber: 2, totalPages: 2, totalCount: 30, hasPreviousPage: true }));
    await fixture.whenStable();

    expect(buttonWithText('Next')!.disabled).toBe(true);
    expect(buttonWithText('Previous')!.disabled).toBe(false);
  });

  it('changes the page size and returns to the first page', async () => {
    await open();

    choose('#pageSize', '50');

    const request = listRequest();
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('cancels a request that has been overtaken, so an old slow answer cannot replace a newer one', async () => {
    await open();

    choose('#category', 'Accessories');
    const slow = listRequest();
    choose('#category', 'Electronics');
    const latest = listRequest();

    expect(slow.cancelled).toBe(true);

    latest.flush(page([item({ name: 'Electronics Item' })]));
    await fixture.whenStable();
    expect(textOf(root())).toContain('Electronics Item');
  });

  // ---------- permissions ----------

  it('shows no create or edit actions to a user who can only view', async () => {
    await open(page([item()]), ['products.view']);

    expect(textOf(root())).not.toContain('New product');
    expect(textOf(root())).not.toContain('Edit');
    expect(buttonWithText('Deactivate')).toBeNull();
  });

  it('shows New product, Edit and Deactivate to users who may create and update', async () => {
    await open(page([item()]), ['products.view', 'products.create', 'products.update']);

    expect(textOf(root())).toContain('New product');
    expect(textOf(root())).toContain('Edit');
    expect(buttonWithText('Deactivate')).not.toBeNull();
  });

  // ---------- activate / deactivate ----------

  it('deactivates a product with the version it showed, confirms, and reloads the list', async () => {
    await open(page([item({ version: 'v-7' })]), ['products.view', 'products.update']);

    click(buttonWithText('Deactivate'));

    const request = http.expectOne(`${api}/products/1/deactivate`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ version: 'v-7' });
    request.flush({ ...item(), isActive: false } as Product);
    await fixture.whenStable();

    expect(textOf(root())).toContain('Business Laptop was deactivated.');
    listRequest().flush(page([item({ isActive: false, version: 'v-8' })]));
    await fixture.whenStable();
    expect(buttonWithText('Reactivate')).not.toBeNull();
  });

  it('reactivates an inactive product', async () => {
    await open(page([item({ isActive: false, version: 'v-2' })]), ['products.view', 'products.update']);

    click(buttonWithText('Reactivate'));

    const request = http.expectOne(`${api}/products/1/activate`);
    expect(request.request.body).toEqual({ version: 'v-2' });
    request.flush({ ...item() } as Product);
    await fixture.whenStable();

    expect(textOf(root())).toContain('Business Laptop was reactivated.');
    listRequest().flush(page([item()]));
  });

  it('explains a conflict (someone else changed the product) and refreshes the list', async () => {
    await open(page([item()]), ['products.view', 'products.update']);

    click(buttonWithText('Deactivate'));
    http.expectOne(`${api}/products/1/deactivate`).flush(
      { title: 'Concurrency conflict.', detail: 'Product (1) was changed by someone else. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(textOf(root())).toContain('was changed by someone else');
    listRequest().flush(page([item({ version: 'v-newer' })]));   // the list reloads so the user sees fresh data
  });

  it('shows other errors from a row action without reloading', async () => {
    await open(page([item()]), ['products.view', 'products.update']);

    click(buttonWithText('Deactivate'));
    http.expectOne(`${api}/products/1/deactivate`).flush({}, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();

    expect(textOf(root())).toContain('You do not have permission to do that.');
    http.expectNone((request) => request.url === `${api}/products`);
  });
});