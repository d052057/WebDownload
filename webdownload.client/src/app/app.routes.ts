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
    path: 'robocopy',
    loadComponent: () => import('./robo-copy/robo-copy')
      .then(mod => mod.RoboCopy)
  },
  {
    path: 'dmg',
    loadComponent: () => import('./dmg/dmg')
      .then(mod => mod.Dmg)
  },
  {
    path: 'githelp',
    loadComponent: () => import('./git-help/git-help')
      .then(mod => mod.GitHelp)
  }
];


