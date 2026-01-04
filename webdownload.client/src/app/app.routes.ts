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
    path: 'recovery',
    loadComponent: () => import('./recovery/recovery')
      .then(mod => mod.Recovery)
  },
  {
    path: 'repairboot',
    loadComponent: () => import('./repair-boot/repair-boot')
      .then(mod => mod.RepairBoot)
  },
  {
    path: 'repairusbboot',
    loadComponent: () => import('./repair-usb-boot/repair-usb-boot')
      .then(mod => mod.RepairUsbBoot)
  },
  {
    path: 'ypdlp',
    loadComponent: () => import('./ypdlp/ypdlp')
      .then(mod => mod.Ypdlp)
  },
  {
    path: 'windows',
    loadComponent: () => import('./windows11/windows11')
      .then(mod => mod.Windows11)
  },
  {
    path: 'githelp',
    loadComponent: () => import('./git-help/git-help')
      .then(mod => mod.GitHelp)
  }
];


