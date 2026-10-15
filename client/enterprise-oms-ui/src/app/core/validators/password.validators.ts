import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * The password rules, mirroring RegisterCommandValidator on the API (the server stays the
 * authority; these give instant feedback). Exported as data so the register form can render
 * a live checklist from the same list the validator uses.
 */
export const PASSWORD_RULES = [
  { key: 'minLength', label: 'At least 8 characters', test: (value: string) => value.length >= 8 },
  { key: 'uppercase', label: 'One uppercase letter', test: (value: string) => /[A-Z]/.test(value) },
  { key: 'lowercase', label: 'One lowercase letter', test: (value: string) => /[a-z]/.test(value) },
  { key: 'digit', label: 'One digit', test: (value: string) => /[0-9]/.test(value) },
] as const;

/** Empty values are left to Validators.required, so the two rules do not double-report. */
export const strongPassword: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value : '';

  if (value.length === 0) {
    return null;
  }

  const failed = PASSWORD_RULES.filter((rule) => !rule.test(value)).map((rule) => rule.key);

  return failed.length > 0 ? { strongPassword: { failed } } : null;
};

/** Group-level validator: password and confirmPassword must match. */
export const passwordsMatch: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const password = group.get('password')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;

  return password === confirmPassword ? null : { passwordsMismatch: true };
};