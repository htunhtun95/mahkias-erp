import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';

bootstrapApplication(App, appConfig)
  .then(() => {
    const splash = document.getElementById('splash-screen');
    if (splash) {
      splash.style.display = 'none';
    }
  })
  .catch((err) => console.error(err));
