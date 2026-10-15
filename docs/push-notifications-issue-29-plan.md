# FCM/APNs remote push notifications — implementation plan (issue #29)

> Planning doc, not yet implemented. See https://github.com/atukenov/altyncup/issues/29.

## Context

The customer app's only "push notifications" today are `@capacitor/local-notifications`
(`frontend/projects/shared-api/src/lib/notification.service.ts` +
`order-notification.service.ts`), driven client-side by SignalR events
(`signalr.service.ts`). SignalR requires an open socket, so a customer whose app is fully
closed never learns their order was accepted/ready/declined. Issue #29 (High priority)
asks for real remote push: device token registration + a backend FCM/APNs send on order
status change, with the existing local-notification path kept as a fallback.

We don't have a real Firebase project or APNs credentials to test end-to-end with. The
plan wires the feature completely (schema, backend send path, frontend registration) but
ships it **disabled by default** (`Firebase:Enabled = false`), degrading to a no-op, and
documents exactly what a human must supply to turn it on — per the issue's explicit ask.

## Backend

**FCM client**: use the official `FirebaseAdmin` NuGet package (latest stable, currently
3.6.0) rather than hand-rolling OAuth2/JWT signing. It handles token refresh and
multicast send in one call and covers Android + iOS (APNs) through FCM's unified HTTP v1
API, matching the issue's suggested approach. Add `<PackageReference Include="FirebaseAdmin" Version="3.6.0" />`
to `backend/src/Yurt.Infrastructure/Yurt.Infrastructure.csproj`.

**New entity** — `backend/src/Yurt.Domain/Entities/CustomerDeviceToken.cs`:
```csharp
public class CustomerDeviceToken : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public CustomerUser Customer { get; set; } = null!;
}
```
`DevicePlatform { Ios, Android }` added to `backend/src/Yurt.Domain/Enums/Enums.cs`.
Add `ICollection<CustomerDeviceToken> DeviceTokens` to `CustomerUser.cs` (mirrors the
existing `Favorites` collection).

**EF wiring** — mirror the `MenuItemVariantConfiguration` one-to-many + cascade pattern
in `backend/src/Yurt.Infrastructure/Persistence/Configurations/EntityConfigurations.cs`:
`HasOne(e => e.Customer).WithMany(c => c.DeviceTokens).HasForeignKey(...).OnDelete(Cascade)`,
plus a unique index on `Token` (so registering the same device twice upserts instead of
duplicating — important since a device can log out/in as a different customer). Add the
`DbSet<CustomerDeviceToken> CustomerDeviceTokens` property to both
`IApplicationDbContext.cs` and `ApplicationDbContext.cs`, then generate the migration
(`dotnet ef migrations add AddCustomerDeviceTokens`, run from `Yurt.Infrastructure` per
existing convention — timestamp-prefixed filename, matching `AddUserReports` etc.).

**Config** — new `backend/src/Yurt.Application/Features/Push/FirebaseOptions.cs`, styled
like `IikoOptions.cs` (XML-doc'd, `Enabled` flag first):
```csharp
public class FirebaseOptions
{
    public bool Enabled { get; set; } = false;
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Full service-account JSON (Firebase Console → Project Settings → Service
    /// Accounts → Generate new private key). Must come from an env var, never committed.</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;
}
```
Add a `"Firebase": { "Enabled": false, "ProjectId": "", "ServiceAccountJson": "" }`
section to `appsettings.json` (placeholders only, real value via `Firebase__ServiceAccountJson`
env var — same double-underscore convention as `Otp__ApiTokenInstance`).

**Send path** — two new pieces registered in `DependencyInjection.cs`:
1. `IPushNotificationSender` / `FcmPushNotificationSender` (Infrastructure, under a new
   `Yurt.Infrastructure/Push/` folder) — lazily builds a `FirebaseApp` singleton from
   `FirebaseOptions` (only if `Enabled` and `ServiceAccountJson` non-empty; returns a
   clear "not configured" result otherwise, never an unhandled exception), and sends via
   `FirebaseMessaging.SendEachForMulticastAsync`, returning which tokens are dead
   (`NotRegistered`/`InvalidArgument` SDK errors) for pruning. Registered as
   `services.AddSingleton(configuration.GetSection("Firebase").Get<FirebaseOptions>() ?? new FirebaseOptions());`
   + `services.AddSingleton<IPushNotificationSender, FcmPushNotificationSender>();`
2. `IPushNotificationService` / `PushNotificationService` (Application, plain class like
   `ReportService`) — `SendOrderStatusPushAsync(Guid customerUserId, string title, string body, IDictionary<string,string> data, CancellationToken ct)`: loads that customer's
   `CustomerDeviceToken`s, calls the sender, deletes any tokens the sender reports as dead,
   and swallows/logs all exceptions so a push failure can never fail the order-status HTTP
   request that triggered it. Registered `services.AddScoped<PushNotificationService>();`.

**Hook point** — inject `PushNotificationService` into
`backend/src/Yurt.Infrastructure/Hubs/OrdersHubService.cs` (it already resolves
`order.CustomerUserId` for the SignalR group target) and call it from
`NotifyOrderUpdatedAsync` and `NotifyOrderDeclinedAsync` only — these are exactly the
methods `OrderService.AcceptOrderAsync` / `UpdateStatusAsync` / `DeclineOrderAsync` call
on every status transition (`backend/src/Yurt.Application/Features/Orders/Services/OrderService.cs`
lines ~322, ~349, ~376). `NotifyOrderCreatedAsync`/payment events are left alone — the
customer is already looking at the app when they place an order, so a push there is
noise. Notification copy is a short fixed string per status (Russian, matching the
frontend's own default-language fallback — the backend has no persisted per-customer
language preference today, so this is a known simplification, called out in the PR body).

**Device-token endpoints** — extend `AuthController.cs` (same file as `PUT /auth/pin`,
`DELETE /auth/me`) rather than a new controller, since this is customer-account-scoped:
- `POST /api/v1/auth/device-tokens` `{ token, platform }` → upsert by `Token`
  (reassigns `CustomerUserId` if the token already exists under a different customer —
  handles device/account switching), `[Authorize(Policy = "CustomerOnly")]`.
- `DELETE /api/v1/auth/device-tokens/{token}` → best-effort remove, called by the
  frontend on logout so a signed-out device stops receiving another customer's pushes.

**Tests** — `backend/tests/Yurt.UnitTests/PushNotificationServiceTests.cs`, same
NSubstitute + `MockQueryable.NSubstitute` harness as `ReportServiceTests.cs`: verify (a)
`Enabled = false` short-circuits with zero sender calls, (b) a dead-token response from
the sender deletes that `CustomerDeviceToken` row, (c) a sender exception doesn't
propagate.

## Frontend (customer app only — admin app is untouched)

Add `@capacitor/push-notifications` (`^8.1.2`, matching the existing `^8.x` Capacitor
dependency set) to `frontend/package.json`; run `npm install` then `npx cap sync android ios`
to wire the native plugin bridge (this only registers the Capacitor plugin — it does
**not** require real Firebase credentials to build).

New `frontend/projects/shared-api/src/lib/push-notification.service.ts`, structured like
`notification.service.ts` (native-platform guard via `Platform` from `@angular/cdk/platform`):
- `initialize()`: on native platforms only, subscribes to `AuthStateService.user$`. On
  each login (including the refresh-token-at-launch case already handled in `app.ts`) it
  requests permission, calls `PushNotifications.register()`, and on the `registration`
  listener event POSTs the token + platform (`ios`/`android`) to the new
  `YurtApiService.registerDeviceToken()`. On logout (a null emission) it calls
  `YurtApiService.unregisterDeviceToken()` with the last known token, then clears it.
  `registrationError` is logged, not thrown.
- A `pushNotificationReceived` listener is registered but intentionally left as a no-op
  (just logs): when the app is foregrounded, `OrderNotificationService`'s existing
  SignalR-driven local notification already covers this order, so also acting on the raw
  push here would double-notify. When backgrounded/closed, the OS displays the FCM
  `notification` payload itself — no client code runs.

`YurtApiService` additions (same style as `changePin`/`deleteAccount`):
```ts
registerDeviceToken(token: string, platform: 'ios' | 'android'): Observable<void> {
  return this.http.post<void>(`${this.api}/auth/device-tokens`, { token, platform });
}
unregisterDeviceToken(token: string): Observable<void> {
  return this.http.delete<void>(`${this.api}/auth/device-tokens/${encodeURIComponent(token)}`);
}
```

Export the new service from `projects/shared-api/src/public-api.ts`. Wire it into
`frontend/projects/yurt-customer/src/app/app.ts` next to the existing
`this.notifications.initialize()` call: `inject(PushNotificationService)` and call
`.initialize()` in `ngOnInit`.

## Explicitly out of scope / documented as manual follow-up

No real Firebase project exists, so these steps can't be done or tested here — capture
in a `docs/push-notifications-setup.md` when implementing, and call it out in the PR
description:
1. Create a Firebase project, add Android app `com.yurt.app` → download
   `google-services.json` into `frontend/android/app/`, add the Google Services Gradle
   plugin (`frontend/android/build.gradle` + `app/build.gradle`). **Not applied in this
   change** — the Gradle plugin hard-fails the build if the json file is absent, which
   would break every other dev's/CI's build until real credentials exist.
2. Add an iOS app in the same Firebase project → `GoogleService-Info.plist` into the
   Xcode project; enable Push Notifications + Background Modes (remote notifications)
   capabilities in Xcode; upload an APNs Auth Key (.p8) to Firebase Console → Cloud
   Messaging → Apple app config.
3. Generate a Firebase service-account key (Console → Project Settings → Service
   Accounts) and set it as `Firebase__ServiceAccountJson` (and `Firebase__ProjectId`,
   `Firebase__Enabled=true`) in the backend's real environment.

## Verification

- Backend: `dotnet build` on the solution; `dotnet test backend/tests/Yurt.UnitTests` for
  the new `PushNotificationServiceTests`; `dotnet ef migrations add AddCustomerDeviceTokens`
  applies cleanly and `dotnet ef database update` (or the app's own startup migration
  runner, if it has one) succeeds against local Postgres.
- Frontend: `npm run build` for `yurt-customer` (Capacitor plugin resolves, TS compiles);
  confirm `yurt-admin` still builds untouched.
- Manual/native testing of actual push delivery is not possible without real Firebase/APNs
  credentials (flagged above) — this will be called out explicitly in the PR description
  as untestable in this environment, per the issue's own acknowledgment that a human must
  supply those credentials.
