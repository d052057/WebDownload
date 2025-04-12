import { Component, inject, OnInit } from '@angular/core';
import { SignalrService } from './signalr.service';
import { FormsModule } from '@angular/forms'
import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { downloadInfo } from './webdownload.model'
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  imports: [FormsModule, NgFor, AsyncPipe],
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'webdownload.client';
  signalRService = inject(SignalrService);
  registeredEvents: string[] = this.signalRService.registeredEvents;
  url: string = '';
  isDownloading: boolean = false;
  options: string = '-- progress "%(title)s [%(id)s].%(ext)s"\n--no-warnings\n-P movies\\9\n--sub-langs "en"\n--write-subs\n--write-auto-subs "en.*,km"';
  audioOnly: boolean = false;
  subTitle: boolean = true;
  output: string[] = [];
  chapter: string[] = [];
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
      this.output = [...this.output, `${info.output}`];
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
      this.chapter = [...this.chapter, `${info.chapter}`];
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
      audioOnly: this.audioOnly,
      subTitle: this.subTitle,
      outputFolder: this.outputFolder  // Send the user-provided output folder.
    };
    this.isDownloading = true;
    this.signalRService.invokeMethod('HubStartDownloadServiceAsync', payload);
  }
}
