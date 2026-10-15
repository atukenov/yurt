# Angular Yurt Customer - Notification Setup Guide

## Overview

The notification system has been set up to alert users when their order status changes. It works on:

- **Mobile (iOS/Android)**: Native push notifications via Capacitor Local Notifications
- **Web**: Browser notifications via Web Notification API

## Architecture

### Services Added

1. **NotificationService** (`shared-api/notification.service.ts`)
   - Handles native notifications on mobile
   - Falls back to web notifications in browsers
   - Manages notification permissions

2. **OrderNotificationService** (`shared-api/order-notification.service.ts`)
   - Monitors SignalR events for order updates
   - Automatically sends notifications when:
     - Order is created
     - Order status changes (Pending → Confirmed → Preparing → Ready → Completed)
     - Payment is updated
     - Order is declined

## Mobile Configuration

### Prerequisites

- Capacitor CLI: `npm install -g @capacitor/cli`
- iOS development: Xcode (for iOS)
- Android development: Android Studio (for Android)

### Android Setup

1. **Request Notification Permissions**
   - The app requests notification permissions on first launch
   - Users can grant/deny in system settings

2. **Notification Channel**
   - Notifications use the `order-notifications` channel
   - Channel ID configured in `notification.service.ts`

3. **Build for Android**

   ```bash
   cd frontend
   npm run build
   npx cap add android
   npx cap open android
   ```

4. **In Android Studio:**
   - Connect Android device or use emulator
   - Build and run the app
   - Test notifications via the admin panel

### iOS Setup

1. **Request Notification Permissions**
   - The app requests permissions on first launch
   - Users can grant/deny in system settings

2. **Build for iOS**

   ```bash
   cd frontend
   npm run build
   npx cap add ios
   npx cap open ios
   ```

3. **In Xcode:**
   - Select your team and signing certificate
   - Enable "Push Notifications" capability
   - Connect iOS device or use simulator
   - Build and run the app

## Web Configuration

### Browser Notifications

1. **Permission Request**
   - The app requests browser notification permission on launch
   - Users can grant/deny in browser settings

2. **Supported Browsers**
   - Chrome/Edge: Full support
   - Firefox: Full support
   - Safari: Partial support (requires HTTPS)

## Backend Configuration

Ensure your backend has the following:

1. **SignalR Hub**: `/hubs/orders` endpoint
2. **Order Events**:
   - `OrderCreated`
   - `OrderUpdated`
   - `PaymentUpdated`
   - `OrderDeclined`

3. **CORS Configuration**: Allow your Vercel domain
   ```csharp
   services.AddCors(options =>
   {
       options.AddPolicy("AllowAll", builder =>
       {
           builder
               .WithOrigins("https://yurt-customer-yourdomain.vercel.app")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
       });
   });
   ```

## Testing Notifications

### Local Development

1. Run the Angular dev server:

   ```bash
   cd frontend
   npm start
   ```

2. Open browser console for any errors

3. Grant notification permission when prompted

### Mobile Testing

1. Build and deploy to device/emulator
2. Create an order in the app
3. Change order status in admin panel
4. Verify notification appears on device

## Troubleshooting

### Notifications Not Appearing

**Mobile:**

- Check notification settings in device settings
- Verify app has notification permission
- Check `requestPermissions()` call in console

**Web:**

- Verify browser notification permission is granted
- Check browser console for errors
- Ensure HTTPS on production

### SignalR Connection Issues

- Check backend CORS configuration
- Verify backend is running and accessible
- Check auth token is valid
- Monitor Network tab in DevTools

## Customization

### Change Notification Messages

Edit `order-notification.service.ts`:

```typescript
private getStatusMessage(status: OrderStatus): string {
  const statusMessages: Record<OrderStatus, string> = {
    [OrderStatus.Preparing]: 'Being Prepared', // Customize here
    // ... other statuses
  };
  return statusMessages[status] || status;
}
```

### Notification Sounds

Modify in `notification.service.ts`:

```typescript
sound: 'default'; // Change to custom sound file
```

### Update Notification Throttling

Currently updates are deduplicated to prevent spam. Modify `order-notification.service.ts`:

```typescript
private lastNotifiedOrders = new Map<string, OrderStatus>();
```

## Production Deployment to Vercel

See [VERCEL_DEPLOYMENT.md](./VERCEL_DEPLOYMENT.md) for complete deployment steps.
