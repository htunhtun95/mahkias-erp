import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, TemplateRef, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { Activity, ActivityGroup } from '../../core/services/project-api.service';
import { PartSuggestion, partSuggestions } from '../quotation-map';

@Component({
  selector: 'app-part-autocomplete',
  standalone: true,
  template: `
    <input
      #origin
      [value]="value"
      [disabled]="disabled"
      autocomplete="off"
      aria-label="Part no"
      (input)="onInput($any($event.target).value)"
      (focus)="onFocus()"
    />
    <ng-template #menu>
      <div class="part-menu" role="listbox">
        @for (suggestion of suggestions; track suggestion.kind + suggestion.value) {
          <button type="button" role="option" (mousedown)="$event.preventDefault()" (click)="choose(suggestion)">
            {{ suggestion.label }}
          </button>
        }
      </div>
    </ng-template>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    input {
      width: 100%;
      height: 36px;
      border: 0;
      background: transparent;
      padding: 0 8px;
      color: #202124;
      font: inherit;
      font-size: 13px;
      box-sizing: border-box;
    }
    input:disabled { color: #80868b; }
    .part-menu {
      max-height: 280px;
      overflow: auto;
      padding: 6px;
      border: 1px solid #dadce0;
      border-radius: 10px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(32, 33, 36, 0.18);
    }
    .part-menu button {
      display: block;
      width: 100%;
      padding: 8px;
      border: 0;
      border-radius: 6px;
      background: transparent;
      color: #202124;
      font: inherit;
      font-size: 13px;
      text-align: left;
      cursor: pointer;
    }
    .part-menu button:hover { background: #f8f9fa; }
  `,
})
export class PartAutocompleteComponent implements OnChanges, OnDestroy {
  @Input() value = '';
  @Input() activities: Activity[] = [];
  @Input() groups: ActivityGroup[] = [];
  @Input() disabled = false;
  @Output() valueChange = new EventEmitter<string>();
  @Output() picked = new EventEmitter<PartSuggestion>();
  @ViewChild('origin') private origin?: ElementRef<HTMLInputElement>;
  @ViewChild('menu') private menu?: TemplateRef<unknown>;

  suggestions: PartSuggestion[] = [];

  private overlay = inject(Overlay);
  private viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;
  private outsideListener: ((event: Event) => void) | null = null;
  private focused = false;

  ngOnChanges() {
    this.suggestions = partSuggestions(this.value, this.activities, this.groups);
  }

  ngOnDestroy() {
    this.close();
  }

  onFocus() {
    this.focused = true;
    this.refresh();
  }

  onInput(value: string) {
    this.focused = true;
    this.valueChange.emit(value);
    this.suggestions = partSuggestions(value, this.activities, this.groups);
    this.refresh();
  }

  choose(suggestion: PartSuggestion) {
    this.picked.emit(suggestion);
    this.close();
  }

  private refresh() {
    if (!this.focused || this.disabled || this.suggestions.length === 0) {
      this.close();
      return;
    }
    if (!this.overlayRef) {
      this.open();
    }
  }

  private open() {
    if (!this.origin || !this.menu) {
      return;
    }
    const width = Math.max(this.origin.nativeElement.offsetWidth, 220);
    const positionStrategy = this.overlay.position()
      .flexibleConnectedTo(this.origin)
      .withFlexibleDimensions(false)
      .withPush(true)
      .withViewportMargin(8)
      .withPositions([
        { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
        { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
      ]);
    this.overlayRef = this.overlay.create({
      positionStrategy,
      scrollStrategy: this.overlay.scrollStrategies.reposition(),
      hasBackdrop: false,
      panelClass: 'quotation-menu-panel',
      width,
    });
    this.overlayRef.attach(new TemplatePortal(this.menu, this.viewContainerRef));
    this.bindOutside((event) => {
      const target = event.target as Node | null;
      const origin = this.origin?.nativeElement;
      const pane = this.overlayRef?.overlayElement;
      if (target && (origin?.contains(target) || pane?.contains(target))) {
        return;
      }
      this.focused = false;
      this.close();
    });
  }

  private bindOutside(onPointer: (event: Event) => void) {
    this.outsideListener = onPointer;
    setTimeout(() => {
      if (this.overlayRef && this.outsideListener === onPointer) {
        document.addEventListener('mousedown', onPointer);
      }
    });
  }

  private close() {
    if (this.outsideListener) {
      document.removeEventListener('mousedown', this.outsideListener);
      this.outsideListener = null;
    }
    this.overlayRef?.dispose();
    this.overlayRef = null;
  }
}
