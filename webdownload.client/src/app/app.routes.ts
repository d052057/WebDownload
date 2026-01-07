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
    children: [{
      path: 'guide/:item',
      loadComponent: () => import('./guide/display-guide/display-guide')
        .then(mod => mod.DisplayGuide)
    }
    ]
  },
];


