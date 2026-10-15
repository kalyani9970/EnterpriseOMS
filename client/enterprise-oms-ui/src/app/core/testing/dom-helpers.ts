/** Types into an input the way a user would, so Angular forms see the change. */
export function typeInto(root: HTMLElement, selector: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(selector);

  if (!input) {
    throw new Error(`No element matches "${selector}".`);
  }

  input.value = value;
  input.dispatchEvent(new Event('input'));
}

export function submitForm(root: HTMLElement): void {
  const form = root.querySelector('form');

  if (!form) {
    throw new Error('No form found.');
  }

  form.dispatchEvent(new Event('submit'));
}

/** Visible text with whitespace collapsed, for readable assertions. */
export function textOf(root: HTMLElement): string {
  return (root.textContent ?? '').replace(/\s+/g, ' ').trim();
}