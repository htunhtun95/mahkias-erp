import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, TemplateRef, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-lookup-combobox',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="lookup-combobox" [class.open]="open">
      <input
        #origin
        [value]="text"
        [disabled]="disabled"
        [attr.aria-label]="ariaLabel"
        role="combobox"
        aria-autocomplete="list"
        autocomplete="off"
        [attr.aria-expanded]="open"
        [attr.aria-controls]="menuId"
        [attr.aria-activedescendant]="activeId"
        (input)="onInput($any($event.target).value)"
        (focus)="onFocus()"
        (blur)="onBlur()"
        (keydown)="onKey($event)"
      />
      <button class="lookup-chevron" type="button" tabindex="-1" [disabled]="disabled" [attr.aria-label]="'Show ' + ariaLabel + ' options'" (mousedown)="onChevron($event)">
        <mat-icon>expand_more</mat-icon>
      </button>
    </div>
    <ng-template #menu>
      <div class="lookup-menu" role="listbox" [id]="menuId">
        @for (option of matches; track option; let index = $index) {
          <button
            type="button"
            role="option"
            [id]="optionId(index)"
            [class.active]="index === activeIndex"
            [attr.aria-selected]="isCurrent(option)"
            (mouseenter)="activeIndex = index"
            (mousedown)="$event.preventDefault()"
            (click)="choose(option)"
          >
            <span>{{ option }}</span>
            @if (isCurrent(option)) {
              <mat-icon>check</mat-icon>
            }
          </button>
        } @empty {
          <p class="lookup-empty">No matches</p>
        }
      </div>
    </ng-template>
  `,
  styles: `
    :host { display: block; min-width: 0; height: 100%; min-height: 32px; }
    .lookup-combobox { position: relative; height: 100%; min-height: 32px; }
    input {
      display: block;
      box-sizing: border-box;
      width: 100%;
      height: 100%;
      min-height: 32px;
      margin: 0;
      padding: 0 28px 0 8px;
      border: 0;
      border-radius: 0;
      outline: none;
      background: transparent;
      color: #202124;
      font: inherit;
      font-size: 13px;
    }
    .lookup-chevron {
      position: absolute;
      top: 0;
      right: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 100%;
      min-height: 32px;
      padding: 0;
      border: 0;
      background: transparent;
      color: #5f6368;
      cursor: pointer;
    }
    .lookup-chevron mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
      transition: transform 0.15s ease;
    }
    .lookup-combobox.open .lookup-chevron { color: #1a73e8; }
    .lookup-combobox.open .lookup-chevron mat-icon { transform: rotate(180deg); }
    .lookup-chevron:disabled { opacity: 0.6; cursor: default; }
    .lookup-menu {
      box-sizing: border-box;
      max-height: 320px;
      overflow: auto;
      padding: 6px;
      border: 1px solid #dadce0;
      border-radius: 8px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(32, 33, 36, 0.18);
    }
    .lookup-menu button {
      display: flex;
      align-items: center;
      justify-content: space-between;
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
      line-height: 20px;
      text-align: left;
      cursor: pointer;
    }
    .lookup-menu button span {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .lookup-menu button mat-icon {
      flex: 0 0 auto;
      width: 16px;
      height: 16px;
      font-size: 16px;
      color: #1a73e8;
    }
    .lookup-menu button.active,
    .lookup-menu button:hover { background: #e8f0fe; }
    .lookup-empty {
      margin: 0;
      padding: 8px 12px;
      color: #5f6368;
      font-size: 13px;
      line-height: 20px;
    }
  `,
})
export class LookupComboboxComponent implements OnChanges, OnDestroy {
  @Input() value = '';
  @Input() options: string[] = [];
  @Input() disabled = false;
  @Input() ariaLabel = 'Lookup';
  @Output() valueChange = new EventEmitter<string>();
  @Output() committed = new EventEmitter<void>();
  @ViewChild('origin') private origin?: ElementRef<HTMLInputElement>;
  @ViewChild('menu') private menu?: TemplateRef<unknown>;

  text = '';
  matches: string[] = [];
  activeIndex = 0;
  open = false;
  readonly menuId = `lookup-${Math.random().toString(36).slice(2, 9)}`;

  private overlay = inject(Overlay);
  private viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;
  private outsideListener: ((event: Event) => void) | null = null;
  private scrollListener: ((event: Event) => void) | null = null;
  private scrollTargets: HTMLElement[] = [];
  private focused = false;
  private filtering = false;

  ngOnChanges() {
    if (!this.focused) {
      this.text = this.value ?? '';
    }
    if (this.open) {
      this.syncMatches();
    }
  }

  ngOnDestroy() {
    this.close();
  }

  get activeId(): string | null {
    return this.open && this.matches.length ? this.optionId(this.activeIndex) : null;
  }

  optionId(index: number): string {
    return `${this.menuId}-option-${index}`;
  }

  isCurrent(option: string): boolean {
    return this.same(option, this.text);
  }

  onFocus() {
    if (this.disabled) {
      return;
    }
    this.focused = true;
    this.filtering = false;
    this.origin?.nativeElement.select();
    this.show();
  }

  onInput(value: string) {
    this.focused = true;
    this.filtering = true;
    this.text = value;
    this.valueChange.emit(value);
    this.show();
  }

  onBlur() {
    this.focused = false;
    this.filtering = false;
    this.close();
    this.committed.emit();
  }

  onChevron(event: MouseEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (this.disabled) {
      return;
    }
    if (this.open) {
      this.close();
      return;
    }
    const input = this.origin?.nativeElement;
    this.filtering = false;
    if (input && document.activeElement !== input) {
      input.focus();
      return;
    }
    this.focused = true;
    this.show();
  }

  onKey(event: KeyboardEvent) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (!this.open) {
        this.filtering = false;
        this.show();
        return;
      }
      this.move(1);
      return;
    }
    if (event.key === 'ArrowUp' && this.open) {
      event.preventDefault();
      this.move(-1);
      return;
    }
    if (event.key === 'Escape' && this.open) {
      event.preventDefault();
      event.stopPropagation();
      this.close();
      return;
    }
    if (event.key === 'Enter' && this.open && this.matches.length) {
      event.preventDefault();
      event.stopPropagation();
      this.choose(this.matches[this.activeIndex] ?? this.matches[0]);
    }
  }

  choose(option: string) {
    this.text = option;
    this.filtering = false;
    this.valueChange.emit(option);
    this.committed.emit();
    this.close();
  }

  private show() {
    this.syncMatches();
    if (!this.focused || this.disabled) {
      this.close();
      return;
    }
    if (!this.overlayRef) {
      this.openPanel();
    }
    this.open = true;
    this.scrollActiveIntoView();
  }

  private syncMatches() {
    const query = this.filtering ? this.text.trim() : '';
    this.matches = query
      ? this.options.filter((option) => option.toLocaleLowerCase().includes(query.toLocaleLowerCase()))
      : [...this.options];
    const current = this.matches.findIndex((option) => this.same(option, this.text));
    this.activeIndex = current >= 0 ? current : (this.matches.length ? 0 : -1);
  }

  private move(step: number) {
    if (!this.matches.length) {
      return;
    }
    const next = this.activeIndex < 0 ? 0 : (this.activeIndex + step + this.matches.length) % this.matches.length;
    this.activeIndex = next;
    this.scrollActiveIntoView();
  }

  private scrollActiveIntoView() {
    const id = this.activeId;
    if (!id) {
      return;
    }
    requestAnimationFrame(() => {
      this.overlayRef?.overlayElement.querySelector(`#${id}`)?.scrollIntoView({ block: 'nearest' });
    });
  }

  private openPanel() {
    if (!this.origin || !this.menu || this.overlayRef) {
      return;
    }
    const width = Math.max(this.origin.nativeElement.getBoundingClientRect().width, 180);
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
    const onScroll = (event: Event) => {
      const target = event.target;
      const pane = this.overlayRef?.overlayElement;
      if (target instanceof Node && pane?.contains(target)) {
        return;
      }
      if (!this.isOriginVisible()) {
        this.focused = false;
        this.close();
        return;
      }
      this.overlayRef?.updatePosition();
    };
    this.scrollListener = onScroll;
    document.addEventListener('scroll', onScroll, true);
    let parent = this.origin?.nativeElement.parentElement ?? null;
    while (parent) {
      const style = getComputedStyle(parent);
      const scrolls = /(auto|scroll)/.test(style.overflowY) || /(auto|scroll)/.test(style.overflowX);
      if (scrolls) {
        parent.addEventListener('scroll', onScroll, { passive: true });
        this.scrollTargets.push(parent);
      }
      parent = parent.parentElement;
    }
    const onPointer = (event: Event) => {
      const target = event.target as Node | null;
      const host = this.origin?.nativeElement.parentElement;
      const pane = this.overlayRef?.overlayElement;
      if (target && (host?.contains(target) || pane?.contains(target))) {
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
    if (this.scrollListener) {
      document.removeEventListener('scroll', this.scrollListener, true);
      for (const target of this.scrollTargets) {
        target.removeEventListener('scroll', this.scrollListener);
      }
      this.scrollTargets = [];
      this.scrollListener = null;
    }
    this.overlayRef?.dispose();
    this.overlayRef = null;
    this.open = false;
  }

  private isOriginVisible(): boolean {
    const origin = this.origin?.nativeElement;
    if (!origin) {
      return false;
    }
    const rect = origin.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) {
      return false;
    }
    let parent = origin.parentElement;
    while (parent) {
      const style = getComputedStyle(parent);
      const clipsY = style.overflowY === 'auto' || style.overflowY === 'scroll' || style.overflowY === 'hidden';
      const clipsX = style.overflowX === 'auto' || style.overflowX === 'scroll' || style.overflowX === 'hidden';
      if ((clipsY && parent.scrollHeight > parent.clientHeight + 1) || (clipsX && parent.scrollWidth > parent.clientWidth + 1)) {
        const bounds = parent.getBoundingClientRect();
        const header = origin.closest('table')?.querySelector('thead');
        const headerBottom = header && parent.contains(header) ? header.getBoundingClientRect().bottom : bounds.top;
        const top = Math.max(bounds.top, headerBottom);
        if (rect.bottom <= top + 1 || rect.top >= bounds.bottom - 1 || rect.right <= bounds.left + 1 || rect.left >= bounds.right - 1) {
          return false;
        }
      }
      parent = parent.parentElement;
    }
    return true;
  }

  private same(left: string, right: string): boolean {
    return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
  }
}
