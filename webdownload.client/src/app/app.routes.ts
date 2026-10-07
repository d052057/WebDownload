import { Routes } from '@angular/router';
export const routes: Routes = [
  {
    path: '',  // Default route
    loadComponent: () => import('./home/home')
      .then(mod => mod.Home)
  },
  {
    path: 'ytdlp',  // Default route
    loadComponent: () => import('./ytdlp/ytdlp')
      .then(mod => mod.Ytdlp)
  },
  {
    path: 'splitter', loadComponent: () => import('./splitter/splitter')
      .then(mod => mod.Splitter)
  },
  {
    path: 'voice-swap', loadComponent: () => import('./voice-swap/voice-swap')
      .then(mod => mod.VoiceSwap)
  },
  {
    path: 'voiceover', loadComponent: () => import('./voiceover/voiceover')
      .then(mod => mod.Voiceover)
  },
  {
    path: 'translate',
    loadComponent: () => import('./subtitle-dashboard/subtitle-dashboard')
      .then(mod => mod.SubtitleDashboard)
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


