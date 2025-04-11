import { Component, inject, OnInit } from '@angular/core';
import { SignalrService } from './signalr.service';
import { FormsModule } from '@angular/forms'

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  imports: [FormsModule],
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'webdownload.client';
  signalRService = inject(SignalrService);
  registeredEvents: string[] = this.signalRService.registeredEvents;
  url: string = 'https://www.youtube.com/watch?v=X_fNyURresc';
  isDownloading: boolean = false;
  options: string = '-- progress "%(title)s [%(id)s].%(ext)s"\n--no-warnings\n-P movies\\9\n--sub-langs "en"\n--write-subs\n--write-auto-subs "en.*,km"';
  audioOnly: boolean = false;
  subTitle: boolean = true;
  output: string[] = [];
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

    // Subscribe to progress messages
    this.signalRService.addHandler('ReceiveProgress', (progress: string) => {
      this.progress = `${progress}`;
    });

    // Subscribe to error messages
    this.signalRService.addHandler('ReceiveError', (error: string) => {
      this.error += `${error}` + "\n\n";
    });

    // Subscribe to download finished
    this.signalRService.addHandler('DownloadFinished', (message: string) => {
      this.isDownloading = false;
      this.finish = `${message}`;
    });
    this.signalRService.addHandler('ReceiveState', (message: string) => {
      this.ReceiveState = `${message}`;
      if (message == 'Success') {
      }
    });
    this.signalRService.addHandler('ReceiveSpeed', (message: string) => {
      this.ReceiveSpeed = `${message}`;
    });
    this.signalRService.addHandler('ReceiveETA', (message: string) => {
      this.ReceiveETA = `${message}`;
    });
    this.signalRService.addHandler('ReceiveTotalSize', (message: string) => {
      this.ReceiveTotalSize = `${message}`;
    });

    this.signalRService.addHandler('ReceiveTotalFragment', (message: string) => {
      this.TotalFragments = `${message}`;
    });
    this.signalRService.addHandler('ReceiveFileName', (message: string) => {
      this.ReceiveFileName = `${message}`;
    });
    this.signalRService.addHandler('ReceiveOutput', (message: string) => {
      this.output = [...this.output, `${message}`];
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
