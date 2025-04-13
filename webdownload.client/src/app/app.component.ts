import { Component, inject, OnInit } from '@angular/core';
import { SignalrService } from './signalr.service';
import { FormsModule } from '@angular/forms'
import { AsyncPipe, NgIf } from '@angular/common';
import { downloadInfo } from './webdownload.model';
import { BehaviorSubject } from 'rxjs';
import { LinebreakPipe } from './linebreak.pipe';
import { signal, effect } from '@angular/core';
import { fadeInOut } from './animations';
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  imports: [FormsModule, NgIf, LinebreakPipe, AsyncPipe],
  animations: [fadeInOut],
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'webdownload.client';
  signalRService = inject(SignalrService);
  registeredEvents: string[] = this.signalRService.registeredEvents;

  private outputSubject = new BehaviorSubject<string[]>([]);
  output$ = this.outputSubject.asObservable();

  url: string = '';
  isDownloading: boolean = false;
  options: string = '-- progress "%(title)s [%(id)s].%(ext)s"\n--no-warnings\n-P movies\\9\n--sub-langs "en"\n--write-subs\n--write-auto-subs "en.*,km"';
  chkAudio: boolean = false;
  checkAudioChapter: boolean = true;
  selectedAudioFormat: string = 'flac';
  selectedLangFormat: string = 'en.*,km';

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
  finish: string = '';
  ReceiveFileName: string = '';
  ReceiveState: string = '';
  outputFolder: string = "movies\\9";
  connectionId!: string;
  ngOnInit(): void {
    // Initialize SignalR connection
    this.signalRService.startConnection();

    this.signalRService.addHandler('ReceiveTotalFragment', (info: downloadInfo) => {
      this.TotalFragments = `${info.frag}`;
    });
    this.signalRService.addHandler('ReceiveOutput', (info: downloadInfo) => {
      const currentOutput = this.outputSubject.value;
      const updatedOutput = [...currentOutput, `${info.output}`];
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
      this.TotalFragments = info.frag;
      this.ReceiveState = info.state;
    });
    // Subscribe to error messages
    this.signalRService.addHandler('ReceiveError', (info: downloadInfo) => {
      this.error += `${info.error}` + "\n\n";
    });

    // Subscribe to download finished
    this.signalRService.addHandler('ReceiveDownloadFinished', (info: downloadInfo) => {
      this.isDownloading = false;
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
    this.signalRService
    this.registeredEvents.forEach(eventName => this.signalRService.hubConnection.off(eventName));

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
      outputFolder: this.outputFolder  // Send the user-provided output folder.
    };
    this.isDownloading = true;
    this.signalRService.invokeMethod('HubStartDownloadServiceAsync', payload);
  }
}
