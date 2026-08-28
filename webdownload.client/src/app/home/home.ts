import { Component, inject } from '@angular/core';
import { SignalrService } from '../services/signalr.service';
import { FormsModule } from '@angular/forms'
import { AsyncPipe } from '@angular/common';
import { downloadInfo } from '../models/webdownload.model';
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

  private outputSubject = new BehaviorSubject<string[]>([]);
  output$ = this.outputSubject.asObservable();

  url: string = '';
  isDownloading = signal(false);  
  options: string = '';
  chkAudio: boolean = false;
  checkAudioChapter: boolean = true;
  selectedAudioFormat: string = 'flac';
  selectedLangFormat: string = 'en.*,km';
  selectedMenuValue: string = "MOVIES";
  chkVideo: boolean = true;
  chKSubTitleInclude: boolean = true;
  chapter = signal<string[]>([]);

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
    } else if (this.chKSubTitleInclude) {
      parts.push(`--sub-langs "${this.selectedLangFormat || 'en'}" --write-subs --write-auto-subs`);
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
  }

  onUrlChange(): void {
    this.resetDownloadStatus();
    this.getTitle();
    if (this.url && this.url.trim() !== '') {
      this.updateAutoOptions();
    }
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
    this.signalRService.startConnection();

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
    });

    // Subscribe to download finished
    this.signalRService.addHandler('ReceiveDownloadFinished', (info: downloadInfo) => {
      this.isDownloading.set(false);
      this.finish = `${info.finishOutput}`;
    });
    this.signalRService.addHandler('ReceiveState', (info: downloadInfo) => {
      this.ReceiveState = `${info.state}`;
    });

    this.signalRService.addHandler('ReceiveFileName', (info: downloadInfo) => {
      this.ReceiveFileName = `${info.fileName}`;
    });

    this.signalRService.addHandler('ReceiveChapterFileName', (info: downloadInfo) => {
      this.chapter.update(current => [...current, `${info.chapter}`]);
    });
  }
  ngOnDestroy(): void {
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
    if (!this.connectionId) {
      this.connectionId = this.signalRService.getConnectionId();
      const payload = {
        downloadId: this.connectionId,
        url: this.url,
      };
      this.signalRService.invokeMethod('HubGetTitleServiceAsync', payload);
    }
  }
  startDownload(): void {
    // Retrieve SignalR connection ID
    this.connectionId = this.signalRService.getConnectionId();
    if (!this.connectionId) {
      this.connectionId = this.signalRService.getConnectionId();
    }
    const payload = {
      downloadId: this.connectionId,
      url: this.url,
      options: this.options,
      audioOnly: this.chkAudio,
      audioFormat: this.selectedAudioFormat,
      audioChapter: this.checkAudioChapter,
      videoOnly: this.chkVideo,
      subTitle: this.chKSubTitleInclude,
      subTitleLang: this.selectedLangFormat,
      outputFolder: `${this.selectedMenuValue}\\${this.outputFolder}`  // Send the user-provided output folder.
    };
    this.isDownloading.set(true);
    this.signalRService.invokeMethod('HubStartDownloadServiceAsync', payload);
  }
}

