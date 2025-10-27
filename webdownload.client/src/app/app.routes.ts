import { Routes } from '@angular/router';
export const routes: Routes = [
  {
    path: '',  // Default route
    loadComponent: () => import('./home/home')
      .then(mod => mod.Home)
  },
  {
    path: 'ffmpeg',
    loadComponent: () => import('./ffmpeg/ffmpeg')
      .then(mod => mod.Ffmpeg)
  },
  {
    path: 'githelp',
    loadComponent: () => import('./git-help/git-help')
      .then(mod => mod.GitHelp)
  }
];


