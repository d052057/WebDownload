import { Component, ChangeDetectionStrategy, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { Urlbase } from '../services/urlbase';

interface MediaItem { id: string; fileName: string; folder: string; relativePath: string; }
interface QualityOption { id: string; label: string; }
interface SplitterConfig { menus: string[]; qualities: QualityOption[]; outputFolder: string; }
interface DeviceInfo { device: 'cuda' | 'cpu'; name: string; note?: string | null; }
interface OutputFile { label: string; name: string; url?: string | null; }
interface SplitterUpdate {
  jobId: string;
  kind: 'state' | 'log' | 'done' | 'error' | 'cancelled';
  state?: string;
  percent?: number;
  message?: string;
  files?: OutputFile[];
}
type SplitMode = 'voice' | 'music' | 'both' | 'cleanup';

@Component({
  imports: [CommonModule, FormsModule],
  selector: 'app-splitter',
  styleUrl: './splitter.scss',
  templateUrl: './splitter.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Splitter implements OnInit, OnDestroy {
  private urlbase = inject(Urlbase);
  private http = inject(HttpClient);

  private readonly basePrefix: string;
  private readonly apiBase: string;
  private readonly hubUrl: string;

  private hub: signalR.HubConnection | null = null;
  private jobId: string | null = null;

  // Signals, because most of these change inside async HTTP/SignalR callbacks (same as Voiceover).
  config = signal<SplitterConfig | null>(null);
  device = signal<DeviceInfo | null>(null);
  deviceLoading = signal(true);

  menu = signal('movies');
  mediaFiles = signal<MediaItem[]>([]);
  filter = signal('');
  selected = signal<MediaItem | null>(null);
  quality = signal('standard');

  activeMode = signal<SplitMode | null>(null);
  isRunning = signal(false);
  percent = signal(0);
  stateText = signal('');
  logLines = signal<string[]>([]);
  files = signal<OutputFile[] | null>(null);
  errorMessage = signal('');

  // Every button needs a selected media file.
  canRun = computed(() => !!this.selected() && !this.isRunning());
  canReset = computed(() =>
    this.isRunning() || !!this.selected() || !!this.files() || !!this.errorMessage() ||
    this.logLines().length > 0 || !!this.stateText() || this.quality() !== 'standard');
  hint = computed(() => (this.selected() ? '' : 'Select a media file to enable the buttons.'));

  groups = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const map = new Map<string, MediaItem[]>();
    for (const m of this.mediaFiles()) {
      if (q && !m.fileName.toLowerCase().includes(q) && !m.folder.toLowerCase().includes(q)) continue;
      const list = map.get(m.folder);
      if (list) list.push(m); else map.set(m.folder, [m]);
    }
    return [...map].map(([folder, items]) => ({ folder, items }));
  });

  constructor() {
    const segment = this.urlbase.baseUrl();
    this.basePrefix = segment ? `/${segment}` : '';
    this.apiBase = `${this.basePrefix}/api/Splitter`;
    this.hubUrl = `${this.basePrefix}/splitterHub`;
  }

  ngOnInit(): void {
    this.http.get<SplitterConfig>(`${this.apiBase}/config`).subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        if (cfg.menus.length > 0) this.menu.set(cfg.menus[0]);
        this.loadMedia();
      },
      error: (err) => console.error('Failed to load Splitter settings:', err)
    });
    this.loadDevice(false);
  }

  ngOnDestroy(): void {
    void this.hub?.stop();
  }

  // ---- lists and hardware ---------------------------------------------------------------

  loadMedia(): void {
    this.http.get<MediaItem[]>(`${this.apiBase}/media`, { params: { menu: this.menu() } }).subscribe({
      next: (items) => this.mediaFiles.set(items),
      error: (err) => {
        console.error('Failed to list media files:', err);
        this.mediaFiles.set([]);
      }
    });
  }

  setMenu(menu: string): void {
    if (menu === this.menu()) return;
    this.menu.set(menu);
    this.filter.set('');
    this.loadMedia();
  }

  select(item: MediaItem): void {
    this.selected.set(item);
    this.clearResults();
  }

  // refresh = true makes the server check the hardware again instead of using its cached answer.
  loadDevice(refresh: boolean): void {
    this.deviceLoading.set(true);
    this.http.get<DeviceInfo>(`${this.apiBase}/hardware`, { params: { refresh } }).subscribe({
      next: (info) => { this.device.set(info); this.deviceLoading.set(false); },
      error: (err) => {
        console.error('Hardware check failed:', err);
        this.device.set({ device: 'cpu', name: 'CPU', note: 'The hardware check could not be reached.' });
        this.deviceLoading.set(false);
      }
    });
  }

  // ---- running a job --------------------------------------------------------------------

  async start(mode: SplitMode): Promise<void> {
    const media = this.selected();
    if (!media || this.isRunning()) return;

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

    this.stateText.set('Starting');
    this.http.post(`${this.apiBase}/jobs`, {
      jobId,
      mode,
      quality: this.quality(),
      mediaPath: media.relativePath
    }).subscribe({
      next: () => {
        // Accepted: the job runs on the server and reports over SignalR.
        if (this.isRunning() && this.stateText() === 'Starting') this.stateText.set('Queued');
      },
      error: (err) => this.fail(
        typeof err?.error === 'string' && err.error ? err.error : 'The server could not start the job.')
    });
  }

  async cancelOrReset(): Promise<void> {
    if (!this.isRunning()) {
      this.resetAll();
      return;
    }

    this.stateText.set('Cancelling');
    let serverJobFound = false;
    if (this.hub && this.jobId) {
      try {
        serverJobFound = await this.hub.invoke<boolean>('CancelJob', this.jobId);
      } catch {
        serverJobFound = false;
      }
    }
    // No server job yet means no "cancelled" message will ever arrive.
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
      this.hub.on('ReceiveSplitterUpdate', (u: SplitterUpdate) => this.handleUpdate(u));
    }
    if (this.hub.state === signalR.HubConnectionState.Disconnected) {
      await this.hub.start();
    }
    return this.hub;
  }

  private handleUpdate(u: SplitterUpdate): void {
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
        this.files.set(u.files ?? []);
        this.percent.set(100);
        this.stateText.set('Done');
        this.finish();
        break;
      case 'error':
        this.errorMessage.set(u.message ?? 'The job failed.');
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
  }

  // ---- reset helpers ------------------------------------------------------------------------

  private clearResults(): void {
    this.errorMessage.set('');
    this.files.set(null);
  }

  private resetProgress(): void {
    this.clearResults();
    this.percent.set(0);
    this.stateText.set('');
    this.logLines.set([]);
  }

  private resetAll(): void {
    this.resetProgress();
    this.selected.set(null);
    this.filter.set('');
    this.quality.set('standard');
    this.jobId = null;
  }

  // ---- misc -------------------------------------------------------------------------------------

  // Result URLs from the server start with /medias/..., which sits under the app's path base.
  fileUrl(url: string): string {
    return `${this.basePrefix}${url}`;
  }

  // crypto.randomUUID() only exists in secure contexts (https or localhost), and this app is also
  // served over plain http on the LAN, so build the id from getRandomValues instead.
  private newJobId(): string {
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  }
}
