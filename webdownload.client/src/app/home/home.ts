import { Component, inject, ElementRef, AfterViewInit, OnInit } from '@angular/core';
import { DOCUMENT } from '@angular/common';
@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements OnInit, AfterViewInit {
  private document = inject(DOCUMENT);
  private el = inject(ElementRef);

  ngOnInit(): void {
    const fallback = `${this.document.baseURI}assets/images/400110244.jpg`;
    this.el.nativeElement.style.setProperty('--angkor-bg-image', `url("${fallback}")`);
  }

  ngAfterViewInit(): void {
    const imageUrl = `${this.document.baseURI}assets/images/400110244.jpg`;
    this.el.nativeElement.style.setProperty('--angkor-bg-image', `url("${imageUrl}")`);
  }
}
