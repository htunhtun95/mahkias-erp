import { AfterViewInit, Directive, ElementRef, HostListener, inject } from '@angular/core';

@Directive({
  selector: 'textarea[appAutoGrow]',
  standalone: true,
})
export class AutoGrowDirective implements AfterViewInit {
  private el = inject(ElementRef<HTMLTextAreaElement>);

  ngAfterViewInit() {
    this.fit();
    setTimeout(() => this.fit());
  }

  @HostListener('input')
  fit() {
    const textarea = this.el.nativeElement;
    const min = Number(textarea.dataset.minHeight || 38);
    textarea.classList.remove('is-multiline');
    textarea.style.height = `${min}px`;
    const needsGrow = textarea.value.includes('\n') || textarea.scrollHeight > min + 1;
    if (!needsGrow) {
      return;
    }

    textarea.classList.add('is-multiline');
    textarea.style.height = '0px';
    textarea.style.height = `${Math.max(min, textarea.scrollHeight)}px`;
  }
}
