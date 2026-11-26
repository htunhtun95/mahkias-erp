import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, TemplateRef, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { Subject, Subscription, of } from 'rxjs';
import { catchError, debounceTime, switchMap } from 'rxjs/operators';
import { Activity, PartSuggestion, ProjectApiService } from '../../core/services/project-api.service';
import { ActivitySuggestion, activitySuggestions } from '../quotation-map';

@Component({
  selector: 'app-activity-suggest',
  standalone: true,
  template: `
    <input
      #origin
      [value]="value"
      [disabled]="disabled"
      [readOnly]="readOnly"
      autocomplete="off"
      role="combobox"
      aria-autocomplete="list"
      [attr.aria-label]="ariaLabel"
      [attr.aria-expanded]="open"
      (input)="onInput($any($event.target).value)"
      (focus)="onFocus()"
      (blur)="onBlur()"
      (keydown)="onKey($event)"
    />
    <ng-template #menu>
      <div class="activity-menu" role="listbox">
        @for (suggestion of suggestions; track trackSuggestion(suggestion); let index = $index) {
          @if (groupHeading(index); as heading) {
            <div class="menu-heading">{{ heading }}</div>
          }
          <button
            type="button"
            role="option"
            [class.active]="index === activeIndex"
            [class.catalog]="suggestion.source === 'part'"
            (mouseenter)="activeIndex = index"
            (mousedown)="$event.preventDefault()"
            (click)="choose(suggestion)"
          >
            <span class="option-label">{{ suggestion.label }}</span>
            @if (suggestion.source === 'part' && suggestion.description) {
              <span class="option-description">{{ suggestion.description }}</span>
            }
            @if (suggestion.role === 'alternate') {
              <span class="option-badge">Alt</span>
            }
          </button>
        }
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
      box-sizing: border-box;
      width: 100%;
      height: 100%;
      min-width: 0;
      min-height: 0;
    }
    input {
      display: block;
      box-sizing: border-box;
      width: 100%;
      height: 100%;
      min-height: 0;
      margin: 0;
      padding: 6px 8px;
      border: 0;
      border-radius: 0;
      outline: none;
      background: transparent;
      color: #111827;
      font: inherit;
      font-size: 13px;
      line-height: 26px;
    }
    input:disabled { color: #80868b; }
    input[readonly] { cursor: default; }
    .activity-menu {
      box-sizing: border-box;
      max-height: 280px;
      overflow: auto;
      padding: 6px;
      border: 1px solid #dadce0;
      border-radius: 8px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(32, 33, 36, 0.18);
    }
    .activity-menu button {
      display: flex;
      align-items: center;
      gap: 8px;
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
    .activity-menu button.active,
    .activity-menu button:hover { background: #e8f0fe; }
    .activity-menu button.catalog {
      flex-direction: column;
      align-items: flex-start;
      gap: 2px;
    }
    .menu-heading {
      padding: 6px 12px 2px;
      color: #5f6368;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .option-label {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .option-description {
      max-width: 100%;
      overflow: hidden;
      color: #5f6368;
      font-size: 12px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .option-badge {
      flex: 0 0 auto;
      margin-left: auto;
      padding: 1px 7px;
      border-radius: 999px;
      background: #f1f3f4;
      color: #5f6368;
      font-size: 11px;
      font-weight: 700;
      line-height: 16px;
      letter-spacing: 0.02em;
    }
  `,
})
export class ActivitySuggestComponent implements OnChanges, OnDestroy {
  @Input() value = '';
  @Input() activities: Activity[] = [];
  @Input() disabled = false;
  @Input() readOnly = false;
  @Input() ariaLabel = 'Activity';
  @Input() includeParts = false;
  @Output() valueChange = new EventEmitter<string>();
  @Output() picked = new EventEmitter<ActivitySuggestion>();
  @Output() blurred = new EventEmitter<void>();
  @ViewChild('origin') private origin?: ElementRef<HTMLInputElement>;
  @ViewChild('menu') private menu?: TemplateRef<unknown>;

  suggestions: ActivitySuggestion[] = [];
  activeIndex = 0;
  open = false;

  private api = inject(ProjectApiService);
  private overlay = inject(Overlay);
  private viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;
  private outsideListener: ((event: Event) => void) | null = null;
  private focused = false;
  private latestQuery = '';
  private catalog: PartSuggestion[] = [];
  private readonly queries = new Subject<string>();
  private readonly searchSub: Subscription;

  constructor() {
    this.searchSub = this.queries.pipe(
      debounceTime(150),
      switchMap((query) => {
        const text = query.trim();
        if (!this.includeParts || text.length < 3) {
          return of([] as PartSuggestion[]);
        }
        return this.api.searchParts(text).pipe(catchError(() => of([] as PartSuggestion[])));
      }),
    ).subscribe((matches) => {
      if (!this.includeParts) {
        return;
      }
      this.catalog = this.catalogQuery(this.latestQuery) ? (matches ?? []) : [];
      this.refresh(false);
    });
  }

  ngOnChanges() {
    this.latestQuery = this.value ?? '';
    this.refresh(false);
  }

  ngOnDestroy() {
    this.searchSub.unsubscribe();
    this.close();
  }

  onFocus() {
    if (this.disabled || this.readOnly) {
      return;
    }
    this.focused = true;
    this.latestQuery = this.value ?? '';
    this.refresh(true);
    if (this.includeParts) {
      this.queries.next(this.latestQuery);
    }
  }

  onInput(value: string) {
    if (this.readOnly) {
      return;
    }
    this.focused = true;
    this.latestQuery = value;
    this.valueChange.emit(value);
    if (!this.catalogQuery(value)) {
      this.catalog = [];
    }
    this.refresh(true);
    if (this.includeParts) {
      this.queries.next(value);
    }
  }

  onBlur() {
    this.focused = false;
    this.close();
    this.blurred.emit();
  }

  onKey(event: KeyboardEvent) {
    if (this.readOnly || !this.open) {
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
      return;
    }
    if (event.key === 'Enter' && this.suggestions.length) {
      event.preventDefault();
      event.stopPropagation();
      this.choose(this.suggestions[this.activeIndex] ?? this.suggestions[0]);
    }
  }

  choose(suggestion: ActivitySuggestion) {
    this.picked.emit(suggestion);
    this.close();
  }

  trackSuggestion(suggestion: ActivitySuggestion) {
    return `${suggestion.source ?? 'activity'}:${suggestion.partId ?? suggestion.activityId}:${suggestion.partNo}:${suggestion.description}`;
  }

  groupHeading(index: number): string | null {
    const suggestion = this.suggestions[index];
    if (!suggestion?.group) {
      return null;
    }
    const previous = index > 0 ? this.suggestions[index - 1] : null;
    return !previous || previous.group !== suggestion.group ? suggestion.group : null;
  }

  private catalogQuery(value: string): boolean {
    return this.includeParts && value.trim().length >= 3;
  }

  private refresh(resetIndex: boolean) {
    const query = this.latestQuery;
    const grouped = this.catalogQuery(query);
    const activities = activitySuggestions(query, this.activities).map((suggestion) => ({
      ...suggestion,
      source: 'activity' as const,
      group: grouped ? 'Project activities' : '',
    }));
    const parts = grouped
      ? this.catalog.map((part) => ({
        activityId: 0,
        dsnNo: '',
        partNo: part.partNo,
        label: part.partNo,
        role: 'primary' as const,
        description: part.description ?? '',
        source: 'part' as const,
        partId: part.partId,
        group: 'Parts',
      }))
      : [];
    this.suggestions = [...activities, ...parts];
    if (resetIndex || this.open) {
      this.activeIndex = 0;
    }
    this.show();
  }

  private show() {
    if (!this.focused || this.disabled || this.readOnly || this.suggestions.length === 0) {
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
    const width = Math.max(this.origin.nativeElement.getBoundingClientRect().width, 240);
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
