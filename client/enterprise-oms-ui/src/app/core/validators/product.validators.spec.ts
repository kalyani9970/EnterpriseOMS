import { FormControl } from '@angular/forms';
import { maxTwoDecimals, skuFormat, wholeNumber } from './product.validators';

describe('skuFormat', () => {
  const check = (value: unknown) => skuFormat(new FormControl(value));

  it.each(['A', 'a.b-c_d/e', '123', 'LAP-001', 'X/Y.2', '  LAP-001  '])('accepts %s', (sku) => {
    expect(check(sku)).toBeNull();
  });

  it.each(['-abc', 'a b', 'a#b', 'ab*', 'é1'])('rejects %s', (sku) => {
    expect(check(sku)).toEqual({ skuFormat: true });
  });

  it('leaves empty values to the required validator', () => {
    expect(check('')).toBeNull();
    expect(check(null)).toBeNull();
  });
});

describe('maxTwoDecimals', () => {
  const check = (value: unknown) => maxTwoDecimals(new FormControl(value));

  it.each([0, 1, 1.5, 899.99, 0.07, 10.1, 999999999.99])('accepts %s', (price) => {
    expect(check(price)).toBeNull();
  });

  it.each([1.005, 0.001, 2.999, 10.123])('rejects %s', (price) => {
    expect(check(price)).toEqual({ maxTwoDecimals: true });
  });

  it('does not fall for floating-point rounding (0.07 * 100 is 7.000000000000001)', () => {
    expect(check(0.07)).toBeNull();
    expect(check(1.15)).toBeNull();
  });

  it('leaves empty values to the required validator', () => {
    expect(check(null)).toBeNull();
    expect(check('')).toBeNull();
  });
});

describe('wholeNumber', () => {
  const check = (value: unknown) => wholeNumber(new FormControl(value));

  it.each([0, 1, 25, 1_000_000])('accepts %s', (n) => expect(check(n)).toBeNull());
  it.each([1.5, 0.1, 2.0001])('rejects %s', (n) => expect(check(n)).toEqual({ wholeNumber: true }));
  it('leaves empty values to the required validator', () => expect(check(null)).toBeNull());
});