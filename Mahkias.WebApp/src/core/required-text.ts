import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Rejects blank and whitespace-only values so `control.invalid` matches a required field. */
export function requiredText(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'string' && value.trim() ? null : { required: true };
}
