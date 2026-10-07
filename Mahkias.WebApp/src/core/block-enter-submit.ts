/** Stops Enter from submitting a form. Textareas keep the key so it inserts a new line. */
export function blockEnterSubmit(event: Event) {
  const tag = (event.target as HTMLElement | null)?.tagName;
  if (tag === 'TEXTAREA') {
    return;
  }
  event.preventDefault();
}
