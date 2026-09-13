import { Component, inject, ElementRef, AfterViewInit } from '@angular/core';
import { Urlbase } from '../services/urlbase';

@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements AfterViewInit {
  private urlbase = inject(Urlbase);
  private apiBase!: string;
  private el = inject(ElementRef);

  constructor() {
    const segment = this.urlbase.baseUrl();
    this.apiBase = segment ? `/${segment}` : '';
  }

  ngAfterViewInit(): void {
    const imageUrl = `${this.apiBase}/assets/images/400110244.jfif`;

    this.el.nativeElement.style.setProperty(
      '--angkor-bg-image',
      `url("${imageUrl}")`
    );
  }
}
