import { Routes } from '@angular/router';
export const routes: Routes = [
  {
    path: '',  // Default route
    loadComponent: () => import('./home/home')
      .then(mod => mod.Home)
  },
  {
    path: 'windows11',  // Default route
    loadComponent: () => import('./windows11/windows11')
      .then(mod => mod.Windows11)
  },
  {
    path: 'guide',
    loadComponent: () => import('./guide/guide')
      .then(mod => mod.Guide),
    children: [{
      path: 'guide/:item',
      loadComponent: () => import('./guide/display-guide/display-guide')
        .then(mod => mod.DisplayGuide)
    }
    ]
  },
];


