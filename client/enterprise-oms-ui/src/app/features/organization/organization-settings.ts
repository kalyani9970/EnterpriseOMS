import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Organization } from '../../core/models/organization.models';
import { Permissions } from '../../core/security/permissions';
import { AuthService } from '../../core/services/auth.service';
import { OrganizationService } from '../../core/services/organization.service';
import { parseApiError } from '../../core/utils/api-error';

type LoadStatus = 'loading' | 'loaded' | 'error';

/**
 * Shows the signed-in user's own organization. Anyone with "organizations.view" can read it;
 * only holders of "organizations.update" see the rename form. The API enforces both rules again.
 */
@Component({
  selector: 'app-organization-settings',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './organization-settings.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationSettings implements OnInit {
  private readonly organizationService = inject(OrganizationService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly organization = signal<Organization | null>(null);
  protected readonly status = signal<LoadStatus>('loading');
  protected readonly loadError = signal('');

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly canEdit = computed(() => this.auth.hasPermission(Permissions.Organizations.Update));

  protected readonly form = this.formBuilder.group({
    // pattern(/\S/) rejects a name made only of spaces, which Validators.required would accept.
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.minLength(2), Validators.maxLength(200)]],
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.status.set('loading');

    this.organizationService.getCurrent().subscribe({
      next: (organization) => {
        this.organization.set(organization);
        this.form.reset({ name: organization.name });
        this.status.set('loaded');
      },
      error: (error: unknown) => {
        this.loadError.set(parseApiError(error).message);
        this.status.set('error');
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saved.set(false);
    this.saveError.set(null);

    this.organizationService.rename(this.form.getRawValue().name.trim()).subscribe({
      next: (updated) => {
        this.organization.set(updated);
        this.form.reset({ name: updated.name });
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (error: unknown) => {
        this.saveError.set(parseApiError(error).message);
        this.saving.set(false);
      },
    });
  }

  /** True while the box still holds the name that is already saved, so there is nothing to submit. */
  protected isUnchanged(): boolean {
    return this.form.controls.name.value.trim() === this.organization()?.name;
  }

  protected showError(control: AbstractControl): boolean {
    return control.invalid && (control.touched || control.dirty);
  }
}