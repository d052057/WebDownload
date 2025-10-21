import { Routes } from '@angular/router';
export const routes: Routes = [
  {
    path: '',  // Default route
    loadComponent: () => import('./home/home')
      .then(mod => mod.Home)
  },
  {
    path: 'githelp',
    loadComponent: () => import('./git-help/git-help')
      .then(mod => mod.GitHelp)
  }
];


