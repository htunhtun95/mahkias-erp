import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, TemplateRef, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { Subject, Subscription, of } from 'rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';
import { AutoGrowDirective } from '../../core/auto-grow';
import { PartSuggestion, ProjectApiService } from '../../core/services/project-api.service';

@Component({
  selector: 'app-part-suggest',
  standalone: true,
  imports: [AutoGrowDirective],
  template: `
    <textarea
      #origin
      appAutoGrow
      [attr.data-min-height]="minHeight"
      rows="1"
      [value]="value"
      [disabled]="disabled"
      autocomplete="off"
      role="combobox"
      aria-autocomplete="list"
      [attr.aria-label]="ariaLabel"
      [attr.aria-expanded]="open"
      [attr.aria-invalid]="invalid ? 'true' : null"
      [attr.title]="value || null"
      (input)="onInput($any($event.target).value)"
      (focus)="onFocus()"
      (blur)="onBlur()"
      (keydown)="onKey($event)"
    ></textarea>
    <ng-template #menu>
      <div class="part-menu" role="listbox">
        @for (suggestion of suggestions; track trackSuggestion(suggestion); let index = $index) {
          <button
            type="button"
            role="option"
            [class.active]="index === activeIndex"
            (mouseenter)="activeIndex = index"
            (mousedown)="$event.preventDefault()"
            (click)="choose(suggestion)"
          >
            <span class="part-no">{{ suggestion.partNo }}</span>
            @if (suggestion.description) {
              <span class="part-description">{{ suggestion.description }}</span>
            }
          </button>
        }
      </div>
    </ng-template>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    textarea {
      display: block;
      box-sizing: border-box;
      width: 100%;
      margin: 0;
      border: 0;
      border-radius: 0;
      outline: none;
      background: transparent;
      color: #202124;
      font: inherit;
      resize: none;
      overflow: hidden;
      box-shadow: none;
    }
    .part-menu {
      box-sizing: border-box;
      max-height: 280px;
      overflow: auto;
      padding: 6px;
      border: 1px solid #dadce0;
      border-radius: 8px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(32, 33, 36, 0.18);
    }
    .part-menu button {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 2px;
      width: 100%;
      min-height: 36px;
      margin: 0;
      padding: 8px 12px;
      border: 0;
      border-radius: 6px;
      background: transparent;
      color: #202124;
      font: inherit;
      font-size: 13px;
      text-align: left;
      cursor: pointer;
    }
    .part-menu button.active,
    .part-menu button:hover { background: #e8f0fe; }
    .part-no {
      max-width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-weight: 500;
    }
    .part-description {
      max-width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      color: #5f6368;
      font-size: 12px;
    }
  `,
})
export class PartSuggestComponent implements OnDestroy {
  @Input() value = '';
  @Input() disabled = false;
  @Input() invalid = false;
  @Input() ariaLabel = 'Part No';
  @Input() minHeight = 36;
  @Input() minQueryLength = 0;
  @Output() valueChange = new EventEmitter<string>();
  @Output() picked = new EventEmitter<PartSuggestion>();
  @ViewChild('origin') private origin?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('menu') private menu?: TemplateRef<unknown>;

  suggestions: PartSuggestion[] = [];
  activeIndex = 0;
  open = false;

  private api = inject(ProjectApiService);
  private overlay = inject(Overlay);
  private viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;
  private outsideListener: ((event: Event) => void) | null = null;
  private focused = false;
  private latestQuery = '';
  private readonly queries = new Subject<string>();
  private readonly searchSub: Subscription;

  constructor() {
    this.searchSub = this.queries.pipe(
      debounceTime(150),
      switchMap((query) => {
        const text = query.trim();
        if (!text || text.length < this.minQueryLength) {
          return of([]);
        }
        return this.api.searchParts(text);
      }),
    ).subscribe((matches) => {
      if (!this.canSearch(this.latestQuery)) {
        this.suggestions = [];
        this.activeIndex = 0;
        this.close();
        return;
      }
      this.suggestions = matches ?? [];
      this.activeIndex = 0;
      this.show();
    });
  }

  ngOnDestroy() {
    this.searchSub.unsubscribe();
    this.close();
  }

  onFocus() {
    if (this.disabled) {
      return;
    }
    this.focused = true;
    this.latestQuery = this.value ?? '';
    if (!this.canSearch(this.latestQuery)) {
      this.suggestions = [];
      this.activeIndex = 0;
      this.close();
    }
    this.queries.next(this.latestQuery);
  }

  onInput(value: string) {
    if (this.disabled) {
      return;
    }
    this.focused = true;
    this.latestQuery = value;
    this.valueChange.emit(value);
    if (!this.canSearch(value)) {
      this.suggestions = [];
      this.activeIndex = 0;
      this.close();
    }
    this.queries.next(value);
  }

  onBlur() {
    this.focused = false;
    this.close();
  }

  onKey(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      if (this.open && this.suggestions.length) {
        event.stopPropagation();
        this.choose(this.suggestions[this.activeIndex] ?? this.suggestions[0]);
      }
      return;
    }
    if (!this.open) {
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.move(1);
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.move(-1);
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.close();
    }
  }

  choose(suggestion: PartSuggestion) {
    this.picked.emit(suggestion);
    this.close();
  }

  trackSuggestion(suggestion: PartSuggestion) {
    return `${suggestion.partId}:${suggestion.description ?? ''}`;
  }

  private canSearch(value: string): boolean {
    return value.trim().length >= this.minQueryLength;
  }

  private show() {
    if (!this.focused || this.disabled || !this.canSearch(this.latestQuery) || this.suggestions.length === 0) {
      this.close();
      return;
    }
    if (!this.overlayRef) {
      this.openPanel();
    }
    this.open = true;
  }

  private move(step: number) {
    if (!this.suggestions.length) {
      return;
    }
    const next = this.activeIndex < 0 ? 0 : (this.activeIndex + step + this.suggestions.length) % this.suggestions.length;
    this.activeIndex = next;
  }

  private openPanel() {
    if (!this.origin || !this.menu || this.overlayRef) {
      return;
    }
    const width = Math.max(this.origin.nativeElement.getBoundingClientRect().width, 280);
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
    const onPointer = (event: Event) => {
      const target = event.target as Node | null;
      const origin = this.origin?.nativeElement;
      const pane = this.overlayRef?.overlayElement;
      if (target && (origin?.contains(target) || pane?.contains(target))) {
        return;
      }
      this.focused = false;
      this.close();
    };
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
    this.open = false;
  }
}
