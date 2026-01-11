import { Routes } from '@angular/router';
export const routes: Routes = [
  {
    path: '',  // Default route
    loadComponent: () => import('./home/home')
      .then(mod => mod.Home)
  },
  {
    path: 'guide',
    loadComponent: () => import('./guide/guide')
      .then(mod => mod.Guide),
    children: [
      {
        path: 'windows11',  // Default route
        loadComponent: () => import('./guide/windows11/windows11')
          .then(mod => mod.Windows11)
      },
      {
      path: ':item',
      loadComponent: () => import('./guide/display-guide/display-guide')
        .then(mod => mod.DisplayGuide)
      }
    ]
  }
];


