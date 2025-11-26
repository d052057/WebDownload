import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { MatIconModule, MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
function initializeIcons() {

  const iconRegistry = inject(MatIconRegistry);
  const sanitizer = inject(DomSanitizer);

  iconRegistry.addSvgIcon('kh', sanitizer.bypassSecurityTrustResourceUrl('assets/svg-icons/4x3/kh.svg'));
  iconRegistry.addSvgIcon('us', sanitizer.bypassSecurityTrustResourceUrl('assets/svg-icons/4x3/us.svg'));
  iconRegistry.addSvgIcon('logo', sanitizer.bypassSecurityTrustResourceUrl('assets/svg-icons/logo.svg'),
    { viewBox: '0 0 120 120' }
  );

}
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAppInitializer(initializeIcons)
  ]
};

