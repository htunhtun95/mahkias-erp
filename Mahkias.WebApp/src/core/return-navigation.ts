import { Injectable, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ReturnNavigation {
  private previousUrl: string | null = null;
  private currentUrl: string | null = null;

  constructor() {
    const router = inject(Router);
    router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)).subscribe((event) => {
      const next = event.urlAfterRedirects;
      if (this.currentUrl && this.currentUrl !== next && !this.samePath(this.currentUrl, next)) {
        this.previousUrl = this.currentUrl;
      }
      this.currentUrl = next;
    });
  }

  previous(): string | null {
    const previous = this.previousUrl;
    if (!previous) {
      return null;
    }
    const path = previous.split(/[?#]/)[0];
    if (!path || path === '/' || path === '/login') {
      return null;
    }
    return previous;
  }

  private samePath(left: string, right: string): boolean {
    return left.split(/[?#]/)[0] === right.split(/[?#]/)[0];
  }
}
