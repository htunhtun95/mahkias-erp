import { AfterViewInit, Component, ElementRef, Input, ViewChild, forwardRef } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { DateAdapter, MAT_DATE_FORMATS, NativeDateAdapter } from '@angular/material/core';
import { MatDatepickerInput, MatDatepickerInputEvent, MatDatepickerModule } from '@angular/material/datepicker';
import { MaterialModule } from '../../app/material-module';

class QuotationDateAdapter extends NativeDateAdapter {
  override parse(value: unknown): Date | null {
    if (typeof value === 'string') {
      const text = value.trim();
      if (!text) {
        return null;
      }
      return parseDisplayDate(text) ?? this.invalid();
    }
    if (value instanceof Date) {
      return Number.isNaN(value.getTime()) ? this.invalid() : value;
    }
    return null;
  }

  override format(date: Date, displayFormat: Intl.DateTimeFormatOptions): string {
    if (displayFormat.day === '2-digit' && displayFormat.month === '2-digit') {
      return formatDisplayDate(date);
    }
    return super.format(date, displayFormat);
  }
}

const QUOTATION_DATE_FORMATS = {
  parse: {
    dateInput: 'dd/MM/yyyy',
  },
  display: {
    dateInput: { day: '2-digit', month: '2-digit', year: 'numeric' },
    monthYearLabel: { month: 'short', year: 'numeric' },
    dateA11yLabel: { day: 'numeric', month: 'long', year: 'numeric' },
    monthYearA11yLabel: { month: 'long', year: 'numeric' },
  },
};

@Component({
  selector: 'app-quotation-date-field',
  standalone: true,
  imports: [FormsModule, MaterialModule, MatDatepickerModule],
  providers: [
    { provide: DateAdapter, useClass: QuotationDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: QUOTATION_DATE_FORMATS },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => QuotationDateFieldComponent),
      multi: true,
    },
  ],
  templateUrl: './quotation-date-field.html',
  styleUrl: './quotation-date-field.css',
})
export class QuotationDateFieldComponent implements ControlValueAccessor, AfterViewInit {
  @Input() label = 'Date';
  @Input() disabled = false;
  @Input() externalError = '';
  @ViewChild('dateBox') private inputEl?: ElementRef<HTMLInputElement>;
  @ViewChild(MatDatepickerInput) private dateInput?: MatDatepickerInput<Date>;

  showError = false;
  private date: Date | null = null;
  private touched = false;
  private queued: Date | null = null;
  private queuedSet = false;
  private lastEmitted = '';
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  get hasValue(): boolean {
    return !!this.inputText() || !!this.date;
  }

  get invalid(): boolean {
    const text = this.inputText();
    return !!text && !parseDisplayDate(text);
  }

  ngAfterViewInit(): void {
    if (this.queuedSet) {
      this.apply(this.queued);
      this.queuedSet = false;
    }
  }

  writeValue(value: string | null): void {
    const next = parseIsoDate(value);
    const text = this.inputText();
    const current = text ? parseDisplayDate(text) : null;
    this.lastEmitted = formatIsoDate(next);
    if (sameDay(next, current)) {
      this.date = next;
      if (current || !text.trim()) {
        this.showError = false;
      }
      return;
    }
    if (!next && text && !current) {
      this.date = null;
      return;
    }
    this.date = next;
    this.showError = false;
    if (this.dateInput) {
      this.apply(next);
    } else {
      this.queued = next;
      this.queuedSet = true;
    }
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

  reveal(): void {
    this.touched = true;
    this.syncError(true);
  }

  clear(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.disabled) {
      return;
    }
    this.touched = false;
    this.showError = false;
    this.date = null;
    this.apply(null);
    this.publish(null);
  }

  onTyped(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.syncError(false, text);
    this.publish(parseDisplayDate(text));
  }

  onBlur(): void {
    this.touched = true;
    this.onTouched();
    this.syncError(true);
    this.publish(parseDisplayDate(this.inputText()));
  }

  onPicked(event: MatDatepickerInputEvent<Date>): void {
    const text = this.inputText().trim();
    const parsed = parseDisplayDate(text);
    this.touched = true;
    if (parsed || event.value) {
      this.showError = false;
      this.publish(event.value ?? parsed);
      return;
    }
    if (text) {
      this.syncError(true);
      this.publish(null);
      return;
    }
    this.showError = false;
    this.publish(null);
  }

  private apply(value: Date | null): void {
    if (this.dateInput) {
      this.dateInput.value = value;
    }
  }

  private publish(value: Date | null): void {
    const iso = formatIsoDate(value);
    this.date = value;
    if (iso === this.lastEmitted) {
      return;
    }
    this.lastEmitted = iso;
    this.onChange(iso);
  }

  private syncError(force: boolean, text = this.inputText()): void {
    const value = text.trim();
    if (!value || parseDisplayDate(value)) {
      this.showError = false;
      return;
    }
    this.showError = force || !isPartialDate(value);
  }

  private inputText(): string {
    return this.inputEl?.nativeElement.value ?? '';
  }
}

function parseDisplayDate(value: string): Date | null {
  const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(value.trim());
  if (!match) {
    return null;
  }
  const day = Number(match[1]);
  const month = Number(match[2]);
  const year = Number(match[3]);
  if (month < 1 || month > 12 || day < 1 || year < 1000) {
    return null;
  }
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}

function isPartialDate(value: string): boolean {
  const text = value.trim();
  if (/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(text)) {
    return false;
  }
  return /^(\d{0,2})(\/(\d{0,2})(\/\d{0,4})?)?$/.test(text);
}

function parseIsoDate(value?: string | null): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value ?? '');
  if (!match) {
    return null;
  }
  return parseDisplayDate(`${match[3]}/${match[2]}/${match[1]}`);
}

function formatDisplayDate(value: Date): string {
  const day = String(value.getDate()).padStart(2, '0');
  const month = String(value.getMonth() + 1).padStart(2, '0');
  return `${day}/${month}/${value.getFullYear()}`;
}

function formatIsoDate(value: Date | null): string {
  if (!value) {
    return '';
  }
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

function sameDay(left: Date | null, right: Date | null): boolean {
  if (!left || !right) {
    return !left && !right;
  }
  return left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth() && left.getDate() === right.getDate();
}
