import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.user;

  /** Permissions grouped by module ("orders.create" belongs to "orders"), for a readable display. */
  protected readonly permissionGroups = computed(() => {
    const groups = new Map<string, string[]>();

    for (const permission of this.user()?.permissions ?? []) {
      const module = permission.split('.')[0] ?? permission;
      groups.set(module, [...(groups.get(module) ?? []), permission]);
    }

    return [...groups.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([module, items]) => ({ module, items: [...items].sort() }));
  });
}