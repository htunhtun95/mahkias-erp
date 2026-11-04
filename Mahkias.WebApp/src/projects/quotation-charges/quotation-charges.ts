import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface QuotationChargeRow {
  key: string;
  name: string;
  amount: string;
}

export interface QuotationChargePayload {
  name: string;
  amount: number;
}

export function readQuotationCharges(rows: QuotationChargeRow[]): { charges: QuotationChargePayload[]; error: string } {
  const charges: QuotationChargePayload[] = [];
  for (const row of rows) {
    const name = row.name.trim();
    const text = row.amount.trim().replace(/^\$/, '');
    if (!name && !text) {
      continue;
    }
    if (!name) {
      return { charges: [], error: 'Each additional charge needs a name.' };
    }
    if (name.length > 200) {
      return { charges: [], error: 'Additional charge names must be 200 characters or fewer.' };
    }
    const amount = chargeAmount(text);
    if (amount == null) {
      return { charges: [], error: 'Each additional charge needs an amount of zero or more.' };
    }
    charges.push({ name, amount });
  }
  return { charges, error: '' };
}

export function quotationChargeTotal(rows: QuotationChargeRow[]): number {
  return rows.reduce((sum, row) => {
    if (!row.name.trim()) {
      return sum;
    }
    const amount = chargeAmount(row.amount.trim().replace(/^\$/, ''));
    return amount == null ? sum : sum + amount;
  }, 0);
}

function chargeAmount(text: string): number | null {
  if (!text || /[a-z]/i.test(text)) {
    return null;
  }
  const parsed = Number(text);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

@Component({
  selector: 'app-quotation-charges',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="charges">
      <div class="charges-head">
        <h3>Additional charges</h3>
        <button class="add" type="button" [disabled]="disabled" (click)="add()">Add charge</button>
      </div>
      <div class="charge-scroll">
        @if (!rows.length) {
          <p class="empty">Add shipping, tax, or other amounts that apply to the whole {{ subject }}.</p>
        }
        @for (row of rows; track row.key) {
          <div class="charge-row">
            <input
              [(ngModel)]="row.name"
              [name]="'chargeName' + row.key"
              placeholder="Shipping charges"
              maxlength="200"
              autocomplete="off"
              [disabled]="disabled"
            />
            <label class="amount">
              <span>{{ symbol }}</span>
              <input
                [(ngModel)]="row.amount"
                [name]="'chargeAmount' + row.key"
                placeholder="0.00"
                inputmode="decimal"
                autocomplete="off"
                [disabled]="disabled"
              />
            </label>
            <button class="remove" type="button" [disabled]="disabled" (click)="remove(row.key)">Remove</button>
          </div>
        }
      </div>
      @if (completeCount) {
        <p class="subtotal">Additional charges {{ symbol }}{{ formattedTotal }} · included in the {{ subject }} total</p>
      }
    </section>
  `,
  styles: `
    :host {
      display: flex;
      justify-content: flex-end;
      flex: 0 0 auto;
      width: 100%;
      min-width: 0;
    }

    .charges {
      display: flex;
      flex-direction: column;
      gap: 8px;
      flex: 0 0 auto;
      box-sizing: border-box;
      width: 33.333%;
      min-width: min(100%, 320px);
      max-width: 100%;
      padding: 0;
    }

    .charge-scroll {
      display: flex;
      flex-direction: column;
      gap: 8px;
      flex: 0 0 auto;
      box-sizing: border-box;
      min-height: min-content;
      overflow: visible;
    }

    .charges:has(.charge-row) .charge-scroll {
      padding: 8px;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      background: #fff;
    }

    .charges-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }

    h3 {
      margin: 0;
      color: #202124;
      font-size: 14px;
      font-weight: 600;
    }

    .add,
    .remove {
      border: 0;
      background: transparent;
      color: #1a73e8;
      font: inherit;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;
    }

    .add:disabled,
    .remove:disabled {
      color: #9aa0a6;
      cursor: default;
    }

    .empty,
    .subtotal {
      margin: 0;
      color: #5f6368;
      font-size: 13px;
    }

    .charge-row {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 96px auto;
      gap: 8px;
      align-items: center;
    }

    input {
      width: 100%;
      height: 36px;
      box-sizing: border-box;
      border: 1px solid #dadce0;
      border-radius: 8px;
      padding: 0 12px;
      color: #202124;
      background: #fff;
      font: inherit;
      font-size: 13px;
    }

    input:focus {
      outline: none;
      border-color: #1a73e8;
    }

    .amount {
      display: flex;
      align-items: center;
      height: 36px;
      box-sizing: border-box;
      border: 1px solid #dadce0;
      border-radius: 8px;
      background: #fff;
      overflow: hidden;
    }

    .amount:focus-within {
      border-color: #1a73e8;
    }

    .amount span {
      padding-left: 12px;
      color: #5f6368;
      font-size: 13px;
    }

    .amount input {
      border: 0;
      height: 34px;
      padding-left: 6px;
    }

    .remove {
      padding: 0 4px;
    }
  `,
})
export class QuotationChargesComponent {
  @Input() rows: QuotationChargeRow[] = [];
  @Input() symbol = '$';
  @Input() subject = 'quotation';
  @Input() disabled = false;

  add() {
    this.rows.push({ key: crypto.randomUUID(), name: '', amount: '' });
  }

  remove(key: string) {
    const index = this.rows.findIndex((row) => row.key === key);
    if (index >= 0) {
      this.rows.splice(index, 1);
    }
  }

  get completeCount(): number {
    return this.rows.filter((row) => row.name.trim() && chargeAmount(row.amount.trim().replace(/^\$/, '')) != null).length;
  }

  get formattedTotal(): string {
    const total = this.rows.reduce((sum, row) => {
      if (!row.name.trim()) {
        return sum;
      }
      const amount = chargeAmount(row.amount.trim().replace(/^\$/, ''));
      return amount == null ? sum : sum + amount;
    }, 0);
    return total.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}
