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
    return parts.join('\n');
  }

  // Phase 1: always clear the box and rebuild it entirely from the current switches.
  updateAutoOptions(): void {
    this.options = this.buildAutoOptions();
  }

  onUrlChange(): void {
    this.getTitle();
    if (this.url && this.url.trim() !== '') {
      this.updateAutoOptions();
    }
  }

  ngOnInit(): void {
    // Initialize SignalR connection
    this.signalRService.startConnection();

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

