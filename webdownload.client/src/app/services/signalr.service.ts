import { Injectable, NgZone } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  public hubConnection!: signalR.HubConnection;
  private handlers = new Map<string, EventListenerOrEventListenerObject>();
  private startPromise!: Promise<void>;
  url!: string;

  // @microsoft/signalr deliberately runs its internal connection/transport
  // work outside Angular's zone (it captures Zone.root at construction
  // time). That means server -> client pushes (hubConnection.on callbacks)
  // update your component fields just fine, but Angular never finds out it
  // needs to re-render - the page only catches up on the next unrelated
  // Angular event (a click, a Tab press, etc). That's exactly the "press Tab
  // twice" / "toggle the checkbox and it suddenly appears" symptom. Fix:
  // explicitly re-enter the Angular zone around every pushed callback.
  constructor(private ngZone: NgZone) {
    this.url = environment.appUrl;
  }

  // SignalR's JS client populates hubConnection.connectionId synchronously
  // once start() resolves - no extra server round-trip needed, so there's
  // nothing here that can race with the page loading.
  getConnectionId(): string {
    return this.hubConnection?.connectionId ?? '';
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
      this.ngZone.run(() => callback(...args));
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
