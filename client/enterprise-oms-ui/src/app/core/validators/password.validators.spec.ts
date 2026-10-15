import { FormControl, FormGroup } from '@angular/forms';
import { passwordsMatch, strongPassword } from './password.validators';

describe('strongPassword', () => {
  const check = (value: string) => strongPassword(new FormControl(value));

  it('accepts a password that meets every rule', () => {
    expect(check('Passw0rd1')).toBeNull();
  });

  it('leaves empty values to the required validator', () => {
    expect(check('')).toBeNull();
  });

  it('reports exactly which rules failed', () => {
    expect(check('short')).toEqual({ strongPassword: { failed: ['minLength', 'uppercase', 'digit'] } });
    expect(check('alllowercase1')).toEqual({ strongPassword: { failed: ['uppercase'] } });
    expect(check('ALLUPPERCASE1')).toEqual({ strongPassword: { failed: ['lowercase'] } });
    expect(check('NoDigitsHere')).toEqual({ strongPassword: { failed: ['digit'] } });
  });
});

describe('passwordsMatch', () => {
  const group = (password: string, confirmPassword: string) =>
    new FormGroup({ password: new FormControl(password), confirmPassword: new FormControl(confirmPassword) });

  it('passes when both fields are equal', () => {
    expect(passwordsMatch(group('Passw0rd1', 'Passw0rd1'))).toBeNull();
  });

  it('flags a mismatch', () => {
    expect(passwordsMatch(group('Passw0rd1', 'Passw0rd2'))).toEqual({ passwordsMismatch: true });
  });
});