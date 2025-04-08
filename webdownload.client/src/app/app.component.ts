import { Component, inject, OnInit } from '@angular/core';
import { SignalrService } from './signalr.service';
import { FormsModule } from '@angular/forms'
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  imports: [MatInputModule, FormsModule],
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'webdownload.client';
  signalRService = inject(SignalrService);
  registeredEvents: string[] = this.signalRService.registeredEvents;
  url: string = 'https://www.youtube.com/watch?v=X_fNyURresc';
  isDownloading: boolean = false;
  options: string = '';
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
  startDownload(): void {
    // Retrieve SignalR connection ID
    const connectionId = this.signalRService.getConnectionId();
    if (!connectionId) {
      alert('Connection to SignalR not established yet. Please wait...');
      return;
    }
    const payload = {
      downloadId: connectionId,
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
