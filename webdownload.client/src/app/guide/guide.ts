import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-guide',
  imports: [RouterOutlet],
  templateUrl: './guide.html',
  styleUrl: './guide.scss',
})
export class Guide {
  activatedRoute = inject(ActivatedRoute);
  router = inject(Router)
  items = [
    'Backup IIS',
    'FFmpeg',
    'Git Help',
    'Apple Dmg',
    'Repair Boot',
    'Recovery',
    'Robo Copy',
    'Yt-dlp Websites',
    'Windows11',
    'Repair Boot From USB'
  ]

  constructor() { }
  showGuide(item: string) {
    this.router.navigate(['./guide', item],
      {
        relativeTo: this.activatedRoute
      })
  }
}
