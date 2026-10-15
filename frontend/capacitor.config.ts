import type { CapacitorConfig } from '@capacitor/cli';

// Defaults describe the Yurt platform app. A cafe's branded build overrides them from the
// environment (see docs/runbook.md, "Branded app builds"):
//   YURT_APP_ID=kz.mycafe.app YURT_APP_NAME="My Cafe" npx cap sync
const config: CapacitorConfig = {
  appId: process.env['YURT_APP_ID'] ?? 'com.yurt.app',
  appName: process.env['YURT_APP_NAME'] ?? 'Yurt',
  webDir: 'dist/yurt-customer/browser'
};

export default config;
