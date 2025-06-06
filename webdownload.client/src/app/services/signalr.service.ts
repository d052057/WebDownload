import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  public hubConnection!: signalR.HubConnection;
  private connectionId: string = '';
  private handlers = new Map<string, EventListenerOrEventListenerObject>();
  url!: string;
  //registeredEvents: string[] = [
  //  'ReceiveFileName',
  //  'ReceiveError',
  //  'ReceiveDownloadInfo',
  //  'ReceiveDownloadFinished',
  //  'ReceiveState',
  //  'ReceiveOutput',
  //  'ReceiveChapterFileName',
  //  'ReceiveTotalFragment',
  //  'ReceiveLastDownloadInfo'
  //];
  constructor() {
    this.url = environment.appUrl;
}
  getConnectionId(): string {
    return this.connectionId;
  }
  startConnection(): void {
    let intervalId: any;
      const hubUrl = `${window.location.origin}/webdownload/downloadHub`;
    //const hubUrl = this.url + "/webdownload/downloadHub";
    console.log(`Connecting to SignalR hub at: ${hubUrl}`);
    // Initialize the SignalR connection
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { transport: signalR.HttpTransportType.WebSockets, withCredentials: false }) // Update URL if needed
   
      .configureLogging(signalR.LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    // Start the connection and log results
    this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR Connected');
        // Capture the connection ID from the server
        this.hubConnection.invoke('GetConnectionId')
          .then((id: string) => {
            this.connectionId = id;
            console.log(`Connection ID retrieved: ${this.connectionId}`);
            clearInterval(intervalId); // Stop logging when connected
          })
          .catch(err => console.error('Error retrieving Connection ID:', err));
      })
      .catch(err => {
        console.error('Error while starting SignalR connection:', err);
        intervalId = setTimeout(
          () => this.startConnection()
          , 2000); // Retry after 2 seconds
      });

    this.hubConnection.onreconnecting((error) => {
      console.warn('SignalR reconnecting:', error);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR reconnected successfully.');
    });
  }

  
  async invokeMethod(methodName: string, ...args: any[]): Promise<void> {
    try {
      /* alert("invokeMethod: " + methodName);*/
      return await this.hubConnection.invoke(methodName, ...args);
    } catch (err) {
      console.error(`Error invoking method '${methodName}':`, err);
      throw err;
    }
  }
  addHandler(eventName: string, callback: (...args: any[]) => void): void {
    this.hubConnection.on(eventName, callback);
    this.handlers.set(eventName, callback);
    console.log(`Handler added for event: ${eventName}`);
  }
  unregisterHandlers(): void {
    this.handlers.forEach((callback, eventName) => {
      this.hubConnection.off(eventName);
      console.log(`Handler removed for event: ${eventName}`);
    });
    this.handlers.clear();
  }
}
