import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../environments/environment';

declare const google: any;

@Injectable({ providedIn: 'root' })
export class GoogleAuthService {
  private clientId = environment.googleClientId;
  private _user$ = new BehaviorSubject<any | null>(this.getStoredUser());
  user$ = this._user$.asObservable();
  private initialized = false;
  private promptFailed = false;

  get user() {
    return this._user$.value;
  }

  get autoLoginFailed() {
    return this.promptFailed;
  }

  initialize(callback: () => void) {
    if (this.user) {
      callback();
      return;
    }

    this.whenGoogleReady().then(() => {
      if (this.initialized || typeof google === 'undefined' || !google.accounts?.id) {
        callback();
        return;
      }

      this.initialized = true;
      this.configureGoogle();
      callback();
    });
  }

  private configureGoogle() {
    google.accounts.id.initialize({
      client_id: this.clientId,
      use_fedcm_for_prompt: true,
      use_fedcm_for_button: true,
      callback: (response: any) => {
        const user = this.decodeJwt(response.credential);
        this.storeUser(user, response.credential);
        this._user$.next(user);
      },
      auto_select: true,
    });

  }

  private whenGoogleReady(): Promise<void> {
    return new Promise((resolve) => {
      const ready = () => typeof google !== 'undefined' && !!google.accounts?.id;
      if (ready()) {
        resolve();
        return;
      }

      const started = Date.now();
      const timer = window.setInterval(() => {
        if (ready() || Date.now() - started > 4000) {
          window.clearInterval(timer);
          resolve();
        }
      }, 50);
    });
  }

  renderButton(containerId: string) {
    if (typeof google === 'undefined' || !google.accounts?.id) {
      return;
    }

    if (!this.initialized) {
      this.configureGoogle();
      this.initialized = true;
    }

    google.accounts.id.prompt((notification: any) => {
      if (notification.isNotDisplayed?.() || notification.isSkippedMoment?.()) {
        this.promptFailed = true;
      }
    });

    google.accounts.id.renderButton(document.getElementById(containerId), {
      theme: 'outline',
      size: 'large',
      width: 320,
    });
  }

  logout() {
    sessionStorage.removeItem('auth_user');
    this._user$.next(null);
    google.accounts.id.disableAutoSelect();
  }

  private decodeJwt(token: string): any {
    const [, payload] = token.split('.');
    return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
  }

  private storeUser(user: any, idToken: string) {
    sessionStorage.setItem(
      'auth_user',
      JSON.stringify({
        user,
        idToken,
      }),
    );
  }

  private getStoredUser(): any {
    const data = sessionStorage.getItem('auth_user');
    if (!data) {
      return null;
    }

    try {
      return JSON.parse(data).user;
    } catch {
      return null;
    }
  }
}
