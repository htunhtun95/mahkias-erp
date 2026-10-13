import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { MAT_DIALOG_DEFAULT_OPTIONS } from '@angular/material/dialog';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { googleAuthInitializer } from '../auth/google-auth.init';
import { apiKeyInterceptor } from '../core/services/api-key.interceptor';
import { GoogleAuthService } from '../auth/google-auth.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([apiKeyInterceptor])),
    provideAnimations(),
    { provide: MAT_DIALOG_DEFAULT_OPTIONS, useValue: { disableClose: true } },
    provideAppInitializer(() => {
      const googleAuth = inject(GoogleAuthService);
      return googleAuthInitializer(googleAuth)();
    }),
  ],
};
