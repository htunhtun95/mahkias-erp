import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { GoogleAuthService } from '../../auth/google-auth.service';
import { environment } from '../../environments/environment';

export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!environment.apiKey || !req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const user = inject(GoogleAuthService).user;
  const headers: Record<string, string> = {
    'X-Api-Key': environment.apiKey,
  };
  const name = typeof user?.name === 'string' ? user.name.trim() : '';
  const email = typeof user?.email === 'string' ? user.email.trim() : '';
  if (name) {
    headers['X-User-Name'] = encodeURIComponent(name);
  }
  if (email) {
    headers['X-User-Email'] = encodeURIComponent(email);
  }

  return next(req.clone({ setHeaders: headers }));
};
