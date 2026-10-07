import { Component, ChangeDetectionStrategy, signal, computed, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { Urlbase } from '../services/urlbase';
import { FolderNodeComponent } from '../folder-node/folder-node.component';
import { SearchBoxComponent } from '../shared/search-box/search-box.component';
import { MediaFolderTreeDto, MediaTrackDto } from '../models/media-folder-tree.model';
import { MenuOption } from '../models/menu.model';

interface QualityOption { id: string; label: string; }
interface VoiceOption { id: string; name: string; defaultPitch: number; }
interface VoiceSwapConfig {
  menus: MenuOption[];
  qualities: QualityOption[];
  models: VoiceOption[];
  outputFolder: string;
  rpmFolder: string;
  rpmMenu: string;
  hubPath: string;      // from ApplicationSettings:VoiceSwapHubPath
  problems: string[];   // setup problems found by the server (Applio or a model file missing)
}
interface VoiceSwapTree { menu: string; fileCount: number; folders: MediaFolderTreeDto[]; tracks: MediaTrackDto[]; }
interface SearchHit { track: MediaTrackDto; folder: string; }
interface OutputFile { label: string; name: string; url?: string | null; }
interface VoiceSwapUpdate {
  jobId: string;
  kind: 'state' | 'log' | 'done' | 'error' | 'cancelled';
  state?: string;
  percent?: number;
  step?: number;
  message?: string;
  files?: OutputFile[];
}

const MAX_SEARCH_RESULTS = 200;
const MIN_PITCH = -24;
const MAX_PITCH = 24;

@Component({
  imports: [CommonModule, FormsModule, FolderNodeComponent, SearchBoxComponent],
  selector: 'app-voice-swap',
  styleUrl: './voice-swap.scss',
  templateUrl: './voice-swap.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoiceSwap implements OnInit, OnDestroy {
  private urlbase = inject(Urlbase);
  private http = inject(HttpClient);

  private readonly basePrefix: string;
  private readonly apiBase: string;

  private hub: signalR.HubConnection | null = null;
  private jobId: string | null = null;

  readonly minPitch = MIN_PITCH;
  readonly maxPitch = MAX_PITCH;
  readonly steps = [
    { n: 1, label: 'Split the song' },
    { n: 2, label: 'Convert the voice' },
    { n: 3, label: 'Mix' }
  ];

  config = signal<VoiceSwapConfig | null>(null);

  menu = signal('');
  tree = signal<VoiceSwapTree | null>(null);
  treeLoading = signal(false);
  query = signal('');
  selected = signal<MediaTrackDto | null>(null);

  quality = signal('standard');
  modelId = signal('');
  pitch = signal<number | null>(0);

  isRunning = signal(false);
  step = signal(0);
  percent = signal(0);
  stateText = signal('');
  logLines = signal<string[]>([]);
  files = signal<OutputFile[] | null>(null);
  errorMessage = signal('');

  problems = computed(() => this.config()?.problems ?? []);
  pitchValid = computed(() => {
    const p = this.pitch();
    return p !== null && Number.isInteger(p) && p >= MIN_PITCH && p <= MAX_PITCH;
  });
  canRun = computed(() =>
    !!this.selected() && !!this.modelId() && this.pitchValid() && this.problems().length === 0 && !this.isRunning());
  canReset = computed(() =>
    this.isRunning() || !!this.selected() || !!this.files() || !!this.errorMessage() ||
    this.logLines().length > 0 || !!this.stateText());
  hint = computed(() => {
    if (this.problems().length > 0) return 'The server is not set up yet (see above).';
    if (!this.selected()) return 'Select a song to enable the button.';
    if (!this.pitchValid()) return `Pitch must be a whole number from ${MIN_PITCH} to ${MAX_PITCH}.`;
    return '';
  });

  menuTitle = computed(() => this.titleOf(this.menu()));
  countLabel = computed(() => {
    const n = this.tree()?.fileCount ?? 0;
    const noun = this.isRpm() ? 'song' : 'file';
    return `${n} ${noun}${n === 1 ? '' : 's'}`;
  });
  isRpm = computed(() => {
    const rpm = this.config()?.rpmMenu;
    return !!rpm && this.menu().toLowerCase() === rpm.toLowerCase();
  });
  folderPath = computed(() =>
    this.isRpm() ? `//medias/${this.config()?.rpmFolder ?? ''}` : `//medias/${this.menu()}`);

  searchResults = computed(() => {
    const q = this.query().trim().toLocaleLowerCase();
    if (!q) return null;

    const hits: SearchHit[] = [];
    let total = 0;
    const visit = (tracks: MediaTrackDto[], path: string) => {
      for (const t of tracks) {
        const haystack = `${t.displayTitle} ${t.fileName} ${t.artist ?? ''} ${path}`.toLocaleLowerCase();
        if (!haystack.includes(q)) continue;
        total++;
        if (hits.length < MAX_SEARCH_RESULTS) hits.push({ track: t, folder: path });
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
    this.apiBase = `${this.basePrefix}/api/VoiceSwap`;
  }

  ngOnInit(): void {
    this.http.get<VoiceSwapConfig>(`${this.apiBase}/config`).subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        if (cfg.models.length > 0) this.setModel(cfg.models[0].id);
        if (cfg.menus.length > 0) {
          this.menu.set(cfg.menus[0].name);
          this.loadTree();
        }
      },
      error: (err) => console.error('Failed to load the Voice Swap settings:', err)
    });
  }

  ngOnDestroy(): void {
    void this.hub?.stop();
  }

  titleOf(menu: string): string {
    return this.config()?.menus.find(m => m.name === menu)?.title ?? menu;
  }

  // Picking a voice also loads that voice's own starting pitch from the server settings.
  setModel(id: string): void {
    this.modelId.set(id);
    const m = this.config()?.models.find(x => x.id === id);
    if (m) this.pitch.set(m.defaultPitch);
  }

  // ---- tree -------------------------------------------------------------------------------------

  loadTree(): void {
    this.treeLoading.set(true);
    this.http.get<VoiceSwapTree>(`${this.apiBase}/tree`, { params: { menu: this.menu() } }).subscribe({
      next: (t) => { this.tree.set(t); this.treeLoading.set(false); },
      error: (err) => {
        console.error('Failed to load the media tree:', err);
        this.tree.set(null);
        this.treeLoading.set(false);
      }
    });
  }

  setMenu(menu: string): void {
    if (menu === this.menu()) return;
    this.menu.set(menu);
    this.tree.set(null);
    this.loadTree();
  }

  select(track: MediaTrackDto): void {
    if (this.isRunning()) return;
    this.selected.set(track);
    this.clearResults();
  }

  // ---- running a job ----------------------------------------------------------------------------

  async start(): Promise<void> {
    const media = this.selected();
    const cfg = this.config();
    if (!media || !cfg || !this.canRun()) return;

    this.resetProgress();
    const jobId = this.newJobId();
    this.jobId = jobId;
    this.isRunning.set(true);
    this.stateText.set('Connecting');

    // Join the job's progress group BEFORE starting the job so no early update is missed.
    try {
      const hub = await this.ensureHub(cfg.hubPath);
      await hub.invoke('JoinGroup', jobId);
    } catch (err) {
      console.error('SignalR connection failed:', err);
      this.fail('Could not open the progress connection. Check that the server is running.');
      return;
    }

    this.stateText.set('Starting');
    this.http.post(`${this.apiBase}/jobs`, {
      jobId,
      modelId: this.modelId(),
      quality: this.quality(),
      pitch: this.pitch(),
      mediaPath: media.url   // relative to the media drive, as built by the server
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
      this.stateText.set('Cancelled');
    }
  }

  private async ensureHub(hubPath: string): Promise<signalR.HubConnection> {
    if (!this.hub) {
      this.hub = new signalR.HubConnectionBuilder()
        .withUrl(`${this.basePrefix}${hubPath}`)
        .withAutomaticReconnect()
        .build();
      this.hub.on('ReceiveVoiceSwapUpdate', (u: VoiceSwapUpdate) => this.handleUpdate(u));
    }
    if (this.hub.state === signalR.HubConnectionState.Disconnected) {
      await this.hub.start();
    }
    return this.hub;
  }

  private handleUpdate(u: VoiceSwapUpdate): void {
    if (u.jobId !== this.jobId) return;

    switch (u.kind) {
      case 'state':
        if (u.state) this.stateText.set(u.state);
        if (u.percent != null) this.percent.set(u.percent);
        if (u.step != null) this.step.set(u.step);
        break;
      case 'log':
        if (u.message) this.logLines.update((lines) => [...lines.slice(-199), u.message!]);
        break;
      case 'done':
        this.files.set(u.files ?? []);
        this.percent.set(100);
        this.step.set(this.steps.length + 1);   // every step shows as done
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
  }

  // ---- reset helpers ------------------------------------------------------------------------------

  private clearResults(): void {
    this.errorMessage.set('');
    this.files.set(null);
  }

  private resetProgress(): void {
    this.clearResults();
    this.percent.set(0);
    this.step.set(0);
    this.stateText.set('');
    this.logLines.set([]);
  }

  private resetAll(): void {
    this.resetProgress();
    this.selected.set(null);
    this.quality.set('standard');
    const first = this.config()?.models[0];
    if (first) this.setModel(first.id);
    this.jobId = null;
  }

  // ---- misc ---------------------------------------------------------------------------------------

  // Result URLs from the server start with /medias/..., which sits under the app's path base.
  fileUrl(url: string): string {
    return `${this.basePrefix}${url}`;
  }

  // crypto.randomUUID() only exists in secure contexts, and this app is also served over plain http on the LAN.
  private newJobId(): string {
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  }
}
