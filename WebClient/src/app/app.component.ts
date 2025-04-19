import { Component, OnDestroy, OnInit } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';

interface SfoxMessage {
  sequence: number;
  recipient: string;
  timestamp: number;
  payload: any; // Use 'any' or a more specific interface if you define payload structures
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  private _messages = new BehaviorSubject<SfoxMessage[]>([]);
  private hubConnection!: signalR.HubConnection;
  private destroy$ = new Subject<void>();

  currentFeed: string = 'ticker'; // Default feed
  currentCurrencyPair: string = 'btcusd'; // Default pair
  connectionStatus: string = 'Disconnected';

  availableFeeds: string[] = ['ticker', 'trades', 'orderbook'];
  availableCurrencyPairs: string[] = ['btcusd', 'ethusd', 'ltcusd', 'ethbtc'];

  messages$ = this._messages.asObservable();

  constructor() { }

  ngOnInit(): void {
    this.startConnection();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.stopConnection();
  }

  async startConnection(): Promise<void> {
    this.connectionStatus = 'Connecting...';
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('https://localhost:7087/ws') // Adjust URL and port if necessary
      .configureLogging(signalR.LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    this.hubConnection.start()
      .then(() => {
        this.connectionStatus = 'Connected';
        console.log('SignalR Connection started');
        this.registerHubCallbacks();
      })
      .catch(err => {
        this.connectionStatus = `Connection failed: ${err}`;
        console.error('Error while starting connection: ' + err);
        // Attempt to reconnect automatically is handled by withAutomaticReconnect()
      });

      // Handle connection state changes (optional, for better UI feedback)
      this.hubConnection.onreconnecting(() => {
          this.connectionStatus = 'Reconnecting...';
      });

       this.hubConnection.onreconnected(() => {
          this.connectionStatus = 'Reconnected';
      });

       this.hubConnection.onclose(() => {
           this.connectionStatus = 'Disconnected';
           console.log('SignalR Connection closed');
       });
  }

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop()
        .then(() => {
          this.connectionStatus = 'Disconnected';
          console.log('SignalR Connection stopped');
        })
        .catch(err => console.error('Error while stopping connection: ' + err));
    }
  }

  private registerHubCallbacks(): void {
    this.hubConnection.on('ReceiveMarketData', (data: SfoxMessage) => {
      console.log('Received market data:', data);
      const messages = [data, ... this._messages.value];
      this._messages.next(messages.length > 25? messages.slice(25): messages);
    });

     this.hubConnection.on('ReceiveError', (error: string) => {
        console.error('Received error from hub:', error);
     });
  }

  async subscribeToFeed(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      const feedKey = `${this.currentFeed}.sfox.${this.currentCurrencyPair}`;

      try {
        await this.hubConnection.invoke('Subscribe', feedKey);

        console.log(`Successfully subscribed & joined group: ${feedKey}`);
      } catch (err) {
        console.error(`Subscription failed: ${err}`);
      }
    } else {
      console.warn('SignalR connection not active. Cannot subscribe.');
    }
  }

  async unsubscribeFromFeed(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      const feedKey = `${this.currentFeed}.sfox.${this.currentCurrencyPair}`;

      try {
        await this.hubConnection.invoke('Unsubscribe', feedKey);

        console.log(`Successfully unsubscribed & left group: ${feedKey}`);
      } catch (err) {
        console.error(`Unsubscription failed: ${err}`);
      }
    } else {
      console.warn('SignalR connection not active. Cannot unsubscribe.');
    }
  }

   // Helper to display payload nicely
   getPayloadString(payload: any): string {
       return JSON.stringify(payload, null, 2);
   }
}
