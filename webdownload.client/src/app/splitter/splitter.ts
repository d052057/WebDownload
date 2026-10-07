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
interface SplitterConfig { menus: MenuOption[]; qualities: QualityOption[]; outputFolder: string; rpmFolder: string; rpmMenu: string; }
interface SplitterTree { menu: string; fileCount: number; folders: MediaFolderTreeDto[]; tracks: MediaTrackDto[]; }
interface SearchHit { track: MediaTrackDto; folder: string; }
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

const MAX_SEARCH_RESULTS = 200;

@Component({
  imports: [CommonModule, FormsModule, FolderNodeComponent, SearchBoxComponent],
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

  // Name of the selected menu; set from the first menu the server offers.
  menu = signal('');
  tree = signal<SplitterTree | null>(null);
  treeLoading = signal(false);
  // Text from the shared search box. The box keeps its own text, so this is never cleared from here.
  query = signal('');
  selected = signal<MediaTrackDto | null>(null);
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

  // Header of the folder card: the menu's title, with a count badge like "86 songs".
  menuTitle = computed(() => this.titleOf(this.menu()));
  countLabel = computed(() => {
    const n = this.tree()?.fileCount ?? 0;
    const noun = this.isRpm() ? 'song' : 'file';
    return `${n} ${noun}${n === 1 ? '' : 's'}`;
  });
  // The special menu built from the Rpm tables (its name comes from Splitter:RpmMenu); every other menu is a MediaMenu row.
  isRpm = computed(() => {
    const rpm = this.config()?.rpmMenu;
    return !!rpm && this.menu().toLowerCase() === rpm.toLowerCase();
  });
  folderPath = computed(() =>
    this.isRpm() ? `//medias/${this.config()?.rpmFolder ?? ''}` : `//medias/${this.menu()}`);

  // While something is typed in the search box, matches are shown as one flat list (with their folder
  // path) instead of the collapsed tree, so a hit is never hidden inside a closed folder.
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
    this.apiBase = `${this.basePrefix}/api/Splitter`;
    this.hubUrl = `${this.basePrefix}/splitterHub`;
  }

  ngOnInit(): void {
    this.http.get<SplitterConfig>(`${this.apiBase}/config`).subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        if (cfg.menus.length > 0) {
          this.menu.set(cfg.menus[0].name);
          this.loadTree();
        }
      },
      error: (err) => console.error('Failed to load Splitter settings:', err)
    });
    this.loadDevice(false);
  }

  ngOnDestroy(): void {
    void this.hub?.stop();
  }

  titleOf(menu: string): string {
    return this.config()?.menus.find(m => m.name === menu)?.title ?? menu;
  }

  // ---- tree and hardware -------------------------------------------------------------------

  loadTree(): void {
    this.treeLoading.set(true);
    this.http.get<SplitterTree>(`${this.apiBase}/tree`, { params: { menu: this.menu() } }).subscribe({
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

  // Called by folder-node (any depth) and by the search results.
  select(track: MediaTrackDto): void {
    if (this.isRunning()) return;
    this.selected.set(track);
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

  // ---- running a job -------------------------------------------------------------------------

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

  // ---- reset helpers ---------------------------------------------------------------------------

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
    this.quality.set('standard');
    this.jobId = null;
  }

  // ---- misc ---------------------------------------------------------------------------------------

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
