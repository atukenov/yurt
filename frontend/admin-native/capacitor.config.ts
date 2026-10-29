import type { CapacitorConfig } from '@capacitor/cli';

// The admin app as a tablet app for the cafe (Android tablets and iPad). It exists for Bluetooth receipt printers:
// the native YurtPrinter plugin (android/.../YurtPrinterPlugin.java, ios/App/App/YurtPrinterPlugin.swift) reaches
// classic Bluetooth printers on Android and Bluetooth Low Energy printers on iPad, which Chrome cannot.
// See docs/tablet-app.md. Build: npx ng build yurt-admin && cd admin-native && npx cap sync
const config: CapacitorConfig = {
  appId: process.env['YURT_ADMIN_APP_ID'] ?? 'com.yurt.admin',
  appName: process.env['YURT_ADMIN_APP_NAME'] ?? 'Yurt Admin',
  webDir: '../dist/yurt-admin/browser',
  // The customer app's plugins (push, FCM) need Firebase files; the tablet app uses none of them.
  includePlugins: [],
};

export default config;
