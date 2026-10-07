import { Component, ChangeDetectionStrategy, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { Subscription } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { DragDropDirective } from '../directives/drag-drop.directive';
import { Urlbase } from '../services/urlbase';
import { FolderNodeComponent } from '../folder-node/folder-node.component';
import { SearchBoxComponent } from '../shared/search-box/search-box.component';
import { MediaFolderTreeDto, MediaTrackDto } from '../models/media-folder-tree.model';
import { MenuOption } from '../models/menu.model';

interface SrtFile { name: string; type: string; sizeBytes: number; modifiedUtc: string; }
interface VideoTree { menu: string; fileCount: number; folders: MediaFolderTreeDto[]; tracks: MediaTrackDto[]; }
interface SearchHit { track: MediaTrackDto; folder: string; }
interface VoiceOption { id: string; label: string; }
interface VoiceoverConfig {
  voices: VoiceOption[];
  defaultVoice: string;
  menus: MenuOption[];
  srtFolder: string;
  outputFolder: string;
  videoExtensions: string[];
  subtitleExtensions: string[];
  maxAdjustPercent: number;
  matchVoiceByDefault: boolean;
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
  imports: [CommonModule, FormsModule, DragDropDirective, FolderNodeComponent, SearchBoxComponent],
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

  // Name of the selected MediaMenu menu; set from the first menu the server offers.
  videoMenu = signal('');
  tree = signal<VideoTree | null>(null);
  treeLoading = signal(false);
  // Text from the shared search box. The box keeps its own text, so it is never cleared from here.
  query = signal('');
  selectedVideo = signal<MediaTrackDto | null>(null);
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

  // Voice options. With matchVoice on (the default), the server detects male/female and pitch
  // from the video and ignores the voice select and both sliders.
  matchVoice = signal(true);
  ratePercent = signal(0);
  pitchPercent = signal(0);

  hasSrt = computed(() => !!this.selectedSrt() || !!this.uploadedSrt());
  hasVideo = computed(() => !!this.selectedVideo() || !!this.uploadedVideo());
  // The voice-matching checkbox and both convert buttons need a subtitle file AND a video file.
  canConvert = computed(() => this.hasSrt() && this.hasVideo() && !this.isRunning());
  canEmbed = computed(() => this.canConvert());
  canReset = computed(() =>
    this.isRunning() || this.hasSrt() || this.hasVideo() || !!this.result() ||
    !!this.errorMessage() || this.logLines().length > 0 || !!this.stateText() ||
    this.matchVoice() !== (this.config()?.matchVoiceByDefault ?? true) || this.ratePercent() !== 0 || this.pitchPercent() !== 0);

  hint = computed(() => {
    if (!this.hasSrt() && !this.hasVideo()) return 'Select a subtitle file and a video file to enable conversion.';
    if (!this.hasSrt()) return 'Select a subtitle file to enable conversion.';
    if (!this.hasVideo()) return 'Select a video file to enable conversion.';
    return '';
  });

  subtitleAllowed = computed(() => this.config()?.subtitleExtensions ?? ['.srt', '.vtt']);
  subtitleAccept = computed(() => this.subtitleAllowed().join(','));
  maxAdjust = computed(() => this.config()?.maxAdjustPercent ?? 25);

  videoAccept = computed(() => (this.config()?.videoExtensions ?? ['.mp4']).join(','));

  // Header of the folder card: the menu's title with a count badge.
  menuTitle = computed(() => this.titleOf(this.videoMenu()));
  countLabel = computed(() => {
    const n = this.tree()?.fileCount ?? 0;
    return `${n} ${n === 1 ? 'video' : 'videos'}`;
  });
  folderPath = computed(() => `//medias/${this.videoMenu()}`);

  // While something is typed in the search box, matches are shown as one flat list (with their folder
  // path) instead of the collapsed tree, so a hit is never hidden inside a closed folder.
  searchResults = computed(() => {
    const q = this.query().trim().toLocaleLowerCase();
    if (!q) return null;

    const hits: SearchHit[] = [];
    let total = 0;
    const visit = (tracks: MediaTrackDto[], path: string) => {
      for (const t of tracks) {
        if (!`${t.displayTitle} ${t.fileName} ${path}`.toLocaleLowerCase().includes(q)) continue;
        total++;
        if (hits.length < 200) hits.push({ track: t, folder: path });
      }
    };
    const walk = (folders: MediaFolderTreeDto[], prefix: string) => {
      for (const f of folders) {
        const path = prefix ? `${prefix} / ${f.name}` : f.name;
        visit(f.tracks, path);
        walk(f.folders, path);
      }
    };

    const t = this.tree();
    if (t) {
      visit(t.tracks, '');
      walk(t.folders, '');
    }
    return { hits, total };
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
        this.matchVoice.set(cfg.matchVoiceByDefault);
        if (cfg.menus.length > 0) {
          this.videoMenu.set(cfg.menus[0].name);
          this.loadTree();
        }
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

  loadTree(): void {
    this.treeLoading.set(true);
    this.http.get<VideoTree>(`${this.apiBase}/tree`, { params: { menu: this.videoMenu() } }).subscribe({
      next: (t) => { this.tree.set(t); this.treeLoading.set(false); },
      error: (err) => {
        console.error('Failed to load the video tree:', err);
        this.tree.set(null);
        this.treeLoading.set(false);
      }
    });
  }

  titleOf(menu: string): string {
    return this.config()?.menus.find(m => m.name === menu)?.title ?? menu;
  }

  setMenu(menu: string): void {
    if (menu === this.videoMenu()) return;
    this.videoMenu.set(menu);
    this.tree.set(null);
    this.loadTree();
  }

  // A file from the list and an uploaded file are mutually exclusive sources for the same
  // input, as in subtitle-dashboard.
  selectSrt(file: SrtFile): void {
    this.selectedSrt.set(file);
    this.uploadedSrt.set(null);
    this.clearMessages();
  }

  // Called by folder-node (any depth) and by the search results.
  selectVideo(track: MediaTrackDto): void {
    if (this.isRunning()) return;
    this.selectedVideo.set(track);
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
    const allowed = this.subtitleAllowed();
    if (!allowed.includes(this.extensionOf(file.name))) {
      this.inputError.set(`Subtitle files must be ${allowed.join(' or ')}.`);
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
    const useMatch = this.matchVoice() && this.hasVideo();
    form.append('matchVoice', String(useMatch));
    form.append('ratePercent', String(this.ratePercent()));
    form.append('pitchPercent', String(this.pitchPercent()));

    const srtUpload = this.uploadedSrt();
    const srtServer = this.selectedSrt();
    if (srtUpload) form.append('srtFile', srtUpload, srtUpload.name);
    else if (srtServer) form.append('srtServerName', srtServer.name);

    let hasUpload = !!srtUpload;
    if (mode === 'mp3-embed' || useMatch) {
      const videoUpload = this.uploadedVideo();
      const videoServer = this.selectedVideo();
      if (videoUpload) { form.append('videoFile', videoUpload, videoUpload.name); hasUpload = true; }
      else if (videoServer) form.append('videoServerPath', videoServer.url);
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
    this.jobId = null;

    // Back to the defaults: voice matching as configured, sliders centred, default voice.
    const cfg = this.config();
    this.matchVoice.set(cfg?.matchVoiceByDefault ?? true);
    this.ratePercent.set(0);
    this.pitchPercent.set(0);
    if (cfg) this.voice.set(cfg.defaultVoice);
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
