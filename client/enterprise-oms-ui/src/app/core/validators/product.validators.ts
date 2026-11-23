import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Mirrors the server's SKU rule: letters, digits, dots, hyphens, underscores and slashes,
 * starting with a letter or digit. Empty values are left to Validators.required.
 */
export const skuFormat: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value.trim() : '';

  if (value.length === 0) return null;

  return /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(value) ? null : { skuFormat: true };
};

/**
 * Money allows at most 2 decimal places. The check is done on the TEXT of the number, because
 * floating-point maths gets it wrong (0.07 * 100 is 7.000000000000001 in JavaScript).
 */
export const maxTwoDecimals: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;

  if (value === null || value === undefined || value === '') return null;

  return /^-?\d+(\.\d{1,2})?$/.test(String(value)) ? null : { maxTwoDecimals: true };
};

/** Whole numbers only (reorder level counts items). */
export const wholeNumber: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;

  if (value === null || value === undefined || value === '') return null;

  return Number.isInteger(Number(value)) ? null : { wholeNumber: true };
};