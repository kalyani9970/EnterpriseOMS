import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { Product } from '../../../core/models/product.models';
import { AuthService } from '../../../core/services/auth.service';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { ProductForm } from './product-form';

describe('ProductForm', () => {
  let http: HttpTestingController;
  let api: string;
  let navigate: ReturnType<typeof vi.spyOn>;
  let harness: RouterTestingHarness;
  let root: HTMLElement;

  const product = (overrides: Partial<Product> = {}): Product => ({
    id: 5,
    sku: 'lap-001',
    name: 'Business Laptop',
    description: '14 inch',
    category: 'Electronics',
    unitOfMeasure: 'Piece',
    unitPrice: 899,
    costPrice: 690,
    reorderLevel: 5,
    isActive: true,
    version: 'v1',
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
          { path: 'products/new', component: ProductForm },
          { path: 'products/:id', component: ProductForm },
        ]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  /** Opens the screen at a URL and answers the categories request. */
  async function open(url: string, permissions: string[]): Promise<void> {
    signIn(permissions);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, ProductForm);
    root = harness.routeNativeElement as HTMLElement;
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    http.expectOne(`${api}/products/categories`).flush(['Accessories', 'Electronics']);
    harness.detectChanges();
  }

  /** Opens the edit screen and answers the product request. */
  async function openEdit(permissions = ['products.view', 'products.update'], loaded: Product = product()): Promise<void> {
    await open('/products/5', permissions);
    http.expectOne(`${api}/products/5`).flush(loaded);
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  const openCreate = () => open('/products/new', ['products.view', 'products.create']);
  const field = (id: string) => root.querySelector<HTMLInputElement>(`#${id}`)!;
  const buttonWithText = (text: string) =>
    Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes(text)) ?? null;

  function fillValidNewProduct(): void {
    typeInto(root, '#sku', '  NEW-001  ');
    typeInto(root, '#name', '  New Item  ');
    typeInto(root, '#unitPrice', '12.5');
    typeInto(root, '#costPrice', '7');
    typeInto(root, '#reorderLevel', '10');
  }

  // ---------- create ----------

  it('starts empty, with an editable SKU and sensible defaults', async () => {
    await openCreate();

    expect(textOf(root)).toContain('New product');
    expect(field('sku').disabled).toBe(false);
    expect(field('unitOfMeasure').value).toBe('Piece');
    expect(field('unitPrice').value).toBe('0');
  });

  it('offers the existing categories as suggestions', async () => {
    await openCreate();

    const suggestions = Array.from(root.querySelectorAll('#category-suggestions option')).map((o) => o.getAttribute('value'));
    expect(suggestions).toEqual(['Accessories', 'Electronics']);
  });

  it('shows required errors and sends nothing when the form is empty', async () => {
    await openCreate();

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('SKU is required.');
    expect(textOf(root)).toContain('Product name is required.');
    http.expectNone(`${api}/products`);
  });

  it('rejects a name made only of spaces', async () => {
    await openCreate();
    fillValidNewProduct();
    typeInto(root, '#name', '     ');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Product name is required.');
    http.expectNone(`${api}/products`);
  });

  it('explains an invalid SKU', async () => {
    await openCreate();
    fillValidNewProduct();
    typeInto(root, '#sku', 'bad sku!');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Use letters, digits, dots, hyphens, underscores and slashes');
    http.expectNone(`${api}/products`);
  });

  it('rejects a price with more than two decimals', async () => {
    await openCreate();
    fillValidNewProduct();
    typeInto(root, '#unitPrice', '1.005');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Unit price can have at most 2 decimal places.');
    http.expectNone(`${api}/products`);
  });

  it('rejects a negative cost price and a fractional reorder level', async () => {
    await openCreate();
    fillValidNewProduct();
    typeInto(root, '#costPrice', '-1');
    typeInto(root, '#reorderLevel', '2.5');

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Cost price must be between 0 and 999,999,999.99.');
    expect(textOf(root)).toContain('Reorder level must be a whole number.');
    http.expectNone(`${api}/products`);
  });

  it('creates the product (trimmed, blank optionals as null) and returns to the list', async () => {
    await openCreate();
    fillValidNewProduct();

    submitForm(root);
    harness.detectChanges();

    const request = http.expectOne(`${api}/products`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      sku: 'NEW-001',
      name: 'New Item',
      description: null,
      category: null,
      unitOfMeasure: 'Piece',
      unitPrice: 12.5,
      costPrice: 7,
      reorderLevel: 10,
    });
    request.flush(product({ sku: 'NEW-001' }));

    expect(navigate).toHaveBeenCalledWith(['/products']);
  });

  it('shows the server\'s duplicate-SKU error under the SKU field and lets the user fix it', async () => {
    await openCreate();
    fillValidNewProduct();

    submitForm(root);
    harness.detectChanges();
    http.expectOne(`${api}/products`).flush(
      { title: 'One or more validation errors occurred.', errors: { Sku: ['A product with this SKU already exists.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    harness.detectChanges();

    expect(textOf(field('sku').parentElement!)).toContain('A product with this SKU already exists.');
    expect(navigate).not.toHaveBeenCalled();
    expect(root.querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(false);
  });

  // ---------- edit ----------

  it('loads the product into the form, with the SKU locked', async () => {
    await openEdit();

    expect(textOf(root)).toContain('Edit product');
    expect(field('sku').value).toBe('lap-001');
    expect(field('sku').disabled).toBe(true);
    expect(field('name').value).toBe('Business Laptop');
    expect(field('description').value).toBe('14 inch');
    expect(field('category').value).toBe('Electronics');
    expect(field('unitPrice').value).toBe('899');
    expect(field('reorderLevel').value).toBe('5');
    expect(textOf(root)).toContain('A SKU cannot be changed once the product exists.');
    expect(textOf(root)).toContain('Active');
  });

  it('saves changes, sending the version it loaded (and no SKU)', async () => {
    await openEdit();
    typeInto(root, '#name', 'Laptop Pro');
    typeInto(root, '#unitPrice', '999.99');

    submitForm(root);
    harness.detectChanges();

    const request = http.expectOne(`${api}/products/5`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      version: 'v1',
      name: 'Laptop Pro',
      description: '14 inch',
      category: 'Electronics',
      unitOfMeasure: 'Piece',
      unitPrice: 999.99,
      costPrice: 690,
      reorderLevel: 5,
    });
    request.flush(product({ name: 'Laptop Pro', unitPrice: 999.99, version: 'v2' }));
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('Saved.');
    expect(field('name').value).toBe('Laptop Pro');
  });

  it('uses the NEW version for the next save (so two saves in a row do not conflict with themselves)', async () => {
    await openEdit();

    typeInto(root, '#name', 'First');
    submitForm(root);
    http.expectOne(`${api}/products/5`).flush(product({ name: 'First', version: 'v2' }));
    await harness.fixture.whenStable();

    typeInto(root, '#name', 'Second');
    submitForm(root);

    const second = http.expectOne(`${api}/products/5`);
    expect(second.request.body.version).toBe('v2');
    second.flush(product({ name: 'Second', version: 'v3' }));
  });

  it('tells the user when someone else changed the product, without losing the screen', async () => {
    await openEdit();
    typeInto(root, '#name', 'My edit');

    submitForm(root);
    http.expectOne(`${api}/products/5`).flush(
      { title: 'Concurrency conflict.', detail: 'Product (5) was changed by someone else. Reload it and try again.' },
      { status: 409, statusText: 'Conflict' },
    );
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('Someone else changed this product while you were editing.');
    expect(textOf(root)).toContain('Your changes were not saved.');
    expect(field('name').value).toBe('My edit');   // their typing is still there until they choose to reload
  });

  it('loads the latest version on request, discarding the stale edit and the warning', async () => {
    await openEdit();
    typeInto(root, '#name', 'My edit');
    submitForm(root);
    http.expectOne(`${api}/products/5`).flush({ detail: 'conflict' }, { status: 409, statusText: 'Conflict' });
    await harness.fixture.whenStable();

    buttonWithText('Load the latest version')!.click();
    http.expectOne(`${api}/products/5`).flush(product({ name: 'Edited by someone else', version: 'v9' }));
    await harness.fixture.whenStable();

    expect(field('name').value).toBe('Edited by someone else');
    expect(textOf(root)).not.toContain('Someone else changed this product');
  });

  it('shows a clear message when the product does not exist (or belongs to another organization)', async () => {
    await open('/products/5', ['products.view']);
    http.expectOne(`${api}/products/5`).flush({}, { status: 404, statusText: 'Not Found' });
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('That product could not be found.');
    expect(root.querySelector('form')).toBeNull();
  });

  it('does not even ask the server about an id that is not a positive number', async () => {
    await open('/products/abc', ['products.view']);

    expect(textOf(root)).toContain('That product could not be found.');
    http.expectNone(`${api}/products/abc`);
  });

  it('shows a load error and retries on demand', async () => {
    await open('/products/5', ['products.view']);
    http.expectOne(`${api}/products/5`).flush({}, { status: 500, statusText: 'Server Error' });
    await harness.fixture.whenStable();
    expect(textOf(root)).toContain('The server ran into a problem.');

    buttonWithText('Try again')!.click();
    http.expectOne(`${api}/products/5`).flush(product());
    await harness.fixture.whenStable();

    expect(field('name').value).toBe('Business Laptop');
  });

  it('is read-only for a user who may view but not update', async () => {
    await openEdit(['products.view']);

    expect(field('name').disabled).toBe(true);
    expect(buttonWithText('Save changes')).toBeNull();
    expect(buttonWithText('Deactivate')).toBeNull();
    expect(textOf(root)).toContain('You can view this product but not change it.');
  });

  // ---------- activate / deactivate on the edit screen ----------

  it('deactivates the product with its current version and shows the new state', async () => {
    await openEdit();

    buttonWithText('Deactivate')!.click();

    const request = http.expectOne(`${api}/products/5/deactivate`);
    expect(request.request.body).toEqual({ version: 'v1' });
    request.flush(product({ isActive: false, version: 'v2' }));
    await harness.fixture.whenStable();

    expect(textOf(root)).toContain('Inactive');
    expect(buttonWithText('Reactivate')).not.toBeNull();
    expect(textOf(root)).toContain('Saved.');
  });

  it('reactivates an inactive product', async () => {
    await openEdit(['products.view', 'products.update'], product({ isActive: false, version: 'v4' }));

    buttonWithText('Reactivate')!.click();

    const request = http.expectOne(`${api}/products/5/activate`);
    expect(request.request.body).toEqual({ version: 'v4' });
    request.flush(product({ isActive: true, version: 'v5' }));
    await harness.fixture.whenStable();

    expect(buttonWithText('Deactivate')).not.toBeNull();
  });
});