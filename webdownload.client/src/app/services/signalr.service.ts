import { ApplicationRef, Injectable, NgZone } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  public hubConnection!: signalR.HubConnection;
  private handlers = new Map<string, EventListenerOrEventListenerObject>();
  private startPromise!: Promise<void>;
  private reconnectedCallbacks: Array<() => void> = [];
  url!: string;

  constructor(private ngZone: NgZone, private appRef: ApplicationRef) {
    this.url = environment.appUrl;
  }

  // SignalR's JS client populates hubConnection.connectionId synchronously
  // once start() resolves - no extra server round-trip needed, so there's
  // nothing here that can race with the page loading.
  getConnectionId(): string {
    return this.hubConnection?.connectionId ?? '';
  }

  // Joins a stable, app-generated group (see Home's downloadGroupId). Call
  // this once after connecting, and again from an onReconnected callback -
  // the transport connectionId changes on every reconnect, but messages
  // sent to Clients.Group(groupId) server-side will keep reaching whichever
  // connection most recently joined that group.
  async joinGroup(groupId: string): Promise<void> {
    await this.hubConnection.invoke('JoinGroup', groupId);
  }

  // Register a callback to run every time the connection is re-established
  // after a drop (e.g. to rejoin a group). Not the same as onreconnected
  // logging below - this lets components hook in without touching this
  // service's internals.
  onReconnected(callback: () => void): void {
    this.reconnectedCallbacks.push(callback);
  }

  // Await this before invoking any hub method from a component. Resolves
  // once the connection is actually open (and again after a reconnect).
  async ensureConnected(): Promise<string> {
    if (this.startPromise) {
      await this.startPromise;
    }
    // In the rare case start() resolved but connectionId hasn't been
    // assigned on this tick yet, give it a couple of short retries.
    for (let i = 0; i < 10 && !this.getConnectionId(); i++) {
      await new Promise(r => setTimeout(r, 100));
    }
    return this.getConnectionId();
  }

  startConnection(): Promise<void> {
    const hubUrl = `${window.location.origin}/webdownload/downloadHub`;
    console.log(`Connecting to SignalR hub at: ${hubUrl}`);
    // Initialize the SignalR connection
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { withCredentials: false }) // let SignalR negotiate and fall back if WS upgrade is blocked by the proxy
      .configureLogging(signalR.LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    this.hubConnection.onreconnecting((error) => {
      console.warn('SignalR reconnecting:', error);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR reconnected successfully. New connectionId:', this.getConnectionId());
      this.reconnectedCallbacks.forEach(cb => {
        try {
          cb();
        } catch (err) {
          console.error('Error in onReconnected callback:', err);
        }
      });
    });

    this.startPromise = this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR Connected. connectionId:', this.getConnectionId());
      })
      .catch(err => {
        console.error('Error while starting SignalR connection:', err);
        return new Promise<void>(resolve => {
          setTimeout(() => {
            this.startConnection().then(resolve);
          }, 2000);
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
        // Belt-and-suspenders: force a render pass right now regardless of
        // whether zone.js noticed this task. Harmless if it was already
        // going to render on its own.
        try {
          this.appRef.tick();
        } catch {
          // tick() throws if a CD pass is already in progress; safe to ignore.
        }
      });
    };
    this.hubConnection.on(eventName, zoneWrappedCallback);
    this.handlers.set(eventName, zoneWrappedCallback);
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
