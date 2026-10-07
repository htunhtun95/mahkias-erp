import { Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, TemplateRef, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { Activity } from '../../core/services/project-api.service';
import { activityOptionLabel } from '../quotation-map';

@Component({
  selector: 'app-activity-multi-select',
  standalone: true,
  template: `
    <div class="activity-multi">
      <button #origin class="activity-multi-btn" type="button" [disabled]="disabled" [attr.aria-expanded]="open" (click)="toggle()">
        {{ label() }}
      </button>
      @if (summary) {
        <span class="activity-multi-summary">{{ summary }}</span>
      }
    </div>
    <ng-template #menu>
      <div class="activity-multi-menu" role="listbox" aria-multiselectable="true">
        @for (activity of activities; track activity.id) {
          <label>
            <input type="checkbox" [checked]="isSelected(activity.id)" [disabled]="disabled" (change)="toggleActivity(activity.id, $any($event.target).checked)" />
            <span>{{ optionLabel(activity) }}</span>
          </label>
        }
      </div>
    </ng-template>
  `,
  styles: `
    .activity-multi { position: relative; min-width: 0; }
    .activity-multi-btn {
      width: 100%;
      min-height: 32px;
      padding: 4px 8px;
      border: 1px solid #dadce0;
      border-radius: 8px;
      background: #fff;
      color: #202124;
      font: inherit;
      font-size: 13px;
      text-align: left;
      cursor: pointer;
    }
    .activity-multi-btn:disabled { color: #80868b; cursor: default; }
    .activity-multi-summary {
      display: block;
      margin-top: 4px;
      color: #5f6368;
      font-size: 11px;
      line-height: 1.3;
    }
    .activity-multi-menu {
      max-height: 280px;
      overflow: auto;
      padding: 6px;
      border: 1px solid #dadce0;
      border-radius: 10px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(32, 33, 36, 0.18);
    }
    .activity-multi-menu label {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 6px;
      border-radius: 6px;
      color: #202124;
      font-size: 13px;
      cursor: pointer;
    }
    .activity-multi-menu label:hover { background: #f8f9fa; }
    .activity-multi-menu input { margin-top: 2px; }
  `,
})
export class ActivityMultiSelectComponent implements OnDestroy {
  @Input() activities: Activity[] = [];
  @Input() selectedIds: number[] = [];
  @Input() summary = '';
  @Input() disabled = false;
  @Output() selectedIdsChange = new EventEmitter<number[]>();
  @ViewChild('origin') private origin?: ElementRef<HTMLButtonElement>;
  @ViewChild('menu') private menu?: TemplateRef<unknown>;

  open = false;

  private overlay = inject(Overlay);
  private viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;
  private outsideListener: ((event: Event) => void) | null = null;

  ngOnDestroy() {
    this.close();
  }

  label(): string {
    const selected = this.activities.filter((activity) => this.selectedIds.includes(activity.id));
    if (selected.length === 0) {
      return 'Select activity';
    }
    if (selected.length === 1) {
      return this.optionLabel(selected[0]);
    }
    return `${selected.length} activities`;
  }

  optionLabel(activity: Activity): string {
    return activityOptionLabel(activity, this.activities);
  }

  isSelected(id: number): boolean {
    return this.selectedIds.includes(id);
  }

  toggle() {
    if (this.disabled) {
      return;
    }
    if (this.open) {
      this.close();
      return;
    }
    this.openMenu();
  }

  toggleActivity(id: number, checked: boolean) {
    const next = checked
      ? [...this.selectedIds.filter((item) => item !== id), id]
      : this.selectedIds.filter((item) => item !== id);
    this.selectedIdsChange.emit(next);
  }

  private openMenu() {
    if (!this.origin || !this.menu) {
      return;
    }
    this.close();
    const width = Math.max(this.origin.nativeElement.offsetWidth, 260);
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
    this.open = true;
    this.bindOutside((event) => {
      const target = event.target as Node | null;
      const origin = this.origin?.nativeElement;
      const pane = this.overlayRef?.overlayElement;
      if (target && (origin?.contains(target) || pane?.contains(target))) {
        return;
      }
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
    this.open = false;
  }
}
