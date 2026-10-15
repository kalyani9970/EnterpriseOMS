import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Role } from '../../core/models/role.models';
import { RolesService } from '../../core/services/roles.service';
import { parseApiError } from '../../core/utils/api-error';

type LoadStatus = 'loading' | 'loaded' | 'error';

/**
 * The first screen backed by a permission-protected endpoint: GET /api/roles needs "roles.view".
 * The route guard hides the page from users without it, and the API still returns 403 if the
 * guard were ever bypassed.
 */
@Component({
  selector: 'app-roles',
  templateUrl: './roles.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Roles implements OnInit {
  private readonly rolesService = inject(RolesService);

  protected readonly roles = signal<Role[]>([]);
  protected readonly status = signal<LoadStatus>('loading');
  protected readonly errorMessage = signal('');

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.status.set('loading');

    this.rolesService.getAll().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.status.set('loaded');
      },
      error: (error: unknown) => {
        this.errorMessage.set(parseApiError(error).message);
        this.status.set('error');
      },
    });
  }
}