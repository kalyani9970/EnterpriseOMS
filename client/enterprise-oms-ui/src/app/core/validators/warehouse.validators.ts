import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Mirrors the server's rule for warehouse and location codes: letters, digits, dots, hyphens,
 * underscores and slashes, starting with a letter or digit. Empty values are left to Validators.required.
 */
export const codeFormat: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value.trim() : '';

  if (value.length === 0) return null;

  return /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(value) ? null : { codeFormat: true };
};

/**
 * A two-letter ISO country code such as "NL" or "in". Case does not matter: the server stores it
 * upper-cased. Empty values are left to Validators.required.
 */
export const countryCode: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value.trim() : '';

  if (value.length === 0) return null;

  return /^[A-Za-z]{2}$/.test(value) ? null : { countryCode: true };
};