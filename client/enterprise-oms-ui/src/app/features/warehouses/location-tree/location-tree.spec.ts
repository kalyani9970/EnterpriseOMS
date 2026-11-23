import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { StorageLocation, StorageLocationType } from '../../../core/models/warehouse.models';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { LocationTree } from './location-tree';

describe('LocationTree', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<LocationTree>;

  const location = (
    id: number,
    code: string,
    type: StorageLocationType,
    parentId: number | null = null,
    overrides: Partial<StorageLocation> = {},
  ): StorageLocation => ({
    id,
    warehouseId: 7,
    parentId,
    code,
    name: null,
    type,
    isActive: true,
    version: `v${id}`,
    created: '2026-10-17T09:00:00Z',
    lastModified: null,
    ...overrides,
  });

  /** In tree order, as the server sends it: A, A-01, A-01-R1, A-02, B. */
  const tree = (): StorageLocation[] => [
    location(1, 'A', 'Zone', null, { name: 'Ambient' }),
    location(2, 'A-01', 'Aisle', 1),
    location(3, 'A-01-R1', 'Rack', 2),
    location(4, 'A-02', 'Aisle', 1, { name: 'Second aisle' }),
    location(5, 'B', 'Zone'),
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  const locationsRequest = (): TestRequest =>
    http.expectOne((request) => request.method === 'GET' && request.url === `${api}/warehouses/7/locations`);
  const root = () => fixture.nativeElement as HTMLElement;

  async function open(
    locations: StorageLocation[] = tree(),
    options: { canEdit?: boolean; warehouseActive?: boolean } = {},
  ): Promise<HTMLElement> {
    fixture = TestBed.createComponent(LocationTree);
    fixture.componentRef.setInput('warehouseId', 7);
    fixture.componentRef.setInput('canEdit', options.canEdit ?? true);
    fixture.componentRef.setInput('warehouseActive', options.warehouseActive ?? true);
    fixture.detectChanges();

    locationsRequest().flush(locations);
    await fixture.whenStable();

    return root();
  }

  const byLabel = (label: string) => root().querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
  const buttonWithText = (text: string) =>
    Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim() === text) ?? null;
  const click = (element: Element | null) => (element as HTMLElement).click();

  function chooseType(type: string): void {
    const select = root().querySelector<HTMLSelectElement>('#location-type')!;
    select.value = type;
    select.dispatchEvent(new Event('change'));
  }

  // ---------- showing the tree ----------

  it('shows a loading message, then every location with its type, code and name', async () => {
    fixture = TestBed.createComponent(LocationTree);
    fixture.componentRef.setInput('warehouseId', 7);
    fixture.detectChanges();
    expect(textOf(root())).toContain('Loading locations…');

    locationsRequest().flush(tree());
    await fixture.whenStable();

    const text = textOf(root());
    expect(text).toContain('Zone');
    expect(text).toContain('A-01-R1');
    expect(text).toContain('Ambient');
    expect(text).toContain('Second aisle');
    expect(text).not.toContain('Loading locations…');
  });

  it('asks for every location, including inactive ones', async () => {
    fixture = TestBed.createComponent(LocationTree);
    fixture.componentRef.setInput('warehouseId', 7);
    fixture.detectChanges();

    const request = locationsRequest();
    expect(request.request.params.get('activeOnly')).toBe('false');
    request.flush([]);
  });

  it('indents each row by its depth in the tree', async () => {
    await open();

    const depths = Array.from(root().querySelectorAll('li')).map((li) => li.getAttribute('data-depth'));

    expect(depths).toEqual(['0', '1', '2', '1', '0']);
  });

  it('shows an empty message when the warehouse has no locations yet', async () => {
    await open([]);

    expect(textOf(root())).toContain('No locations yet.');
  });

  it('shows the load error and retries on demand', async () => {
    fixture = TestBed.createComponent(LocationTree);
    fixture.componentRef.setInput('warehouseId', 7);
    fixture.detectChanges();

    locationsRequest().flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    expect(textOf(root())).toContain('The server ran into a problem.');

    click(buttonWithText('Try again'));
    locationsRequest().flush(tree());
    await fixture.whenStable();

    expect(textOf(root())).toContain('A-01-R1');
  });

  // ---------- permissions and state ----------

  it('offers no editing actions to a user who may only view', async () => {
    await open(tree(), { canEdit: false });

    expect(buttonWithText('Add top-level location')).toBeNull();
    expect(buttonWithText('Add inside')).toBeNull();
    expect(buttonWithText('Rename')).toBeNull();
    expect(buttonWithText('Deactivate')).toBeNull();
  });

  it('offers add, rename and deactivate to a user who may edit', async () => {
    await open();

    expect(buttonWithText('Add top-level location')).not.toBeNull();
    expect(byLabel('Add location inside A-01')).not.toBeNull();
    expect(byLabel('Rename A-01')).not.toBeNull();
    expect(byLabel('Deactivate A-01-R1')).not.toBeNull();
  });

  it('offers no "Add inside" for a bin, because nothing can go inside one', async () => {
    await open([location(1, 'A', 'Zone'), location(2, 'A-B1', 'Bin', 1)]);

    expect(byLabel('Add location inside A')).not.toBeNull();
    expect(byLabel('Add location inside A-B1')).toBeNull();
  });

  it('cannot add locations to an inactive warehouse, and says why', async () => {
    await open(tree(), { warehouseActive: false });

    expect(buttonWithText('Add top-level location')).toBeNull();
    expect(byLabel('Add location inside A')).toBeNull();
    expect(textOf(root())).toContain('Reactivate the warehouse to add locations.');
    expect(byLabel('Rename A')).not.toBeNull();   // the rest of the editing still works
  });

  // ---------- adding ----------

  it('adds a top-level location, then reloads the tree', async () => {
    await open();

    click(buttonWithText('Add top-level location'));
    fixture.detectChanges();
    expect(textOf(root())).toContain('New top-level location');
    expect(Array.from(root().querySelectorAll('#location-type option')).map((o) => o.textContent?.trim())).toEqual([
      'Zone',
      'Aisle',
      'Rack',
      'Shelf',
      'Bin',
    ]);

    typeInto(root(), '#location-code', '  C  ');
    typeInto(root(), '#location-name', 'Cold storage');
    submitForm(root());

    const request = http.expectOne(`${api}/warehouses/7/locations`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ parentId: null, code: 'C', name: 'Cold storage', type: 'Zone' });
    request.flush(location(6, 'C', 'Zone'));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Added C.');
    expect(root().querySelector('form')).toBeNull();   // the panel closed
    locationsRequest().flush([...tree(), location(6, 'C', 'Zone', null, { name: 'Cold storage' })]);
    await fixture.whenStable();

    // The reloaded tree now contains the new location.
    expect(Array.from(root().querySelectorAll('li')).map((li) => li.getAttribute('data-depth'))).toHaveLength(6);
    expect(textOf(root())).toContain('Cold storage');
  });

  it('adds a location INSIDE another, offering only the levels below it', async () => {
    await open();

    click(byLabel('Add location inside A-01'));
    fixture.detectChanges();

    expect(textOf(root())).toContain('New location inside A-01 (Aisle)');
    expect(Array.from(root().querySelectorAll('#location-type option')).map((o) => o.textContent?.trim())).toEqual([
      'Rack',
      'Shelf',
      'Bin',
    ]);

    typeInto(root(), '#location-code', 'A-01-R2');
    submitForm(root());

    const request = http.expectOne(`${api}/warehouses/7/locations`);
    expect(request.request.body).toEqual({ parentId: 2, code: 'A-01-R2', name: null, type: 'Rack' });
    request.flush(location(6, 'A-01-R2', 'Rack', 2));
    await fixture.whenStable();
    locationsRequest().flush(tree());
  });

  it('sends the chosen type, not just the first one', async () => {
    await open();

    click(byLabel('Add location inside A'));
    fixture.detectChanges();
    typeInto(root(), '#location-code', 'A-Q');
    chooseType('Bin');
    submitForm(root());

    const request = http.expectOne(`${api}/warehouses/7/locations`);
    expect(request.request.body.type).toBe('Bin');
    request.flush(location(6, 'A-Q', 'Bin', 1));
    await fixture.whenStable();
    locationsRequest().flush(tree());
  });

  it('requires a code and sends nothing without one', async () => {
    await open();

    click(buttonWithText('Add top-level location'));
    fixture.detectChanges();
    submitForm(root());
    fixture.detectChanges();

    expect(textOf(root())).toContain('Location code is required.');
    http.expectNone(`${api}/warehouses/7/locations`);
  });

  it('explains an invalid code and sends nothing', async () => {
    await open();

    click(buttonWithText('Add top-level location'));
    fixture.detectChanges();
    typeInto(root(), '#location-code', 'bad code!');
    submitForm(root());
    fixture.detectChanges();

    expect(textOf(root())).toContain('Use letters, digits, dots, hyphens, underscores and slashes');
    http.expectNone(`${api}/warehouses/7/locations`);
  });

  it('shows the server\'s duplicate-code error under the code field and keeps the panel open', async () => {
    await open();

    click(buttonWithText('Add top-level location'));
    fixture.detectChanges();
    typeInto(root(), '#location-code', 'A');
    submitForm(root());

    http.expectOne(`${api}/warehouses/7/locations`).flush(
      { title: 'One or more validation errors occurred.', errors: { Code: ['A location with this code already exists in this warehouse.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(textOf(root().querySelector('#location-code')!.parentElement!)).toContain(
      'A location with this code already exists in this warehouse.',
    );
    expect(root().querySelector('form')).not.toBeNull();
  });

  it('shows a business-rule refusal from the server as an alert in the panel', async () => {
    await open();

    click(byLabel('Add location inside A'));
    fixture.detectChanges();
    typeInto(root(), '#location-code', 'A-X');
    submitForm(root());

    http.expectOne(`${api}/warehouses/7/locations`).flush(
      { title: 'Business rule violation.', detail: 'The parent location is inactive. Reactivate it first.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    expect(textOf(root().querySelector('form')!)).toContain('The parent location is inactive. Reactivate it first.');
  });

  it('closes the panel on Cancel without sending anything', async () => {
    await open();

    click(buttonWithText('Add top-level location'));
    fixture.detectChanges();
    click(buttonWithText('Cancel'));
    fixture.detectChanges();

    expect(root().querySelector('form')).toBeNull();
  });

  // ---------- renaming ----------

  it('renames a location with the version it showed, then reloads', async () => {
    await open();

    click(byLabel('Rename A-02'));
    fixture.detectChanges();
    const input = root().querySelector<HTMLInputElement>('input[aria-label="Name of A-02"]')!;
    expect(input.value).toBe('Second aisle');

    input.value = '  Cold aisle  ';
    input.dispatchEvent(new Event('input'));
    click(buttonWithText('Save'));

    const request = http.expectOne(`${api}/warehouses/7/locations/4`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ version: 'v4', name: 'Cold aisle' });
    request.flush(location(4, 'A-02', 'Aisle', 1, { name: 'Cold aisle', version: 'v4b' }));
    await fixture.whenStable();

    expect(textOf(root())).toContain('Renamed A-02.');
    locationsRequest().flush(tree());
  });

  it('clears the name when it is renamed to nothing', async () => {
    await open();

    click(byLabel('Rename A-02'));
    fixture.detectChanges();
    const input = root().querySelector<HTMLInputElement>('input[aria-label="Name of A-02"]')!;
    input.value = '   ';
    input.dispatchEvent(new Event('input'));
    click(buttonWithText('Save'));

    const request = http.expectOne(`${api}/warehouses/7/locations/4`);
    expect(request.request.body.name).toBeNull();
    request.flush(location(4, 'A-02', 'Aisle', 1));
    await fixture.whenStable();
    locationsRequest().flush(tree());
  });

  it('leaves the name alone on Cancel', async () => {
    await open();

    click(byLabel('Rename A-02'));
    fixture.detectChanges();
    click(buttonWithText('Cancel'));
    fixture.detectChanges();

    expect(root().querySelector('input[aria-label="Name of A-02"]')).toBeNull();
    expect(textOf(root())).toContain('Second aisle');
  });

  it('explains a rename conflict (someone else changed it first) and refreshes the tree', async () => {
    await open();

    click(byLabel('Rename A-02'));
    fixture.detectChanges();
    click(buttonWithText('Save'));
    http.expectOne(`${api}/warehouses/7/locations/4`).flush(
      { title: 'Concurrency conflict.', detail: 'StorageLocation (4) was changed by someone else. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(textOf(root())).toContain('A-02 was changed by someone else.');
    locationsRequest().flush(tree());
  });

  // ---------- deactivating and reactivating ----------

  it('deactivates a location that has no active sub-locations, then reloads', async () => {
    await open();

    click(byLabel('Deactivate A-01-R1'));

    const request = http.expectOne(`${api}/warehouses/7/locations/3/deactivate`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ version: 'v3' });
    request.flush(location(3, 'A-01-R1', 'Rack', 2, { isActive: false }));
    await fixture.whenStable();

    expect(textOf(root())).toContain('A-01-R1 was deactivated.');
    locationsRequest().flush(tree());
  });

  it('does not let a location with active sub-locations be deactivated, and says what to do first', async () => {
    await open();

    const button = byLabel('Deactivate A-01')!;
    expect(button.disabled).toBe(true);
    expect(button.getAttribute('title')).toBe('Deactivate its 1 active sub-location(s) first.');
    expect(textOf(root())).toContain('Deactivate its 2 active sub-location(s) first.');   // the zone A has two aisles

    button.click();
    http.expectNone((request) => request.url.includes('/deactivate'));
  });

  it('shows a refusal from the server when a deactivation is rejected anyway', async () => {
    await open();

    click(byLabel('Deactivate A-01-R1'));
    http.expectOne(`${api}/warehouses/7/locations/3/deactivate`).flush(
      { title: 'Business rule violation.', detail: 'Deactivate its 1 active sub-location(s) first.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    expect(textOf(root())).toContain('Deactivate its 1 active sub-location(s) first.');
    http.expectNone((request) => request.method === 'GET');   // no reload for a plain refusal
  });

  it('explains a conflict on deactivate and refreshes the tree', async () => {
    await open();

    click(byLabel('Deactivate A-01-R1'));
    http.expectOne(`${api}/warehouses/7/locations/3/deactivate`).flush(
      { detail: 'conflict' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(textOf(root())).toContain('A-01-R1 was changed by someone else.');
    locationsRequest().flush(tree());
  });

  it('reactivates a location whose parent is active', async () => {
    await open([
      location(1, 'A', 'Zone'),
      location(2, 'A-01', 'Aisle', 1, { isActive: false, version: 'v2' }),
    ]);

    click(byLabel('Reactivate A-01'));

    const request = http.expectOne(`${api}/warehouses/7/locations/2/activate`);
    expect(request.request.body).toEqual({ version: 'v2' });
    request.flush(location(2, 'A-01', 'Aisle', 1));
    await fixture.whenStable();

    expect(textOf(root())).toContain('A-01 was reactivated.');
    locationsRequest().flush([location(1, 'A', 'Zone'), location(2, 'A-01', 'Aisle', 1)]);
  });

  it('does not let a location be reactivated while its parent is inactive, and says what to do first', async () => {
    await open([
      location(1, 'A', 'Zone', null, { isActive: false }),
      location(2, 'A-01', 'Aisle', 1, { isActive: false }),
    ]);

    const child = byLabel('Reactivate A-01')!;
    expect(child.disabled).toBe(true);
    expect(child.getAttribute('title')).toBe('Reactivate the parent location first.');
    expect(byLabel('Reactivate A')!.disabled).toBe(false);   // the top of the branch can come back first

    child.click();
    http.expectNone((request) => request.url.includes('/activate'));
  });

  it('marks inactive locations', async () => {
    await open([location(1, 'A', 'Zone', null, { isActive: false })]);

    expect(textOf(root())).toContain('Inactive');
  });
});