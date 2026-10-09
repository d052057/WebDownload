import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { DisplayGuide, dedent, isAppShell, isHtml, wrapScript } from './display-guide';

const APP_SHELL = '<!doctype html><html><body><app-root></app-root></body></html>';

describe('DisplayGuide', () => {
  let fixture: ComponentFixture<DisplayGuide>;
  let http: HttpTestingController;
  let route: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  const host = () => fixture.nativeElement as HTMLElement;
  const guideStyles = () => document.head.querySelectorAll('style[data-guide]');
  const guideScripts = () => document.head.querySelectorAll('script[data-guide]');

  // Answers the three requests a guide makes. A value of null means "file does not exist" (404).
  function respond(item: string, files: { html: string | null; css?: string | null; js?: string | null }, tick = true) {
    const name = encodeURIComponent(item);
    const answer = (url: string, body: string | null | undefined) => {
      const req = http.expectOne(url);
      if (body === null || body === undefined) req.flush('Not found', { status: 404, statusText: 'Not Found' });
      else req.flush(body);
    };
    answer(`assets/guide/${name}/index.html`, files.html);
    answer(`assets/guide/${name}/${name}.css`, files.css);
    answer(`assets/guide/${name}/${name}.js`, files.js);
    fixture.detectChanges();
    if (tick) TestBed.inject(ApplicationRef).tick();   // runs afterNextRender callbacks, as the real app does
  }

  beforeEach(() => {
    route = new BehaviorSubject(convertToParamMap({ item: 'Demo' }));
    TestBed.configureTestingModule({
      imports: [DisplayGuide],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { paramMap: route.asObservable() } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DisplayGuide);
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    document.head.querySelectorAll('[data-guide]').forEach((e) => e.remove());
    document.body.querySelectorAll('textarea,a[download]').forEach((e) => e.remove());
    vi.restoreAllMocks();
  });

  // ---- loading ------------------------------------------------------------------------------

  it('shows the page and loads its stylesheet only for this guide', () => {
    respond('Demo', { html: '<h1 id="t">Hello</h1>', css: '.demo{color:red}' });
    expect(host().querySelector('#t')?.textContent).toBe('Hello');
    expect(guideStyles().length).toBe(1);
    expect(guideStyles()[0].textContent).toContain('.demo');
  });

  it('removes the old stylesheet and script when you move to another guide', () => {
    respond('Demo', { html: '<p>one</p>', css: '.one{}', js: 'window.x = 1;' });
    expect(guideStyles().length).toBe(1);
    expect(guideScripts().length).toBe(1);

    route.next(convertToParamMap({ item: 'Other' }));
    // The old guide is gone straight away, before the new files even arrive.
    expect(guideStyles().length).toBe(0);
    expect(guideScripts().length).toBe(0);

    respond('Other', { html: '<p>two</p>', css: '.two{}' });
    expect(guideStyles().length).toBe(1);
    expect(guideStyles()[0].textContent).toContain('.two');
    expect(host().textContent).toContain('two');
    expect(host().textContent).not.toContain('one');
  });

  it('removes its stylesheet and script when the component is destroyed', () => {
    respond('Demo', { html: '<p>x</p>', css: '.a{}', js: 'void 0;' });
    fixture.destroy();
    expect(guideStyles().length).toBe(0);
    expect(guideScripts().length).toBe(0);
  });

  it('runs the guide script once, in its own function, after the page exists', () => {
    respond('Demo', { html: '<div id="here"></div>', js: 'const a = 1;' });
    const scripts = guideScripts();
    expect(scripts.length).toBe(1);
    expect(scripts[0].textContent).toContain('(function () {');
    expect(scripts[0].textContent).toContain('const a = 1;');
    expect(host().querySelector('#here')).not.toBeNull();   // the page was there when the script was added
  });

  it('survives a guide that has no .css and no .js (they answer 404)', () => {
    respond('Demo', { html: '<p>plain</p>', css: null, js: null });
    expect(host().textContent).toContain('plain');
    expect(guideStyles().length).toBe(0);
    expect(guideScripts().length).toBe(0);
    expect(host().querySelector('.alert')).toBeNull();
  });

  it('ignores a "missing" css/js that the server answers with the app page (status 200)', () => {
    respond('Demo', { html: '<p>plain</p>', css: APP_SHELL, js: APP_SHELL });
    expect(guideStyles().length).toBe(0);
    expect(guideScripts().length).toBe(0);
    expect(host().textContent).toContain('plain');
  });

  it('shows a message when the guide itself does not exist (404)', () => {
    respond('Demo', { html: null });
    expect(host().querySelector('.alert')?.textContent).toContain('"Demo" was not found');
  });

  it('shows the same message when the server answers with the app page instead of the guide', () => {
    respond('Demo', { html: APP_SHELL });
    expect(host().querySelector('.alert')?.textContent).toContain('was not found');
    expect(host().querySelector('app-root')).toBeNull();
  });

  it('encodes names with spaces, & and parentheses', () => {
    route.next(convertToParamMap({ item: 'Applio Inference & Volume (x)' }));
    respond('Applio Inference & Volume (x)', { html: '<p>ok</p>' });
    expect(host().textContent).toContain('ok');
  });

  it('cancels the requests of a guide you already left, so a slow answer can never replace the current one', () => {
    // Open "Demo" but do not answer yet, then move to "Other".
    const slow = ['index.html', 'Demo.css', 'Demo.js'].map((f) => http.expectOne(`assets/guide/Demo/${f}`));
    route.next(convertToParamMap({ item: 'Other' }));

    expect(slow.every((r) => r.cancelled)).toBe(true);
    respond('Other', { html: '<p>other</p>' });
    expect(host().textContent).toContain('other');
    expect(host().textContent).not.toContain('demo');
  });

  it('does not add the script of a guide you left before the page was rendered', () => {
    respond('Demo', { html: '<p>x</p>', js: 'window.x = 1;' }, false);   // answered, but not rendered yet
    route.next(convertToParamMap({ item: 'Other' }));                    // ...and then you leave
    TestBed.inject(ApplicationRef).tick();                               // the queued render now happens
    expect(guideScripts().length).toBe(0);
  });

  // ---- copy and download --------------------------------------------------------------------

  const page = `
    <button id="b" data-copy="code1">Copy</button>
    <button id="d" data-download="code1" data-filename="run.bat">Download</button>
    <pre id="code1">
        @echo off
          echo hi
    </pre>`;

  const click = (id: string) => {
    (host().querySelector('#' + id) as HTMLElement).click();
    fixture.detectChanges();
  };

  it('copies with navigator.clipboard when it is available, indentation removed', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(window.navigator, 'clipboard', { value: { writeText }, configurable: true });
    Object.defineProperty(window, 'isSecureContext', { value: true, configurable: true });

    respond('Demo', { html: page });
    click('b');
    await Promise.resolve(); await Promise.resolve();
    expect(writeText).toHaveBeenCalledWith('@echo off\n  echo hi');
    expect(host().querySelector('#b')?.textContent).toBe('Copied!');
  });

  it('still copies on plain http, where navigator.clipboard does not exist', async () => {
    Object.defineProperty(window.navigator, 'clipboard', { value: undefined, configurable: true });
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    let copiedFrom = '';
    (document as any).execCommand = vi.fn(() => {
      copiedFrom = (document.querySelector('textarea') as HTMLTextAreaElement).value;
      return true;
    });

    respond('Demo', { html: page });
    click('b');
    await Promise.resolve();
    expect((document as any).execCommand).toHaveBeenCalledWith('copy');
    expect(copiedFrom).toBe('@echo off\n  echo hi');
    expect(host().querySelector('#b')?.textContent).toBe('Copied!');
    expect(document.querySelector('textarea')).toBeNull();   // helper box cleaned up
  });

  it('says so when copying is impossible', async () => {
    Object.defineProperty(window.navigator, 'clipboard', { value: undefined, configurable: true });
    (document as any).execCommand = vi.fn(() => false);
    respond('Demo', { html: page });
    click('b');
    await Promise.resolve();
    expect(host().querySelector('#b')?.textContent).toContain('Copy failed');
  });

  it('downloads a .bat with Windows line endings', async () => {
    let blob: Blob | undefined;
    (URL as any).createObjectURL = vi.fn((b: Blob) => { blob = b; return 'blob:test'; });
    (URL as any).revokeObjectURL = vi.fn();
    const clicked: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push(this.download);
    });

    respond('Demo', { html: page });
    click('d');

    expect(clicked).toEqual(['run.bat']);
    const text = await new Promise<string>((res) => {
      const r = new FileReader();
      r.onload = () => res(String(r.result));
      r.readAsText(blob!);
    });
    expect(text).toBe('@echo off\r\n  echo hi\r\n');
    expect(text.replace(/\r\n/g, '').includes('\n')).toBe(false);   // no bare LF anywhere
  });

  it('does nothing for a button whose target id does not exist', () => {
    respond('Demo', { html: '<button id="x" data-copy="nope">Copy</button>' });
    click('x');
    expect(host().querySelector('#x')?.textContent).toBe('Copy');
  });
});

describe('helpers', () => {
  it('dedent removes shared indentation and the empty lines around the text', () => {
    expect(dedent('\n      a\n        b\n      c\n    ')).toBe('a\n  b\nc');
  });
  it('dedent keeps text that is already at column 0', () => {
    expect(dedent('@echo off\n    echo x\nexit')).toBe('@echo off\n    echo x\nexit');
  });
  it('dedent copes with empty and blank-line input', () => {
    expect(dedent('')).toBe('');
    expect(dedent('\n\n  \n')).toBe('');
    expect(dedent('  a\n\n  b')).toBe('a\n\nb');
  });
  it('dedent normalises CRLF', () => {
    expect(dedent('  a\r\n  b')).toBe('a\nb');
  });
  it('isHtml / isAppShell recognise answers that are not real css or js files', () => {
    expect(isHtml('  <!doctype html>')).toBe(true);
    expect(isHtml('.a{}')).toBe(false);
    expect(isAppShell('<body><app-root></app-root>')).toBe(true);
    expect(isAppShell('<div class="guide">x</div>')).toBe(false);
  });
  it('a wrapped script can run twice without "already declared" errors', () => {
    const js = 'const counter = 1; let other = 2;';
    expect(() => new Function(js + '\n' + js)).toThrow();                     // unwrapped: second declaration is a SyntaxError
    expect(() => { new Function(wrapScript(js, 'A & B'))(); new Function(wrapScript(js, 'A & B'))(); }).not.toThrow();
    expect(wrapScript('x', 'A & B (1)')).toContain('//# sourceURL=guide-A-B-1-.js');
  });
});
