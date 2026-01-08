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
    'Git Help',
    'Apple Dmg',
    'Repair Boot',
    'Recovery',
    'Robo Copy',
    'Yt-dlp Websites',
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
