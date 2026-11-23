import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { Warehouse } from '../../../core/models/warehouse.models';
import { AuthService } from '../../../core/services/auth.service';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { WarehouseForm } from './warehouse-form';

describe('WarehouseForm', () => {
  let http: HttpTestingController;
  let api: string;
  let navigate: ReturnType<typeof vi.spyOn>;
  let harness: RouterTestingHarness;
  let root: HTMLElement;

  const warehouse = (overrides: Partial<Warehouse> = {}): Warehouse => ({
    id: 5,
    code: 'wh-main',
    name: 'Main Distribution Center',
    address: {
      line1: '100 Commerce Way',
      line2: 'Gate 4',
      city: 'Rotterdam',
      region: 'South Holland',
      postalCode: '3011 AA',
      countryCode: 'NL',
    },
    isActive: true,
    version: 'v1',
    locationCount: 0,
    created: '2026-10-17T09:00:00Z',
    lastModified: null,
    ...overrides,
  });

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'warehouses/new', component: WarehouseForm },
          { path: 'warehouses/:id', component: WarehouseForm },
        ]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  const locationsRequest = () =>
    http.expectOne((request) => request.method === 'GET' && request.url === `${api}/warehouses/5/locations`);

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  async function open(url: string, permissions: string[]): Promise<void> {
    signIn(permissions);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, WarehouseForm);
    root = harness.routeNativeElement as HTMLElement;
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    harness.detectChanges();
  }

  /** Opens the edit screen, answers the warehouse request, and answers the location tree's request. */
  async function openEdit(
    permissions = ['warehouses.view', 'warehouses.update'],
    loaded: Warehouse = warehouse(),
    locations: unknown[] = [],
  ): Promise<void> {
    await open('/warehouses/5', permissions);
    http.expectOne(`${api}/warehouses/5`).flush(loaded);
    await harness.fixture.whenStable();
    harness.detectChanges();
    locationsRequest().flush(locations);
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  const openCreate = () => open('/warehouses/new', ['warehouses.view', 'warehouses.create']);
  const field = (id: string) => root.querySelector<HTMLInputElement>(`#${id}`)!;
  const buttonWithText = (text: string) =>
    Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text) ?? null;

  function fillValidNewWarehouse(): void {
    typeInto(root, '#code', '  WH-NEW  ');
    typeInto(root, '#name', '  New Warehouse  ');
    typeInto(root, '#line1', '  1 Harbour St  ');
    typeInto(root, '#city', '  Utrecht ');
    typeInto(root, '#countryCode', 'nl');
  }

  // ---------- create ----------

  it('starts empty, with an editable code and no location tree yet', async () => {
    await openCreate();

    expect(textOf(root)).toContain('New warehouse');
    expect(field('code').disabled).toBe(false);
    expect(field('countryCode').value).toBe('');
    expect(root.querySelector('app-location-tree')).toBeNull();
  });

  it('shows required errors and sends nothing when the form is empty', async () => {
    await openCreate();

    submitForm(root);
    harness.detectChanges();

    const text = textOf(root);
    expect(text).toContain('Warehouse code is required.');
    expect(text).toContain('Warehouse name is required.');
    expect(text).toContain('Address line 1 is required.');
    expect(text).toContain('City is required.');
    expect(text).toContain('Country code is required.');
    http.expectNone(`${api}/warehouses`);
  });

  it('explains an invalid code', async () => {
    await openCreate();
    fillValidNewWarehouse();
    typeInto(root, '#code', 'bad code!');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Use letters, digits, dots, hyphens, underscores and slashes');
    http.expectNone(`${api}/warehouses`);
  });

  it('explains an invalid country code', async () => {
    await openCreate();
    fillValidNewWarehouse();
    typeInto(root, '#countryCode', 'N1');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Country code must be a 2-letter ISO code such as IN or NL.');
    http.expectNone(`${api}/warehouses`);
  });

  it('rejects a name made only of spaces', async () => {
    await openCreate();
    fillValidNewWarehouse();
    typeInto(root, '#name', '     ');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Warehouse name is required.');
    http.expectNone(`${api}/warehouses`);
  });

  it('creates the warehouse (trimmed, blank optionals as null, country upper-cased) and returns to the list', async () => {
    await openCreate();
    fillValidNewWarehouse();

    submitForm(root);
    harness.detectChanges();

    const request = http.expectOne(`${api}/warehouses`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      code: 'WH-NEW',
      name: 'New Warehouse',
      address: {
        line1: '1 Harbour St',
        line2: null,
        city: 'Utrecht',
        region: null,
        postalCode: null,
        countryCode: 'NL',
      },
    });
    request.flush(warehouse({ code: 'WH-NEW' }));

    expect(navigate).toHaveBeenCalledWith(['/warehouses']);
  });

  it('shows the server\'s duplicate-code error under the code field and lets the user fix it', async () => {
    await openCreate();
    fillValidNewWarehouse();

    submitForm(root);
    harness.detectChanges();
    http.expectOne(`${api}/warehouses`).flush(
      { title: 'One or more validation errors occurred.', errors: { Code: ['A warehouse with this code already exists.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    harness.detectChanges();

    expect(textOf(field('code').parentElement!)).toContain('A warehouse with this code already exists.');
    expect(navigate).not.toHaveBeenCalled();
    expect(root.querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(false);
  });

  it('puts a NESTED server error (Address.City) under the city field', async () => {
    await openCreate();
    fillValidNewWarehouse();

    submitForm(root);
    harness.detectChanges();
    http.expectOne(`${api}/warehouses`).flush(
      { title: 'One or more validation errors occurred.', errors: { 'Address.City': ['City must be 100 characters or fewer.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    harness.detectChanges();

    expect(textOf(field('city').parentElement!)).toContain('City must be 100 characters or fewer.');
    expect(textOf(field('line1').parentElement!)).not.toContain('City must be');
  });

  // ---------- edit ----------

  it('loads the warehouse into the form, with the code locked', async () => {
    await openEdit();

    expect(textOf(root)).toContain('Edit warehouse');
    expect(field('code').value).toBe('wh-main');
    expect(field('code').disabled).toBe(true);
    expect(field('name').value).toBe('Main Distribution Center');
    expect(field('line1').value).toBe('100 Commerce Way');
    expect(field('line2').value).toBe('Gate 4');
    expect(field('city').value).toBe('Rotterdam');
    expect(field('region').value).toBe('South Holland');
    expect(field('postalCode').value).toBe('3011 AA');
    expect(field('countryCode').value).toBe('NL');
    expect(textOf(root)).toContain('A code cannot be changed once the warehouse exists.');
    expect(textOf(root)).toContain('Active');
  });

  it('shows the warehouse\'s storage locations below the form', async () => {
    await openEdit(['warehouses.view', 'warehouses.update'], warehouse(), [
      {
        id: 1,
        warehouseId: 5,
        parentId: null,
        code: 'A',
        name: 'Ambient',
        type: 'Zone',
        isActive: true,
        version: 'v1',
        created: '2026-10-17T09:00:00Z',
        lastModified: null,
      },
    ]);

    expect(root.querySelector('app-location-tree')).not.toBeNull();
    expect(textOf(root)).toContain('Storage locations');
    expect(textOf(root)).toContain('Ambient');
    expect(buttonWithText('Add top-level location')).not.toBeNull();
  });

  it('saves changes, sending the version it loaded, the new address and no code', async () => {
    await openEdit();
    typeInto(root, '#name', 'East Hub');
    typeInto(root, '#city', 'Antwerp');
    typeInto(root, '#countryCode', 'be');
    typeInto(root, '#line2', '   ');

    submitForm(root);
    harness.detectChanges();

    const request = http.expectOne(`${api}/warehouses/5`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      version: 'v1',
      name: 'East Hub',
      address: {
        line1: '100 Commerce Way',
        line2: null,
        city: 'Antwerp',
        region: 'South Holland',
        postalCode: '3011 AA',
        countryCode: 'BE',
      },
    });
    request.flush(warehouse({ name: 'East Hub', version: 'v2' }));
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('Saved.');
    expect(field('name').value).toBe('East Hub');
  });

  it('uses the NEW version for the next save (so two saves in a row do not conflict with themselves)', async () => {
    await openEdit();

    typeInto(root, '#name', 'First');
    submitForm(root);
    http.expectOne(`${api}/warehouses/5`).flush(warehouse({ name: 'First', version: 'v2' }));
    await harness.fixture.whenStable();

    typeInto(root, '#name', 'Second');
    submitForm(root);

    const second = http.expectOne(`${api}/warehouses/5`);
    expect(second.request.body.version).toBe('v2');
    second.flush(warehouse({ name: 'Second', version: 'v3' }));
  });

  it('tells the user when someone else changed the warehouse, without losing their typing', async () => {
    await openEdit();
    typeInto(root, '#name', 'My edit');

    submitForm(root);
    http.expectOne(`${api}/warehouses/5`).flush(
      { title: 'Concurrency conflict.', detail: 'Warehouse (5) was changed by someone else. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('Someone else changed this warehouse while you were editing.');
    expect(textOf(root)).toContain('Your changes were not saved.');
    expect(field('name').value).toBe('My edit');
  });

  it('loads the latest version on request, discarding the stale edit and the warning', async () => {
    await openEdit();
    typeInto(root, '#name', 'My edit');
    submitForm(root);
    http.expectOne(`${api}/warehouses/5`).flush({ detail: 'conflict' }, { status: 409, statusText: 'Conflict' });
    await harness.fixture.whenStable();

    buttonWithText('Load the latest version')!.click();
    http.expectOne(`${api}/warehouses/5`).flush(warehouse({ name: 'Edited by someone else', version: 'v9' }));
    await harness.fixture.whenStable();

    expect(field('name').value).toBe('Edited by someone else');
    expect(textOf(root)).not.toContain('Someone else changed this warehouse');
  });

  it('shows a clear message when the warehouse does not exist (or belongs to another organization)', async () => {
    await open('/warehouses/5', ['warehouses.view']);
    http.expectOne(`${api}/warehouses/5`).flush({}, { status: 404, statusText: 'Not Found' });
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('That warehouse could not be found.');
    expect(root.querySelector('form')).toBeNull();
    expect(root.querySelector('app-location-tree')).toBeNull();
  });

  it('does not even ask the server about an id that is not a positive number', async () => {
    await open('/warehouses/abc', ['warehouses.view']);

    expect(textOf(root)).toContain('That warehouse could not be found.');
    http.expectNone(`${api}/warehouses/abc`);
  });

  it('shows a load error and retries on demand', async () => {
    await open('/warehouses/5', ['warehouses.view']);
    http.expectOne(`${api}/warehouses/5`).flush({}, { status: 500, statusText: 'Server Error' });
    await harness.fixture.whenStable();
    expect(textOf(root)).toContain('The server ran into a problem.');

    buttonWithText('Try again')!.click();
    http.expectOne(`${api}/warehouses/5`).flush(warehouse());
    await harness.fixture.whenStable();
    harness.detectChanges();
    locationsRequest().flush([]);

    expect(field('name').value).toBe('Main Distribution Center');
  });

  it('is read-only for a user who may view but not update, including the location tree', async () => {
    await openEdit(['warehouses.view']);

    expect(field('name').disabled).toBe(true);
    expect(field('city').disabled).toBe(true);
    expect(buttonWithText('Save changes')).toBeNull();
    expect(buttonWithText('Deactivate')).toBeNull();
    expect(buttonWithText('Add top-level location')).toBeNull();
    expect(textOf(root)).toContain('You can view this warehouse but not change it.');
  });

  // ---------- activate / deactivate on the edit screen ----------

  it('deactivates the warehouse with its current version, and the tree stops offering to add locations', async () => {
    await openEdit();
    expect(buttonWithText('Add top-level location')).not.toBeNull();

    buttonWithText('Deactivate')!.click();

    const request = http.expectOne(`${api}/warehouses/5/deactivate`);
    expect(request.request.body).toEqual({ version: 'v1' });
    request.flush(warehouse({ isActive: false, version: 'v2' }));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(textOf(root)).toContain('Inactive');
    expect(buttonWithText('Reactivate')).not.toBeNull();
    expect(buttonWithText('Add top-level location')).toBeNull();
    expect(textOf(root)).toContain('Reactivate the warehouse to add locations.');
  });

  it('reactivates an inactive warehouse', async () => {
    await openEdit(['warehouses.view', 'warehouses.update'], warehouse({ isActive: false, version: 'v4' }));

    buttonWithText('Reactivate')!.click();

    const request = http.expectOne(`${api}/warehouses/5/activate`);
    expect(request.request.body).toEqual({ version: 'v4' });
    request.flush(warehouse({ isActive: true, version: 'v5' }));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(buttonWithText('Deactivate')).not.toBeNull();
    expect(buttonWithText('Add top-level location')).not.toBeNull();
  });
});