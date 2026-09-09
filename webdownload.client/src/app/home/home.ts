import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { SignalrService } from '../services/signalr.service';
import { FormsModule } from '@angular/forms'
import { AsyncPipe } from '@angular/common';
import { downloadInfo, SubtitleTrackOption } from '../models/webdownload.model';
import { BehaviorSubject } from 'rxjs';
import { LinebreakPipe } from '../pipes/linebreak.pipe';
import { signal } from '@angular/core';

@Component({
  selector: 'app-home',
  imports: [FormsModule, LinebreakPipe, AsyncPipe],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  title = 'webdownload.client';
  signalRService = inject(SignalrService);
  private cdr = inject(ChangeDetectorRef);

  private outputSubject = new BehaviorSubject<string[]>([]);
  output$ = this.outputSubject.asObservable();

  url: string = '';
  isDownloading = signal(false);

  // Single source of truth for "lock the whole form" - true while we're
  // waiting on title/subtitle lookups or an actual download/translation.
  isPageBusy(): boolean {
    return this.isDownloading() || this.isLoadingTitle || this.isLoadingSubtitles;
  }
  options: string = '';
  chkAudio: boolean = false;
  checkAudioChapter: boolean = true;
  selectedAudioFormat: string = 'flac';
  selectedMenuValue: string = "MOVIES";
  chkVideo: boolean = true;
  chKSubTitleInclude: boolean = true;
  chapter = signal<string[]>([]);

  // Subtitle checkboxes: starts empty, gets filled in once we know what
  // YouTube actually has for the entered URL (see getSubtitles()).
  subtitleOptions: SubtitleTrackOption[] = [];
  isLoadingSubtitles = false;
  isLoadingTitle = false;

  // "Translate to ..." checkboxes, mutually exclusive, same pattern as the
  // cookie checkboxes below.
  translateToKm: boolean = false;
  translateToEn: boolean = false;
  translatedFile: string = '';
  translationStatusMessage: string = '';
  private translationPollHandle: ReturnType<typeof setInterval> | null = null;

  onTranslateToKmChange(): void {
    if (this.translateToKm) {
      this.translateToEn = false;
    }
  }

  onTranslateToEnChange(): void {
    if (this.translateToEn) {
      this.translateToKm = false;
    }
  }

  get translateTo(): string {
    if (this.translateToKm) return 'km';
    if (this.translateToEn) return 'en';
    return '';
  }

  progress: string = '';
  error: string = '';
  ReceiveSpeed: string = '';
  ReceiveETA: string = '';
  ReceiveData: string = '';
  ReceiveVideoIndex: string = '';
  ReceiveTotalSize: string = '';
  TotalFragments: string = '';
  CurrentFragment: string = '';
  finish: string = '';
  ReceiveFileName: string = '';
  ReceiveState: string = '';
  outputFolder: string = "9";
  connectionId!: string;
  // Stable for the life of this page load - unlike the transport
  // connectionId (which changes every time the WebSocket reconnects), the
  // server groups all progress sends by this id, so a mid-download network
  // blip doesn't orphan the in-flight download/translation.
  downloadGroupId: string = (crypto as any)?.randomUUID?.() ??
    `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  ytDlpCommand: string = '';

  useCookiesFromBrowser: boolean = false;
  useCookiesFile: boolean = false;

  onCookiesFromBrowserChange(): void {
    if (this.useCookiesFromBrowser) {
      this.useCookiesFile = false;
    }
    this.updateAutoOptions();
  }

  onCookiesFileChange(): void {
    if (this.useCookiesFile) {
      this.useCookiesFromBrowser = false;
    }
    this.updateAutoOptions();
  }

  private buildAutoOptions(): string {
    const parts: string[] = [];
    if (this.chkAudio) {
      parts.push('-f bestaudio');
      if (this.selectedAudioFormat) {
        parts.push(`-x --audio-format ${this.selectedAudioFormat}`);
      }
      if (this.checkAudioChapter) {
        parts.push('--split-chapters');
      }
    }
    if (this.useCookiesFromBrowser) {
      parts.push('--cookies-from-browser chrome');
    } else if (this.useCookiesFile) {
      parts.push('--cookies cookies.txt');
    }
    return parts.join('\n');
  }

  // Phase 1: always clear the box and rebuild it entirely from the current switches.
  updateAutoOptions(): void {
    this.options = this.buildAutoOptions();
  }

  private resetDownloadStatus(): void {
    this.progress = '';
    this.error = '';
    this.ReceiveSpeed = '';
    this.ReceiveETA = '';
    this.ReceiveData = '';
    this.ReceiveVideoIndex = '';
    this.ReceiveTotalSize = '';
    this.TotalFragments = '';
    this.CurrentFragment = '';
    this.finish = '';
    this.ReceiveFileName = '';
    this.ReceiveState = '';
    this.ytDlpCommand = '';
    this.chapter.set([]);
    this.outputSubject.next([]);
    this.translatedFile = '';
  }

  onUrlChange(): void {
    this.resetDownloadStatus();
    this.subtitleOptions = [];
    this.getTitle();
    if (this.url && this.url.trim() !== '') {
      this.updateAutoOptions();
      this.getSubtitles();
    }
  }

  // Ask the server what subtitle/caption tracks YouTube has for this URL,
  // then render them as checkboxes (see ReceiveSubtitleList handler).
  getSubtitles(): void {
    if (!this.chKSubTitleInclude || !this.url || this.url.trim() === '') {
      return;
    }
    this.isLoadingSubtitles = true;
    this.signalRService.ensureConnected().then(async connId => {
      this.connectionId = connId;
      await this.signalRService.joinGroup(this.downloadGroupId);
      const payload = {
        downloadId: this.downloadGroupId,
        url: this.url,
      };
      console.log(`[${new Date().toISOString()}] Sending HubGetSubtitlesAsync`, payload);
      this.signalRService.invokeMethod('HubGetSubtitlesAsync', payload);
    });
  }

  onSubtitleToggleChanged(): void {
    // Placeholder hook if we need side-effects later; checkbox state is
    // bound directly via [(ngModel)]="option.checked" in the template.
  }

  selectAllSubtitles(): void {
    this.subtitleOptions.forEach(o => o.checked = true);
  }

  clearAllSubtitles(): void {
    this.subtitleOptions.forEach(o => o.checked = false);
  }

  // Phase 2: draggable reference list of common yt-dlp args that can be dropped into the Options box.
  ytDlpArgList: { label: string; arg: string }[] = [
    { label: 'Embed subtitles (closed captions)', arg: '--embed-subs' },
    { label: 'Prefer SRT subtitle format', arg: '--sub-format srt/best' },
    { label: 'Convert subtitles to SRT', arg: '--convert-subs srt' },
    { label: 'Remux to MP4 container', arg: '--remux-video mp4' },
    { label: 'Best video+audio merged', arg: '-f bestvideo+bestaudio/best' },
    { label: 'Cap resolution at 1080p', arg: '-f "bv*[height<=1080]+ba/b[height<=1080]"' },
    { label: 'Force merged output to MP4', arg: '--merge-output-format mp4' },
    { label: 'Embed chapters', arg: '--embed-chapters' },
    { label: 'Embed metadata', arg: '--embed-metadata' },
    { label: 'Embed thumbnail as cover art', arg: '--embed-thumbnail' },
    { label: 'Only specific playlist items', arg: '--playlist-items 1-5' },
    { label: 'Single video, ignore playlist', arg: '--no-playlist' },
    { label: 'Limit download rate', arg: '--limit-rate 2M' },
    { label: 'Retry count on network errors', arg: '--retries 10' },
  ];

  draggingArg: string | null = null;

  onArgDragStart(event: DragEvent, arg: string): void {
    event.dataTransfer?.setData('text/plain', arg);
    this.draggingArg = arg;
  }

  onArgDragEnd(): void {
    this.draggingArg = null;
  }

  onOptionsDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  onOptionsDrop(event: DragEvent): void {
    event.preventDefault();
    const dropped = event.dataTransfer?.getData('text/plain');
    if (dropped) {
      this.options = this.options && this.options.trim() !== ''
        ? this.options + '\n' + dropped
        : dropped;
    }
    this.draggingArg = null;
  }


  ngOnInit(): void {
    // Initialize SignalR connection
    this.signalRService.startConnection().then(() => {
      this.signalRService.joinGroup(this.downloadGroupId);
    });

    // If the WebSocket drops mid-download (browser sleep, flaky wifi, dev
    // server hot reload, proxy idle timeout, etc.), SignalR auto-reconnects
    // but with a brand new connection id. Rejoin our stable group so the
    // in-flight download/translation on the server keeps reaching us
    // instead of silently sending updates to a connection that's gone.
    this.signalRService.onReconnected(() => {
      this.signalRService.joinGroup(this.downloadGroupId);
    });

    this.signalRService.addHandler('ReceiveCommand', (info: downloadInfo) => {
      this.ytDlpCommand = info.command || '';
    });
    this.signalRService.addHandler('ReceiveTotalFragment', (info: downloadInfo) => {
      this.TotalFragments = `${info.frag}`;
    });
    this.signalRService.addHandler('ReceiveOutput', (info: downloadInfo) => {
      const currentOutput = this.outputSubject.value;
      /* const updatedOutput = [...currentOutput, `${info.output}`];*/
      const updatedOutput = [`${info.output}`, ...currentOutput];
      this.outputSubject.next(updatedOutput);
    });
    this.signalRService.addHandler('ReceiveLastDownloadInfo', (info: downloadInfo) => {
      this.progress = info.progress;
      this.ReceiveSpeed = info.speed;
      this.ReceiveETA = info.eta;
      this.ReceiveTotalSize = info.size;
      this.ReceiveState = info.state;
    });

    this.signalRService.addHandler('ReceiveDownloadInfo', (info: downloadInfo) => {
      this.progress = info.progress;
      this.ReceiveSpeed = info.speed;
      this.ReceiveETA = info.eta;
      this.ReceiveTotalSize = info.size;
      this.ReceiveState = info.state;
      if (info.frag) {
        this.CurrentFragment = info.frag;
      }
    });
    // Subscribe to error messages
    this.signalRService.addHandler('ReceiveError', (info: downloadInfo) => {
      this.error += `${info.error}` + "\n\n";
      this.isLoadingTitle = false;
      this.isLoadingSubtitles = false;
      console.log('[DEBUG] ReceiveError -> isLoadingTitle:', this.isLoadingTitle, 'isLoadingSubtitles:', this.isLoadingSubtitles);
      this.cdr.detectChanges();
    });

    // Subscribe to download finished
    this.signalRService.addHandler('ReceiveDownloadFinished', (info: downloadInfo) => {
      this.isDownloading.set(false);
      this.finish = `${info.finishOutput}`;
      this.cdr.detectChanges();
    });
    this.signalRService.addHandler('ReceiveState', (info: downloadInfo) => {
      this.ReceiveState = `${info.state}`;
      this.cdr.detectChanges();
    });

    this.signalRService.addHandler('ReceiveFileName', (info: downloadInfo) => {
      this.ReceiveFileName = `${info.fileName}`;
      this.isLoadingTitle = false;
      console.log('[DEBUG] ReceiveFileName handler ran -> isLoadingTitle is now:', this.isLoadingTitle, 'isPageBusy():', this.isPageBusy());
      this.cdr.detectChanges();
      console.log('[DEBUG] detectChanges() called after ReceiveFileName');
    });

    this.signalRService.addHandler('ReceiveChapterFileName', (info: downloadInfo) => {
      this.chapter.update(current => [...current, `${info.chapter}`]);
      this.cdr.detectChanges();
    });

    this.signalRService.addHandler('ReceiveSubtitleList', (info: downloadInfo) => {
      this.isLoadingSubtitles = false;
      const tracks = info.subtitleTracks || [];
      this.subtitleOptions = tracks.map(t => ({ ...t, checked: false }));
      console.log('[DEBUG] ReceiveSubtitleList handler ran -> isLoadingSubtitles is now:', this.isLoadingSubtitles, 'isPageBusy():', this.isPageBusy(), 'tracks:', tracks.length);
      this.cdr.detectChanges();
      console.log('[DEBUG] detectChanges() called after ReceiveSubtitleList');
    });

    this.signalRService.addHandler('ReceiveTranslatedFile', (info: downloadInfo) => {
      this.translatedFile = info.translatedFile || '';
      this.translationStatusMessage = `Translation completed: ${this.translatedFile}`;
      this.stopTranslationStatusPolling();
    });
  }
  ngOnDestroy(): void {
    this.stopTranslationStatusPolling();
    // Stop SignalR connection
    this.outputSubject.complete();
    this.signalRService.unregisterHandlers();
    this.signalRService.hubConnection.stop().then(() => {
      console.log('HubConnection stopped and listeners cleaned up.');
    });
  }

  onCheckboxChange(changedCheckbox: string) {
    if (changedCheckbox === 'chkAudio') {
      this.chkVideo = !this.chkAudio;

    } else if (changedCheckbox === 'chkVideo') {
      this.chkAudio = !this.chkVideo;
    }
    this.updateAutoOptions();
  }

  getTitle(): void {
    this.isLoadingTitle = true;
    this.signalRService.ensureConnected().then(async connId => {
      this.connectionId = connId;
      await this.signalRService.joinGroup(this.downloadGroupId);
      const payload = {
        downloadId: this.downloadGroupId,
        url: this.url,
      };
      console.log(`[${new Date().toISOString()}] Sending HubGetTitleServiceAsync`, payload);
      this.signalRService.invokeMethod('HubGetTitleServiceAsync', payload);
    });
  }
  startDownload(): void {
    this.signalRService.ensureConnected().then(async connId => {
      this.connectionId = connId;
      await this.signalRService.joinGroup(this.downloadGroupId);
      const subtitleLangs = this.chKSubTitleInclude
        ? this.subtitleOptions.filter(o => o.checked).map(o => o.code)
        : [];
      const payload = {
        downloadId: this.downloadGroupId,
        url: this.url,
        options: this.options,
        audioOnly: this.chkAudio,
        audioFormat: this.selectedAudioFormat,
        audioChapter: this.checkAudioChapter,
        videoOnly: this.chkVideo,
        subtitleLangs: subtitleLangs,
        translateTo: this.translateTo || null,
        outputFolder: `${this.selectedMenuValue}\\${this.outputFolder}`  // Send the user-provided output folder.
      };
      this.isDownloading.set(true);
      this.translationStatusMessage = '';
      if (this.translateTo) {
        this.startTranslationStatusPolling();
      }
      this.signalRService.invokeMethod('HubStartDownloadServiceAsync', payload);
    });
  }

  // Manual "check now" button - independent of the live push, works even
  // right after a page reload as long as downloadGroupId is unchanged.
  async checkTranslationStatus(): Promise<void> {
    const status = await this.signalRService.invokeMethod('GetTranslationStatus', this.downloadGroupId) as any;
    this.applyTranslationStatus(status);
  }

  private applyTranslationStatus(status: any): void {
    if (!status) {
      this.translationStatusMessage = 'No translation job found yet for this session.';
      return;
    }
    if (status.state === 'Completed') {
      this.translationStatusMessage = `Translation completed: ${status.translatedFile}`;
      this.stopTranslationStatusPolling();
    } else if (status.state === 'Failed') {
      this.translationStatusMessage = `Translation failed: ${status.error}`;
      this.stopTranslationStatusPolling();
    } else {
      const pct = status.totalLines > 0 ? Math.round((status.currentLine * 100) / status.totalLines) : 0;
      this.translationStatusMessage = `Translating... ${status.currentLine}/${status.totalLines} (${pct}%)`;
    }
  }

  // Poll every few seconds as a fallback/complement to the live push, in
  // case the tab was backgrounded, reloaded, or a message was missed.
  private startTranslationStatusPolling(): void {
    this.stopTranslationStatusPolling();
    this.translationPollHandle = setInterval(() => {
      this.checkTranslationStatus();
    }, 4000);
  }

  private stopTranslationStatusPolling(): void {
    if (this.translationPollHandle) {
      clearInterval(this.translationPollHandle);
      this.translationPollHandle = null;
    }
  }
}

