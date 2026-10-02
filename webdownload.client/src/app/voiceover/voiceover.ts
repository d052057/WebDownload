import { Component, ChangeDetectionStrategy, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { Subscription } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { DragDropDirective } from '../directives/drag-drop.directive';
import { Urlbase } from '../services/urlbase';

interface SrtFile { name: string; type: string; sizeBytes: number; modifiedUtc: string; }
interface VideoFile { id: string; fileName: string; folder: string; relativePath: string; }
interface VoiceOption { id: string; label: string; }
interface VoiceoverConfig {
  voices: VoiceOption[];
  defaultVoice: string;
  menus: string[];
  srtFolder: string;
  outputFolder: string;
  videoExtensions: string[];
}
interface JobUpdate {
  jobId: string;
  kind: 'state' | 'log' | 'done' | 'error' | 'cancelled';
  state?: string;
  percent?: number;
  message?: string;
  mp3Name?: string;
  mp3Url?: string;
  videoName?: string;
  videoUrl?: string;
}
interface JobResult { mp3Name?: string; mp3Url?: string; videoName?: string; videoUrl?: string; }
type ConvertMode = 'mp3' | 'mp3-embed';

@Component({
  imports: [CommonModule, FormsModule, DragDropDirective],
  selector: 'app-voiceover',
  styleUrl: './voiceover.scss',
  templateUrl: './voiceover.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Voiceover implements OnInit, OnDestroy {
  private urlbase = inject(Urlbase);
  private http = inject(HttpClient);

  private readonly basePrefix: string;
  private readonly apiBase: string;
  private readonly hubUrl: string;

  private hub: signalR.HubConnection | null = null;
  private upload$: Subscription | null = null;
  private jobId: string | null = null;

  // Signals, because most of these change inside async HTTP/SignalR callbacks (same reason as
  // subtitle-dashboard).
  config = signal<VoiceoverConfig | null>(null);
  voice = signal('');

  srtFiles = signal<SrtFile[]>([]);
  selectedSrt = signal<SrtFile | null>(null);
  uploadedSrt = signal<File | null>(null);

  videoMenu = signal('movies');
  videoFiles = signal<VideoFile[]>([]);
  videoFilter = signal('');
  selectedVideo = signal<VideoFile | null>(null);
  uploadedVideo = signal<File | null>(null);

  activeMode = signal<ConvertMode | null>(null);
  isRunning = signal(false);
  percent = signal(0);
  uploadPercent = signal<number | null>(null);
  stateText = signal('');
  logLines = signal<string[]>([]);
  result = signal<JobResult | null>(null);
  errorMessage = signal('');
  inputError = signal('');

  hasSrt = computed(() => !!this.selectedSrt() || !!this.uploadedSrt());
  hasVideo = computed(() => !!this.selectedVideo() || !!this.uploadedVideo());
  canConvert = computed(() => this.hasSrt() && !this.isRunning());
  canEmbed = computed(() => this.hasSrt() && this.hasVideo() && !this.isRunning());
  canReset = computed(() =>
    this.isRunning() || this.hasSrt() || this.hasVideo() || !!this.result() ||
    !!this.errorMessage() || this.logLines().length > 0 || !!this.stateText());

  hint = computed(() => {
    if (!this.hasSrt()) return 'Select or drop an srt file to convert it to MP3.';
    if (!this.hasVideo()) return 'Select or drop an mp4 file as well to enable embedding.';
    return '';
  });

  videoAccept = computed(() => (this.config()?.videoExtensions ?? ['.mp4']).join(','));

  videoGroups = computed(() => {
    const q = this.videoFilter().trim().toLowerCase();
    const groups = new Map<string, VideoFile[]>();
    for (const v of this.videoFiles()) {
      if (q && !v.fileName.toLowerCase().includes(q) && !v.folder.toLowerCase().includes(q)) continue;
      const list = groups.get(v.folder);
      if (list) list.push(v); else groups.set(v.folder, [v]);
    }
    return [...groups].map(([folder, files]) => ({ folder, files }));
  });

  constructor() {
    const segment = this.urlbase.baseUrl();
    this.basePrefix = segment ? `/${segment}` : '';
    this.apiBase = `${this.basePrefix}/api/Voiceover`;
    this.hubUrl = `${this.basePrefix}/convertHub`;
  }

  ngOnInit(): void {
    this.http.get<VoiceoverConfig>(`${this.apiBase}/config`).subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        this.voice.set(cfg.defaultVoice);
        if (cfg.menus.length > 0) this.videoMenu.set(cfg.menus[0]);
        this.loadVideos();
      },
      error: (err) => console.error('Failed to load Voiceover settings:', err)
    });
    this.loadSrt();
  }

  ngOnDestroy(): void {
    this.upload$?.unsubscribe();
    void this.hub?.stop();
  }

  // ---- source lists -----------------------------------------------------------------

  loadSrt(): void {
    this.http.get<SrtFile[]>(`${this.apiBase}/srt`).subscribe({
      next: (files) => this.srtFiles.set(files),
      error: (err) => console.error('Failed to list subtitle files:', err)
    });
  }

  loadVideos(): void {
    this.http.get<VideoFile[]>(`${this.apiBase}/mp4`, { params: { menu: this.videoMenu() } }).subscribe({
      next: (files) => this.videoFiles.set(files),
      error: (err) => {
        console.error('Failed to list video files:', err);
        this.videoFiles.set([]);
      }
    });
  }

  setMenu(menu: string): void {
    if (menu === this.videoMenu()) return;
    this.videoMenu.set(menu);
    this.videoFilter.set('');
    this.loadVideos();
  }

  // A file from the list and an uploaded file are mutually exclusive sources for the same
  // input, as in subtitle-dashboard.
  selectSrt(file: SrtFile): void {
    this.selectedSrt.set(file);
    this.uploadedSrt.set(null);
    this.clearMessages();
  }

  selectVideo(file: VideoFile): void {
    this.selectedVideo.set(file);
    this.uploadedVideo.set(null);
    this.clearMessages();
  }

  // ---- drop zones and file inputs -----------------------------------------------------

  onSrtDropped(files: FileList): void {
    if (files.length > 0) this.acceptSrt(files[0]);
  }

  onSrtInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) this.acceptSrt(input.files[0]);
    input.value = '';
  }

  onVideoDropped(files: FileList): void {
    if (files.length > 0) this.acceptVideo(files[0]);
  }

  onVideoInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) this.acceptVideo(input.files[0]);
    input.value = '';
  }

  private acceptSrt(file: File): void {
    const ext = this.extensionOf(file.name);
    if (ext !== '.srt' && ext !== '.vtt') {
      this.inputError.set('Subtitle files must be .srt or .vtt.');
      return;
    }
    this.uploadedSrt.set(file);
    this.selectedSrt.set(null);
    this.clearMessages();
  }

  private acceptVideo(file: File): void {
    const allowed = this.config()?.videoExtensions ?? ['.mp4'];
    if (!allowed.includes(this.extensionOf(file.name))) {
      this.inputError.set(`Video files must be one of: ${allowed.join(', ')}.`);
      return;
    }
    this.uploadedVideo.set(file);
    this.selectedVideo.set(null);
    this.clearMessages();
  }

  private extensionOf(name: string): string {
    const i = name.lastIndexOf('.');
    return i < 0 ? '' : name.slice(i).toLowerCase();
  }

  // ---- converting -----------------------------------------------------------------------

  async start(mode: ConvertMode): Promise<void> {
    if (this.isRunning()) return;
    if (mode === 'mp3-embed' ? !this.canEmbed() : !this.canConvert()) return;

    this.resetProgress();
    const jobId = this.newJobId();
    this.jobId = jobId;
    this.activeMode.set(mode);
    this.isRunning.set(true);
    this.stateText.set('Connecting');

    // Join the job's progress group BEFORE starting the job so no early update is missed.
    try {
      const hub = await this.ensureHub();
      await hub.invoke('JoinGroup', jobId);
    } catch (err) {
      console.error('SignalR connection failed:', err);
      this.fail('Could not open the progress connection. Check that the server is running.');
      return;
    }

    const form = new FormData();
    form.append('jobId', jobId);
    form.append('mode', mode);
    form.append('voice', this.voice());

    const srtUpload = this.uploadedSrt();
    const srtServer = this.selectedSrt();
    if (srtUpload) form.append('srtFile', srtUpload, srtUpload.name);
    else if (srtServer) form.append('srtServerName', srtServer.name);

    let hasUpload = !!srtUpload;
    if (mode === 'mp3-embed') {
      const videoUpload = this.uploadedVideo();
      const videoServer = this.selectedVideo();
      if (videoUpload) { form.append('videoFile', videoUpload, videoUpload.name); hasUpload = true; }
      else if (videoServer) form.append('videoServerPath', videoServer.relativePath);
    }

    this.stateText.set(hasUpload ? 'Uploading' : 'Starting');

    this.upload$ = this.http
      .post(`${this.apiBase}/convert`, form, { observe: 'events', reportProgress: true })
      .subscribe({
        next: (ev: HttpEvent<unknown>) => {
          if (ev.type === HttpEventType.UploadProgress && ev.total) {
            this.uploadPercent.set(Math.round((100 * ev.loaded) / ev.total));
          } else if (ev.type === HttpEventType.Response) {
            // Accepted: the job is now running on the server and reports over SignalR.
            this.uploadPercent.set(null);
            this.upload$ = null;
            if (this.isRunning()) this.stateText.set('Queued');
          }
        },
        error: (err) => {
          this.uploadPercent.set(null);
          this.fail(typeof err?.error === 'string' && err.error ? err.error : 'The server could not start the conversion.');
        }
      });
  }

  async cancelOrReset(): Promise<void> {
    if (!this.isRunning()) {
      this.resetAll();
      return;
    }

    this.stateText.set('Cancelling');
    this.upload$?.unsubscribe(); // aborts an upload that is still in flight
    this.upload$ = null;
    this.uploadPercent.set(null);

    let serverJobFound = false;
    if (this.hub && this.jobId) {
      try {
        serverJobFound = await this.hub.invoke<boolean>('CancelJob', this.jobId);
      } catch {
        serverJobFound = false;
      }
    }
    // No server job yet (still uploading) means no "cancelled" message will ever arrive.
    if (!serverJobFound) {
      this.isRunning.set(false);
      this.activeMode.set(null);
      this.stateText.set('Cancelled');
    }
  }

  private async ensureHub(): Promise<signalR.HubConnection> {
    if (!this.hub) {
      this.hub = new signalR.HubConnectionBuilder()
        .withUrl(this.hubUrl)
        .withAutomaticReconnect()
        .build();
      this.hub.on('ReceiveJobUpdate', (u: JobUpdate) => this.handleUpdate(u));
    }
    if (this.hub.state === signalR.HubConnectionState.Disconnected) {
      await this.hub.start();
    }
    return this.hub;
  }

  private handleUpdate(u: JobUpdate): void {
    if (u.jobId !== this.jobId) return;

    switch (u.kind) {
      case 'state':
        if (u.state) this.stateText.set(u.state);
        if (u.percent != null) this.percent.set(u.percent);
        break;
      case 'log':
        if (u.message) this.logLines.update((lines) => [...lines.slice(-199), u.message!]);
        break;
      case 'done':
        this.result.set({ mp3Name: u.mp3Name, mp3Url: u.mp3Url, videoName: u.videoName, videoUrl: u.videoUrl });
        this.percent.set(100);
        this.stateText.set('Done');
        this.finish();
        break;
      case 'error':
        this.errorMessage.set(u.message ?? 'The conversion failed.');
        this.stateText.set('Failed');
        this.finish();
        break;
      case 'cancelled':
        this.stateText.set('Cancelled');
        this.finish();
        break;
    }
  }

  private fail(message: string): void {
    this.errorMessage.set(message);
    this.stateText.set('Failed');
    this.finish();
  }

  private finish(): void {
    this.isRunning.set(false);
    this.activeMode.set(null);
    this.uploadPercent.set(null);
  }

  // ---- reset helpers ----------------------------------------------------------------------

  private clearMessages(): void {
    this.inputError.set('');
    this.errorMessage.set('');
    this.result.set(null);
  }

  private resetProgress(): void {
    this.clearMessages();
    this.percent.set(0);
    this.uploadPercent.set(null);
    this.stateText.set('');
    this.logLines.set([]);
  }

  private resetAll(): void {
    this.resetProgress();
    this.selectedSrt.set(null);
    this.uploadedSrt.set(null);
    this.selectedVideo.set(null);
    this.uploadedVideo.set(null);
    this.videoFilter.set('');
    this.jobId = null;
  }

  // ---- misc -----------------------------------------------------------------------------------

  // The result URLs the server sends start with /medias/..., which sits under the app's path base.
  fileUrl(url: string): string {
    return `${this.basePrefix}${url}`;
  }

  // crypto.randomUUID() only exists in secure contexts (https or localhost), and this app is
  // also served over plain http on the LAN, so build the id from getRandomValues instead.
  private newJobId(): string {
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  }
}
