import { HttpClient } from "@angular/common/http";
import { Component, OnInit, inject, Renderer2 } from "@angular/core";
import { SafeHtml, DomSanitizer } from "@angular/platform-browser";
import { ActivatedRoute } from "@angular/router";
import { DOCUMENT } from '@angular/common';

@Component({
  selector: 'app-display-guide',
  imports: [],
  templateUrl: './display-guide.html',
  styleUrl: './display-guide.scss',
})
export class DisplayGuide implements OnInit {
  htmlContent: SafeHtml = '';
  http = inject(HttpClient);
  activatedRoute = inject(ActivatedRoute);
  sanitizer = inject(DomSanitizer);
  renderer2 = inject(Renderer2);
  document = inject(DOCUMENT);
  folder: string = '';
  item: string = '';
  css = [
    'Backup IIS.css',
    'FFmpeg.css',
    'Git Help.css',
    'Apple Dmg.css',
    'Repair Boot.css',
    'Recovery.css',
    'Robo Copy.css',
    'Yt-dlp Websites.css',
    'Windows11.css',
    'Repair Boot From USB.css'
  ]
  js = [
  ]
  constructor() { }
  ngOnInit() {
    this.activatedRoute.paramMap
      .subscribe(
        (params: any) => {

          this.folder = '';
          this.item = params.get('item');

          this.folder = 'assets/guide/' + this.item + '/index.html';

          this.http.get(this.folder, { responseType: 'text' })
            .subscribe(response => {
              this.htmlContent = this.sanitizer.bypassSecurityTrustHtml(response);
              this.loadStylesheet(this.item);
              this.loadJs(this.item);
            });
        }
      );
  }
  loadStylesheet(feature: string) {
    const url = 'assets/guide/' + feature + '/' + feature + '.css'
    const cssFile = feature + '.css';
    for (const c of this.css) {
      if (c === cssFile) {
        const link = this.renderer2.createElement('link');
        this.renderer2.setAttribute(link, 'rel', 'stylesheet');
        this.renderer2.setAttribute(link, 'href', url);
        this.renderer2.appendChild(this.document.head, link);
      }
    }
  }
  loadJs(feature: string) {
    let jsFile = feature + '.js';
    const scriptSrc = 'assets/guide/' + feature + '/' + jsFile;
    for (const c of this.js) {
      if (c === jsFile) {
        const script = this.document.createElement('script');
        script.type = 'text/javascript';
        script.src = scriptSrc;
        script.async = true;
        this.document.head.appendChild(script);
      }
    }
  }
}
