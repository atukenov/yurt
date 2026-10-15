# Yurt — Deployment Guide

Covers everything needed to go from a local build to a running production system:
backend API, admin web app, customer mobile app (Capacitor), and optional desktop packaging (Electron).

---

## 1. Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8+ | Backend build & publish |
| SQL Server or PostgreSQL | Any | Production database |
| Node.js | 18+ | Frontend build |
| Angular CLI | 21 | `ng build` commands |
| Java JDK | 17+ | Android Gradle builds |
| Android Studio | Latest stable | Android APK / AAB |
| Xcode | Latest | iOS only (macOS required) |

---

## 2. Backend deployment

### 2.1 Configure production values

Edit `backend/src/Yurt.WebApi/appsettings.json` or (preferred) set environment variables on the host using ASP.NET Core's double-underscore convention:

```
ConnectionStrings__DefaultConnection=Server=prod-sql;Database=YurtDb;User Id=appuser;Password=StrongPass!;TrustServerCertificate=True;
Jwt__Secret=VeryStrongProductionJwtSecretHere
AllowedOrigins__0=https://admin.your-domain.com
AllowedOrigins__1=capacitor://localhost
ASPNETCORE_ENVIRONMENT=Production
```

Minimum required values in `appsettings.json` if not using env vars:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Jwt": {
    "Secret": "VeryStrongProductionJwtSecretHere",
    "Issuer": "yurt-api",
    "Audience": "yurt-clients",
    "ExpiryDays": "7"
  },
  "AllowedOrigins": ["https://admin.your-domain.com", "capacitor://localhost"],
  "AllowedHosts": "*"
}
```

### 2.2 Apply database migrations

```bash
cd backend
dotnet restore
dotnet tool restore
dotnet ef database update --project src/Yurt.Infrastructure --startup-project src/Yurt.WebApi
```

### 2.3 Publish the API

```bash
cd backend
dotnet publish src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish
```

---

### Option A — Linux VPS + Nginx (~$5–$15/month)

Recommended providers: **Hetzner Cloud** ($5/mo), **Vultr** ($5/mo), **Linode** ($5/mo), **Oracle Cloud** (free tier).

1. Install on the server:

```bash
sudo apt update && sudo apt install -y wget apt-transport-https dotnet-runtime-8.0 nginx
```

2. Copy published files:

```bash
scp -r backend/publish/* user@server:/var/www/yurt-api
```

3. Create `/etc/systemd/system/yurt-api.service`:

```ini
[Unit]
Description=Yurt API
After=network.target

[Service]
WorkingDirectory=/var/www/yurt-api
ExecStart=/usr/bin/dotnet /var/www/yurt-api/Yurt.WebApi.dll
Restart=always
RestartSec=10
SyslogIdentifier=yurt-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

4. Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable yurt-api
sudo systemctl start yurt-api
```

5. Configure Nginx as a reverse proxy:

```nginx
server {
    listen 80;
    server_name api.your-domain.com;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo systemctl restart nginx
```

6. Add HTTPS: `sudo certbot --nginx -d api.your-domain.com`

---

### Option B — Railway (managed, PostgreSQL)

Railway is the easiest managed option and provides PostgreSQL natively.

**PostgreSQL detection order** — the backend reads the connection from:
1. `DATABASE_URL` environment variable
2. `RAILWAY_DATABASE_URL` environment variable
3. `ConnectionStrings:DefaultConnection` in `appsettings.json` (local fallback)

Railway provides a Postgres URL (`postgres://user:pass@host:port/db`); the backend converts it to a valid Npgsql connection string automatically.

**Steps:**

1. Create a Railway project, add a **PostgreSQL** plugin.
2. Confirm `DATABASE_URL` appears in the Railway environment variables panel. If Railway uses a different name, also add `RAILWAY_DATABASE_URL` with the same value.
3. Connect your GitHub repo or use the Railway CLI.
4. Set the build command:

```bash
dotnet publish backend/src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish
```

5. Set the start command:

```bash
dotnet backend/publish/Yurt.WebApi.dll
```

6. Add the remaining environment variables:

```
Jwt__Secret=...
Jwt__Issuer=yurt-api
Jwt__Audience=yurt-clients
AllowedOrigins__0=https://admin.your-domain.com
AllowedOrigins__1=capacitor://localhost
ASPNETCORE_ENVIRONMENT=Production
Payment__WebhookCallbackUrl=...
Payment__WebhookSecret=...
Payment__SandboxSecret=...
```

7. Railway exposes an HTTPS endpoint automatically — note the URL and use it in both frontend environment files.

**Migrations with PostgreSQL (Npgsql):**

```bash
cd backend/src/Yurt.Infrastructure
dotnet ef migrations add <Name> --project Yurt.Infrastructure.csproj --startup-project ../Yurt.WebApi/Yurt.WebApi.csproj
dotnet ef database update --project Yurt.Infrastructure.csproj --startup-project ../Yurt.WebApi/Yurt.WebApi.csproj
```

**Local PostgreSQL fallback** — if neither `DATABASE_URL` nor `RAILWAY_DATABASE_URL` is set, update `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=YurtDb;Username=postgres;Password=postgres;Pooling=true;"
}
```

---

### Option C — Other managed platforms (free/low-cost)

| Platform | Notes |
|----------|-------|
| **Render.com** | Free tier for small .NET apps |
| **Fly.io** | Free tier, containerized |
| **Oracle Cloud Free Tier** | Free VM + managed database |
| **Azure App Service B1** | ~$13/month, best .NET ecosystem fit |

For any managed host, prefer environment variables over storing secrets in `appsettings.json`.

---

## 3. Admin frontend deployment

### 3.1 Update the API URL

Edit `frontend/projects/yurt-admin/src/environments/environment.ts` before building:

```ts
export const environment = {
  production: true,
  apiUrl: 'https://api.your-domain.com',
};
```

### 3.2 Build for production

```bash
cd frontend
npm install
ng build yurt-admin --configuration production
```

Output: `frontend/dist/yurt-admin/browser/`

### 3.3 Static hosting (recommended — free)

| Host | Notes |
|------|-------|
| **Netlify / Vercel** | Connect repo or drag-and-drop `dist/`. Add a rewrite rule for Angular routing. |
| **Azure Static Web Apps** | Set `output_location: dist/yurt-admin/browser` in the workflow. |
| **Cloudflare Pages** | Free, global CDN. |
| **GitHub Pages** | Use `angular-cli-ghpages`. |
| **nginx** | `try_files $uri $uri/ /index.html;` in the server block. |

Vercel rewrite (`vercel.json`):

```json
{ "rewrites": [{ "source": "/(.*)", "destination": "/index.html" }] }
```

nginx snippet:

```nginx
server {
    listen 80;
    server_name admin.your-domain.com;
    root /var/www/yurt-admin;
    index index.html;
    location / { try_files $uri $uri/ /index.html; }
}
```

### 3.4 Desktop packaging with Electron (optional)

Use Electron to ship the admin app as a Windows `.exe` or macOS `.dmg`.

1. Create `electron-admin/` at the repo root:

```bash
mkdir electron-admin && cd electron-admin
npm init -y
npm install --save-dev electron electron-builder
```

2. Create `electron-admin/main.js`:

```js
const { app, BrowserWindow } = require('electron');
const path = require('path');

function createWindow() {
  const win = new BrowserWindow({
    width: 1280, height: 800,
    webPreferences: { nodeIntegration: false, contextIsolation: true },
    title: 'Yurt Admin',
  });
  win.loadFile(path.join(__dirname, '../frontend/dist/yurt-admin/browser/index.html'));
}

app.whenReady().then(createWindow);
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
```

> If the app shows a blank screen, enable `HashLocationStrategy` in the admin router config (`useHash: true`).

3. `package.json` build section:

```json
{
  "main": "main.js",
  "scripts": { "start": "electron .", "dist": "electron-builder" },
  "build": {
    "appId": "com.yurt.admin",
    "productName": "Yurt Admin",
    "win": { "target": "nsis" },
    "mac": { "target": "dmg" },
    "files": ["main.js", "../frontend/dist/yurt-admin/**"]
  }
}
```

4. Build the installer: `npm run dist`

---

## 4. Customer mobile app (Capacitor)

### 4.1 Update the API URL

Edit `frontend/projects/yurt-customer/src/environments/environment.ts`:

```ts
export const environment = {
  production: true,
  apiUrl: 'https://api.your-domain.com',
  signalrUrl: 'https://api.your-domain.com',
};
```

Do not use `localhost` — it will not work inside a released mobile app.

### 4.2 Install Capacitor

```bash
cd frontend
npm install @capacitor/core @capacitor/cli @capacitor/android
# iOS only (macOS):
npm install @capacitor/ios
```

### 4.3 Initialise Capacitor (once)

```bash
npx cap init "Yurt" "com.yurt.app" --web-dir=dist/yurt-customer/browser
```

### 4.4 Build the Angular app

```bash
ng build yurt-customer --configuration production
```

### 4.5 Android

```bash
npx cap add android      # first time only
npx cap sync android     # run after every Angular rebuild
npx cap open android     # opens Android Studio
```

In Android Studio: press ▶ for a debug build. For release: **Build → Generate Signed Bundle / APK**, sign with your keystore, then upload the `.aab` to Google Play Console.

### 4.6 iOS (macOS only)

```bash
npx cap add ios
npx cap sync ios
npx cap open ios
```

In Xcode: select target → ▶ for debug, **Product → Archive** for App Store submission via TestFlight / App Store Connect.

### 4.7 Useful Capacitor commands

```bash
npx cap sync          # copy latest web build to all native platforms
npx cap copy android  # copy only (skip plugin updates)
npx cap run android   # build + deploy to connected device in one step
```

### 4.8 Backend CORS for mobile

Add both Capacitor origins to `AllowedOrigins` in the backend config — iOS and Android use
different default WebView origins (`capacitor://localhost` vs `https://localhost`), so both
must be listed or one platform will get CORS errors while the other works fine:

```json
"AllowedOrigins": [
  "https://admin.your-domain.com",
  "capacitor://localhost",
  "https://localhost"
]
```

---

## 5. Budget reference

| Path | Cost |
|------|------|
| Backend on free tier (Render / Railway / Oracle) | $0 |
| Backend on cheap VPS (Hetzner / Vultr) | $5–$15/month |
| Admin frontend (Netlify / Vercel / Cloudflare Pages) | $0 |
| Android APK side-load | $0 |
| Google Play registration | $25 one-time |
| Apple App Store | $99/year |
| **Minimum store-ready launch** | **$25–$30** |

---

## 6. Production checklist

- [ ] `Jwt:Secret` is strong (32+ chars) and not in source control
- [ ] Production DB connection string is set and migrations applied
- [ ] `apiUrl` updated in both Angular environment files before building
- [ ] Deployed frontend origins added to backend `AllowedOrigins`
- [ ] `capacitor://localhost` (iOS) and `https://localhost` (Android) both added to `AllowedOrigins`
- [ ] Backend published in Release mode and running
- [ ] Admin frontend built and deployed to static host
- [ ] Customer app built with correct `apiUrl` then `npx cap sync`
- [ ] Android release APK / AAB signed and distributed
- [ ] HTTPS enabled on backend and admin web host
- [ ] API, admin app, and mobile app smoke-tested against production backend
