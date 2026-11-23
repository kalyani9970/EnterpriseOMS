import { FormControl } from '@angular/forms';
import { codeFormat, countryCode } from './warehouse.validators';

describe('codeFormat', () => {
  const check = (value: unknown) => codeFormat(new FormControl(value));

  it.each(['A', 'wh-main', 'W.1/2', '123', 'A-01-R1-S1-B1', '  WH-1  '])('accepts %s', (code) => {
    expect(check(code)).toBeNull();
  });

  it.each(['-x', 'a b', 'a#b', 'é1', 'A*'])('rejects %s', (code) => {
    expect(check(code)).toEqual({ codeFormat: true });
  });

  it('leaves empty values to the required validator', () => {
    expect(check('')).toBeNull();
    expect(check(null)).toBeNull();
  });
});

describe('countryCode', () => {
  const check = (value: unknown) => countryCode(new FormControl(value));

  it.each(['NL', 'in', ' De ', 'BE'])('accepts %s', (code) => {
    expect(check(code)).toBeNull();
  });

  it.each(['I', 'IND', '1N', 'I-', 'N L'])('rejects %s', (code) => {
    expect(check(code)).toEqual({ countryCode: true });
  });

  it('leaves empty values to the required validator', () => {
    expect(check('')).toBeNull();
    expect(check(null)).toBeNull();
  });
});