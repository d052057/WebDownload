import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterOutlet, RouterLink } from '@angular/router';

@Component({
  selector: 'app-guide',
  imports: [RouterOutlet, RouterLink],
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
    'Repair Boot From USB',
    'FFmpeg',
    'create App'
  ]

  constructor() { }
  //showGuide(item: string) {
  //  this.router.navigate([item],
  //    {
  //      relativeTo: this.activatedRoute
  //    })
  //}
}
