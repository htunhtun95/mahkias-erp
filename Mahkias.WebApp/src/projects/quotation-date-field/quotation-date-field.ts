import { Component, forwardRef, Input, ViewChild } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepicker, MatDatepickerModule } from '@angular/material/datepicker';
import { MaterialModule } from '../../app/material-module';

@Component({
  selector: 'app-quotation-date-field',
  standalone: true,
  imports: [FormsModule, MaterialModule, MatDatepickerModule],
  providers: [
    provideNativeDateAdapter(),
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => QuotationDateFieldComponent),
      multi: true,
    },
  ],
  templateUrl: './quotation-date-field.html',
  styleUrl: './quotation-date-field.css',
})
export class QuotationDateFieldComponent implements ControlValueAccessor {
  @Input() label = 'Date';
  @Input() disabled = false;
  @ViewChild('picker') private picker?: MatDatepicker<Date>;

  date: Date | null = null;
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string | null): void {
    this.date = parseDate(value);
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  open(): void {
    if (this.disabled || this.picker?.opened) {
      return;
    }
    this.picker?.open();
  }

  clear(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.disabled) {
      return;
    }
    this.choose(null);
  }

  choose(value: Date | null): void {
    this.date = value && !Number.isNaN(value.getTime()) ? value : null;
    this.onChange(formatDate(this.date));
    this.onTouched();
  }

  blockTyping(event: KeyboardEvent): void {
    if (event.key === 'Tab' || event.key === 'Escape' || event.key === 'Shift' || event.altKey) {
      return;
    }
    event.preventDefault();
  }
}

function parseDate(value?: string | null): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value ?? '');
  if (!match) {
    return null;
  }
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatDate(value: Date | null): string {
  if (!value) {
    return '';
  }
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}
