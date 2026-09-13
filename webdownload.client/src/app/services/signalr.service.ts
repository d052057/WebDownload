import { ApplicationRef, NgZone, inject, Service } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';

@Service()
export class SignalrService {
  private ngZone = inject(NgZone);
  private appRef = inject(ApplicationRef);

  public hubConnection!: signalR.HubConnection;
  private handlers = new Map<string, (...args: any[]) => void>();
  private startPromise!: Promise<void>;
  private reconnectedCallbacks: Array<() => void> = [];
  url = environment.appUrl;

  getConnectionId(): string {
    return this.hubConnection?.connectionId ?? '';
  }

  async joinGroup(groupId: string): Promise<void> {
    await this.hubConnection.invoke('JoinGroup', groupId);
  }

  onReconnected(callback: () => void): void {
    this.reconnectedCallbacks.push(callback);
  }

  async ensureConnected(): Promise<string> {
    if (this.startPromise) {
      await this.startPromise;
    }
    for (let i = 0; i < 10 && !this.getConnectionId(); i++) {
      await new Promise(r => setTimeout(r, 100));
    }
    return this.getConnectionId();
  }

  startConnection(): Promise<void> {
    const hubUrl = `${window.location.origin}/webdownload/downloadHub`;
    console.log(`Connecting to SignalR hub at: ${hubUrl}`);

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { withCredentials: false })
      .configureLogging(signalR.LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    this.hubConnection.onreconnecting((error) => {
      console.warn('SignalR reconnecting:', error);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR reconnected. New connectionId:', this.getConnectionId());
      this.reconnectedCallbacks.forEach(cb => {
        try { cb(); } catch (err) { console.error('Error in onReconnected callback:', err); }
      });
    });

    this.startPromise = this.hubConnection
      .start()
      .then(() => console.log('SignalR Connected. connectionId:', this.getConnectionId()))
      .catch(err => {
        console.error('Error starting SignalR connection:', err);
        return new Promise<void>(resolve => {
          setTimeout(() => this.startConnection().then(resolve), 2000);
        });
      });

    return this.startPromise;
  }

  async invokeMethod(methodName: string, ...args: any[]): Promise<void> {
    try {
      return await this.hubConnection.invoke(methodName, ...args);
    } catch (err) {
      console.error(`Error invoking method '${methodName}':`, err);
      throw err;
    }
  }

  addHandler(eventName: string, callback: (...args: any[]) => void): void {
    const zoneWrappedCallback = (...args: any[]) => {
      console.log(`[${new Date().toISOString()}] SignalR event received: ${eventName}`, args?.[0]);
      this.ngZone.run(() => {
        callback(...args);
        try { this.appRef.tick(); } catch { }
      });
    };

    this.hubConnection.on(eventName, zoneWrappedCallback);
    this.handlers.set(eventName, zoneWrappedCallback);
    console.log(`Handler added for event: ${eventName}`);
  }

  unregisterHandlers(): void {
    this.handlers.forEach((callback, eventName) => {
      this.hubConnection.off(eventName, callback);
      console.log(`Handler removed for event: ${eventName}`);
    });
    this.handlers.clear();
  }
}
