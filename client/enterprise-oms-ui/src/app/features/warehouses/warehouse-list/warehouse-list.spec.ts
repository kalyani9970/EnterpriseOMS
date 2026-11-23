import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../../core/config/api-config';
import { PagedResult, Warehouse, WarehouseListItem } from '../../../core/models/warehouse.models';
import { AuthService } from '../../../core/services/auth.service';
import { textOf } from '../../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { WarehouseList } from './warehouse-list';

describe('WarehouseList', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<WarehouseList>;

  const item = (overrides: Partial<WarehouseListItem> = {}): WarehouseListItem => ({
    id: 1,
    code: 'WH-MAIN',
    name: 'Main Distribution Center',
    city: 'Rotterdam',
    countryCode: 'NL',
    isActive: true,
    version: 'v1',
    locationCount: 9,
    ...overrides,
  });

  const page = (
    items: WarehouseListItem[],
    overrides: Partial<PagedResult<WarehouseListItem>> = {},
  ): PagedResult<WarehouseListItem> => ({
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

  const listRequest = (): TestRequest => http.expectOne((request) => request.url === `${api}/warehouses`);
  const root = () => fixture.nativeElement as HTMLElement;

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  /** Creates the screen and answers its initial request (the first page). */
  async function open(
    firstPage: PagedResult<WarehouseListItem> = page([item()]),
    permissions: string[] = ['warehouses.view'],
  ): Promise<HTMLElement> {
    signIn(permissions);

    fixture = TestBed.createComponent(WarehouseList);
    fixture.detectChanges();

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

  it('shows a loading message, then the warehouses', async () => {
    signIn(['warehouses.view']);
    fixture = TestBed.createComponent(WarehouseList);
    fixture.detectChanges();
    expect(textOf(root())).toContain('Loading warehouses…');

    listRequest().flush(
      page([item(), item({ id: 2, code: 'WH-OLD', name: 'Closed Depot', city: 'Ghent', countryCode: 'BE', isActive: false, locationCount: 0 })]),
    );
    await fixture.whenStable();

    const text = textOf(root());
    expect(text).toContain('WH-MAIN');
    expect(text).toContain('Main Distribution Center');
    expect(text).toContain('Rotterdam, NL');
    expect(text).toContain('Ghent, BE');
    expect(text).toContain('Active');
    expect(text).toContain('Inactive');
    expect(text).not.toContain('Loading warehouses…');
  });

  it('shows how many locations each warehouse has', async () => {
    await open(page([item({ locationCount: 9 }), item({ id: 2, code: 'WH-EAST', locationCount: 2 })]));

    const counts = Array.from(root().querySelectorAll('tbody tr')).map((row) => row.children[3].textContent?.trim());

    expect(counts).toEqual(['9', '2']);
  });

  it('asks the server for the first page, sorted by name, with no filters', async () => {
    signIn(['warehouses.view']);
    fixture = TestBed.createComponent(WarehouseList);
    fixture.detectChanges();

    const request = listRequest();
    expect(request.request.params.get('pageNumber')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('sortBy')).toBe('name');
    expect(request.request.params.get('descending')).toBe('false');
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('isActive')).toBe(false);
    request.flush(page([]));
  });

  it('links each name to the warehouse page', async () => {
    await open(page([item({ id: 12 })]));

    expect(root().querySelector('tbody a')?.getAttribute('href')).toBe('/warehouses/12');
  });

  it('shows the range and page position', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 3, totalCount: 45, hasNextPage: true }));

    expect(textOf(root())).toContain('Showing 1–20 of 45');
    expect(textOf(root())).toContain('Page 1 of 3');
  });

  it('offers to create the first warehouse when there are none (and the user may create)', async () => {
    await open(page([]), ['warehouses.view', 'warehouses.create']);

    expect(textOf(root())).toContain('No warehouses yet.');
    expect(textOf(root())).toContain('Create the first one');
  });

  it('does not offer creation to users who cannot create', async () => {
    await open(page([]), ['warehouses.view']);

    expect(textOf(root())).toContain('No warehouses yet.');
    expect(textOf(root())).not.toContain('Create the first one');
  });

  it('shows the load error and retries on demand', async () => {
    signIn(['warehouses.view']);
    fixture = TestBed.createComponent(WarehouseList);
    fixture.detectChanges();

    listRequest().flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    expect(textOf(root())).toContain('The server ran into a problem.');

    click(buttonWithText('Try again'));
    listRequest().flush(page([item()]));
    await fixture.whenStable();

    expect(textOf(root())).toContain('WH-MAIN');
  });

  // ---------- search and filters ----------

  it('waits until the user pauses typing before searching, then searches once from page 1', async () => {
    await open(page([item()], { pageNumber: 1, totalPages: 3, totalCount: 45, hasNextPage: true }));
    click(buttonWithText('Next'));
    listRequest().flush(page([item()], { pageNumber: 2, totalPages: 3, totalCount: 45, hasPreviousPage: true, hasNextPage: true }));
    await fixture.whenStable();

    vi.useFakeTimers();
    const input = root().querySelector<HTMLInputElement>('#search')!;
    for (const text of ['r', 'ro', 'rott']) {
      input.value = text;
      input.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(100);
    }

    http.expectNone((request) => request.url === `${api}/warehouses`);   // still within the pause window

    vi.advanceTimersByTime(300);
    const request = listRequest();   // exactly ONE request for the three keystrokes
    expect(request.request.params.get('search')).toBe('rott');
    expect(request.request.params.get('pageNumber')).toBe('1');
    request.flush(page([]));
  });

  it('shows "no match" (not "no warehouses yet") when a search finds nothing', async () => {
    await open();

    vi.useFakeTimers();
    const input = root().querySelector<HTMLInputElement>('#search')!;
    input.value = 'zzz';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(300);
    listRequest().flush(page([]));
    vi.useRealTimers();
    await fixture.whenStable();

    expect(textOf(root())).toContain('No warehouses match your search or filters.');
    expect(textOf(root())).not.toContain('No warehouses yet.');
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

    click(buttonWithText('Code'));
    const first = listRequest();
    expect(first.request.params.get('sortBy')).toBe('code');
    expect(first.request.params.get('descending')).toBe('false');
    first.flush(page([item()]));
    await fixture.whenStable();
    expect(root().querySelector('th[aria-sort="ascending"]')).not.toBeNull();

    click(buttonWithText('Code'));
    const second = listRequest();
    expect(second.request.params.get('descending')).toBe('true');
    second.flush(page([item()]));
    await fixture.whenStable();
    expect(root().querySelector('th[aria-sort="descending"]')).not.toBeNull();
  });

  it('can sort by city, and starts a different column in ascending order', async () => {
    await open();
    click(buttonWithText('City'));
    listRequest().flush(page([item()]));
    click(buttonWithText('City'));   // now descending
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

    choose('#status', 'active');
    const slow = listRequest();
    choose('#status', 'inactive');
    const latest = listRequest();

    expect(slow.cancelled).toBe(true);

    latest.flush(page([item({ name: 'Inactive Depot', isActive: false })]));
    await fixture.whenStable();
    expect(textOf(root())).toContain('Inactive Depot');
  });

  // ---------- permissions ----------

  it('shows no create or edit actions to a user who can only view', async () => {
    await open(page([item()]), ['warehouses.view']);

    expect(textOf(root())).not.toContain('New warehouse');
    expect(textOf(root())).not.toContain('Edit');
    expect(buttonWithText('Deactivate')).toBeNull();
  });

  it('shows New warehouse, Edit and Deactivate to users who may create and update', async () => {
    await open(page([item()]), ['warehouses.view', 'warehouses.create', 'warehouses.update']);

    expect(textOf(root())).toContain('New warehouse');
    expect(textOf(root())).toContain('Edit');
    expect(buttonWithText('Deactivate')).not.toBeNull();
  });

  // ---------- activate / deactivate ----------

  it('deactivates a warehouse with the version it showed, confirms, and reloads the list', async () => {
    await open(page([item({ version: 'v-7' })]), ['warehouses.view', 'warehouses.update']);

    click(buttonWithText('Deactivate'));

    const request = http.expectOne(`${api}/warehouses/1/deactivate`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ version: 'v-7' });
    request.flush({ ...item(), isActive: false } as unknown as Warehouse);
    await fixture.whenStable();

    expect(textOf(root())).toContain('Main Distribution Center was deactivated.');
    listRequest().flush(page([item({ isActive: false, version: 'v-8' })]));
    await fixture.whenStable();
    expect(buttonWithText('Reactivate')).not.toBeNull();
  });

  it('reactivates an inactive warehouse', async () => {
    await open(page([item({ isActive: false, version: 'v-2' })]), ['warehouses.view', 'warehouses.update']);

    click(buttonWithText('Reactivate'));

    const request = http.expectOne(`${api}/warehouses/1/activate`);
    expect(request.request.body).toEqual({ version: 'v-2' });
    request.flush({ ...item() } as unknown as Warehouse);
    await fixture.whenStable();

    expect(textOf(root())).toContain('Main Distribution Center was reactivated.');
    listRequest().flush(page([item()]));
  });

  it('explains a conflict (someone else changed the warehouse) and refreshes the list', async () => {
    await open(page([item()]), ['warehouses.view', 'warehouses.update']);

    click(buttonWithText('Deactivate'));
    http.expectOne(`${api}/warehouses/1/deactivate`).flush(
      { title: 'Concurrency conflict.', detail: 'Warehouse (1) was changed by someone else. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(textOf(root())).toContain('was changed by someone else');
    listRequest().flush(page([item({ version: 'v-newer' })]));   // the list reloads so the user sees fresh data
  });

  it('shows other errors from a row action without reloading', async () => {
    await open(page([item()]), ['warehouses.view', 'warehouses.update']);

    click(buttonWithText('Deactivate'));
    http.expectOne(`${api}/warehouses/1/deactivate`).flush({}, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();

    expect(textOf(root())).toContain('You do not have permission to do that.');
    http.expectNone((request) => request.url === `${api}/warehouses`);
  });
});