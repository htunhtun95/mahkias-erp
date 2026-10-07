import { GoogleAuthService } from './google-auth.service';

export function googleAuthInitializer(googleAuth: GoogleAuthService): () => Promise<void> {
  return () =>
    new Promise((resolve) => {
      googleAuth.initialize(() => resolve());
    });
}
