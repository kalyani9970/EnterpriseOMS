import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="mx-auto max-w-md rounded-2xl bg-white p-10 text-center ring-1 ring-slate-200">
      <p class="text-sm font-semibold text-indigo-600">403</p>
      <h1 class="mt-2 text-2xl font-semibold text-slate-900">You do not have access to this page</h1>
      <p class="mt-2 text-sm text-slate-500">
        Your role does not include the permission this page needs. Ask an administrator if you think that is a mistake.
      </p>
      <a
        routerLink="/home"
        class="mt-6 inline-flex rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
      >
        Back to home
      </a>
    </section>
  `,
})
export class Forbidden {}