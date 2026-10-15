import { inject, Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { GroupCart, Order } from 'shared-models';
import { AuthStateService } from './auth-state.service';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class SignalrService {
  private hubConnection: signalR.HubConnection | null = null;
  private baseUrl = '';

  readonly orderUpdated$ = new Subject<Order>();
  readonly orderCreated$ = new Subject<Order>();
  readonly orderDeclined$ = new Subject<Order>();
  readonly paymentUpdated$ = new Subject<Order>();
  readonly groupCartUpdated$ = new Subject<GroupCart>();
  readonly etaReminder$ = new Subject<{ orderId: string; minutesLeft: number }>();
  readonly connected = signal(false);

  private auth = inject(AuthStateService);

  configure(baseUrl: string): void {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  async startConnection(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.baseUrl}/hubs/orders`, {
        accessTokenFactory: () => this.auth.token ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on('OrderCreated', (order: Order) => this.orderCreated$.next(order));
    this.hubConnection.on('OrderUpdated', (order: Order) => this.orderUpdated$.next(order));
    this.hubConnection.on('OrderDeclined', (order: Order) => this.orderDeclined$.next(order));
    this.hubConnection.on('PaymentUpdated', (order: Order) => this.paymentUpdated$.next(order));
    this.hubConnection.on('GroupCartUpdated', (dto: GroupCart) => this.groupCartUpdated$.next(dto));
    this.hubConnection.on('EtaReminder', (data: { orderId: string; minutesLeft: number }) => this.etaReminder$.next(data));

    this.hubConnection.onclose(() => this.connected.set(false));
    this.hubConnection.onreconnected(() => this.connected.set(true));

    await this.hubConnection.start();
    this.connected.set(true);
  }

  async subscribeToLocation(locationId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('SubscribeToLocation', locationId);
    }
  }

  async unsubscribeFromLocation(locationId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('UnsubscribeFromLocation', locationId);
    }
  }

  async joinGroupCartRoom(groupCartId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinGroupCart', groupCartId);
    }
  }

  async leaveGroupCartRoom(groupCartId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveGroupCart', groupCartId);
    }
  }

  async stopConnection(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.connected.set(false);
    }
  }
}
