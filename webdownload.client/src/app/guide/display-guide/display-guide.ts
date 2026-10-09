import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, Injector, afterNextRender, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { catchError, forkJoin, map, Observable, of, switchMap, tap } from 'rxjs';

/**
 * Shows one guide from  assets/guide/<name>/ :
 *
 *   index.html    required  the page (inserted as HTML; <script> tags inside it never run)
 *   <name>.css    optional  loaded for this guide only and removed when you leave it
 *   <name>.js     optional  run once after the page is shown and removed when you leave it
 *
 * Nothing needs to be registered here: optional files are simply used when they exist.
 * A guide's CSS must stay inside the guide: wrap the page in its own class and prefix every
 * selector with it, never style body, html, p, h1 ... directly.
 *
 * Copy and download buttons need no JavaScript. Put   data-copy="ID"   or
 * data-download="ID" data-filename="name.bat"   on a button, where ID is the id of the
 * element (normally a <pre>) holding the text.
 *
 * A guide's <name>.js runs inside its own function, so its variables are private. To use a
 * function from an inline onclick="..." attribute, assign it to window (window.myFn = ...).
 */
@Component({
  selector: 'app-display-guide',
  imports: [],
  templateUrl: './display-guide.html',
  styleUrl: './display-guide.scss',
})
export class DisplayGuide {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);
  private document = inject(DOCUMENT);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private injector = inject(Injector);

  htmlContent = signal<SafeHtml | null>(null);
  error = signal('');

  // The <style> and <script> elements this component added to <head> for the current guide.
  private injected: HTMLElement[] = [];

  // Changes every time a guide is left, so work that was queued for the old guide can tell it is stale.
  private generation = 0;

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('item') ?? ''),
        tap(() => this.clear()),
        // switchMap drops the answer of a guide you already left, so a slow reply
        // can never replace the guide you are looking at now.
        switchMap((item) => this.load(item)),
        takeUntilDestroyed()
      )
      .subscribe();

    inject(DestroyRef).onDestroy(() => this.removeInjected());
  }

  // ---- loading ------------------------------------------------------------------------------

  private load(item: string): Observable<unknown> {
    const name = encodeURIComponent(item);
    const base = `assets/guide/${name}/`;

    return forkJoin({
      html: this.fetchText(base + 'index.html').pipe(map((t) => (t !== null && !isAppShell(t) ? t : null))),
      css: this.fetchText(base + name + '.css').pipe(map((t) => (t !== null && !isHtml(t) ? t : null))),
      js: this.fetchText(base + name + '.js').pipe(map((t) => (t !== null && !isHtml(t) ? t : null))),
    }).pipe(tap((files) => this.show(item, files)));
  }

  // null when the file does not exist.
  private fetchText(url: string): Observable<string | null> {
    return this.http.get(url, { responseType: 'text' }).pipe(catchError(() => of(null)));
  }

  private show(item: string, files: { html: string | null; css: string | null; js: string | null }): void {
    if (files.html === null) {
      this.error.set(`The guide "${item}" was not found.`);
      return;
    }

    // Style first, so the page never shows unstyled.
    if (files.css) this.addToHead('style', files.css, item);
    this.htmlContent.set(this.sanitizer.bypassSecurityTrustHtml(files.html));

    // The script needs the page's elements, which exist only after the next render. If you have
    // already left this guide by then, the script must not be added.
    const js = files.js;
    const generation = this.generation;
    if (js) {
      afterNextRender(
        () => {
          if (generation === this.generation) this.addToHead('script', wrapScript(js, item), item);
        },
        { injector: this.injector }
      );
    }
  }

  private addToHead(tag: 'style' | 'script', text: string, item: string): void {
    const el = this.document.createElement(tag);
    el.setAttribute('data-guide', item);
    el.textContent = text;
    this.document.head.appendChild(el);
    this.injected.push(el);
  }

  private clear(): void {
    this.generation++;
    this.removeInjected();
    this.htmlContent.set(null);
    this.error.set('');
  }

  private removeInjected(): void {
    for (const el of this.injected) el.remove();
    this.injected = [];
  }

  // ---- copy and download buttons ------------------------------------------------------------

  onGuideClick(event: Event): void {
    const button = (event.target as Element | null)?.closest<HTMLElement>('[data-copy],[data-download]');
    if (!button || !this.host.nativeElement.contains(button)) return;

    const copying = button.dataset['copy'] !== undefined;
    const id = (copying ? button.dataset['copy'] : button.dataset['download']) ?? '';
    const source = this.host.nativeElement.querySelector<HTMLElement>(`[id="${id.replace(/"/g, '\\"')}"]`);
    if (!source) return;

    event.preventDefault();
    const text = dedent(source.textContent ?? '');
    if (copying) void this.copy(text, button);
    else this.download(text, button.dataset['filename'] || 'script.txt');
  }

  private async copy(text: string, button: HTMLElement): Promise<void> {
    let ok = false;

    // navigator.clipboard exists only on https and localhost. This app is also opened over
    // plain http on the network, so there must be a fallback.
    const nav = this.document.defaultView?.navigator;
    if (nav?.clipboard && this.document.defaultView?.isSecureContext) {
      try {
        await nav.clipboard.writeText(text);
        ok = true;
      } catch {
        ok = false;
      }
    }
    if (!ok) ok = this.legacyCopy(text);

    flash(button, ok ? 'Copied!' : 'Copy failed. Select the text and press Ctrl+C');
  }

  private legacyCopy(text: string): boolean {
    const box = this.document.createElement('textarea');
    box.value = text;
    box.setAttribute('readonly', '');
    box.style.position = 'fixed';
    box.style.opacity = '0';
    this.document.body.appendChild(box);
    box.select();
    let ok = false;
    try {
      ok = this.document.execCommand('copy');
    } catch {
      ok = false;
    }
    box.remove();
    return ok;
  }

  private download(text: string, filename: string): void {
    // Batch files need Windows line endings (CRLF); other text just gets a final newline.
    const body = /\.(bat|cmd)$/i.test(filename) ? text.replace(/\r?\n/g, '\r\n') + '\r\n' : text + '\n';
    const url = URL.createObjectURL(new Blob([body], { type: 'text/plain;charset=utf-8' }));

    const link = this.document.createElement('a');
    link.href = url;
    link.download = filename;
    this.document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}

// ---- helpers (exported so the tests can use them) -------------------------------------------

// A missing file is often answered with the app's own page (HTTP 200) instead of a 404.
export const isHtml = (text: string): boolean => text.trimStart().startsWith('<');
export const isAppShell = (text: string): boolean => /<app-root[\s>]/i.test(text);

// Runs the guide's script in its own function so that a second visit does not hit
// "Identifier has already been declared" for top-level const/let.
export const wrapScript = (js: string, item: string): string =>
  `(function () {\n${js}\n})();\n//# sourceURL=guide-${item.replace(/\W+/g, '-')}.js`;

// Removes the indentation shared by every line and the empty lines around the text.
export function dedent(text: string): string {
  const lines = text.replace(/\r\n/g, '\n').replace(/^\s*\n/, '').replace(/\s+$/, '').split('\n');
  const indents = lines.filter((l) => l.trim() !== '').map((l) => l.length - l.trimStart().length);
  const common = indents.length ? Math.min(...indents) : 0;
  return lines.map((l) => l.slice(Math.min(common, l.length - l.trimStart().length))).join('\n');
}

function flash(button: HTMLElement, message: string): void {
  if (button.dataset['busy']) return;
  const original = button.textContent ?? '';
  button.dataset['busy'] = '1';
  button.textContent = message;
  button.classList.add('copied');
  setTimeout(() => {
    button.textContent = original;
    button.classList.remove('copied');
    delete button.dataset['busy'];
  }, 1800);
}
