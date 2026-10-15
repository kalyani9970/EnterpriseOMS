import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { parseApiError } from '../../../core/utils/api-error';
import { PASSWORD_RULES, passwordsMatch, strongPassword } from '../../../core/validators/password.validators';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = this.formBuilder.group(
    {
      fullName: ['', [Validators.required, Validators.maxLength(200)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', [Validators.required, strongPassword]],
      confirmPassword: ['', [Validators.required]],
      // Temporary: the Organization module will replace this field with an invite or a picker.
      organizationId: [1, [Validators.required, Validators.min(1)]],
    },
    { validators: passwordsMatch },
  );

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  /**
   * Angular concept: toSignal turns an Observable (the control's value stream) into a signal,
   * so the template can react to typing without subscriptions or zone.js.
   */
  private readonly passwordValue = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });

  protected readonly passwordChecklist = computed(() =>
    PASSWORD_RULES.map((rule) => ({ label: rule.label, met: rule.test(this.passwordValue()) })),
  );

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.serverErrors.set({});

    const { fullName, email, password, organizationId } = this.form.getRawValue();

    this.auth.register({ fullName, email, password, organizationId }).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl('/');
      },
      error: (error: unknown) => {
        const apiError = parseApiError(error);
        this.errorMessage.set(apiError.message);
        this.serverErrors.set(apiError.fieldErrors);
        this.submitting.set(false);
      },
    });
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && (control.touched || control.dirty);
  }

  protected serverError(field: string): string | undefined {
    return this.serverErrors()[field]?.[0];
  }

  protected showMismatch(): boolean {
    const confirm = this.form.controls.confirmPassword;
    return this.form.hasError('passwordsMismatch') && (confirm.touched || confirm.dirty);
  }
}