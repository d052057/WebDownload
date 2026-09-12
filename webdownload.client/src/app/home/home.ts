import { Component, inject, ElementRef, AfterViewInit } from '@angular/core';
import { DOCUMENT } from '@angular/common';
@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements AfterViewInit {
  private document = inject(DOCUMENT);
  private el = inject(ElementRef);

  ngAfterViewInit(): void {
    const imageUrl = `${this.document.baseURI}assets/images/400110244.jfif`;
    this.el.nativeElement.style.setProperty('--angkor-bg-image', `url("${imageUrl}")`);
  }
}
