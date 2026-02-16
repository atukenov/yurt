# Deployment Guide — Yurt Coffee

This guide covers deploying the **Next.js frontend** + **NestJS backend** monorepo
on budget-friendly hosting platforms.

---

## Architecture Recap

```
Internet
   │
   ├── yourdomain.com        → Next.js frontend (apps/web)
   └── api.yourdomain.com    → NestJS backend  (apps/backend)
       └── MongoDB Atlas (free tier)
```

---

## 1. Database — MongoDB Atlas (FREE)

**Cost: $0/month** (M0 free tier — 512 MB storage, shared cluster)

1. Go to [mongodb.com/atlas](https://www.mongodb.com/atlas) → Create free account
2. Create a **free M0 cluster** (choose region closest to your server)
3. Create a database user (username + password)
4. Whitelist IP addresses:
   - Add `0.0.0.0/0` to allow all IPs (needed for serverless/PaaS deployments)
5. Get connection string:
   ```
   mongodb+srv://<user>:<password>@cluster0.xxxxx.mongodb.net/yurt?retryWrites=true&w=majority
   ```

---

## 2. Frontend (Next.js) — Vercel (FREE)

**Cost: $0/month** (Hobby plan — unlimited deployments, 100 GB bandwidth)

Vercel is the creator of Next.js — zero config, best compatibility.

### Setup

1. Push your repo to GitHub/GitLab
2. Go to [vercel.com](https://vercel.com) → Import your repository
3. Configure:
   - **Framework Preset**: Next.js (auto-detected)
   - **Root Directory**: `apps/web`
   - **Build Command**: `cd ../.. && pnpm install && pnpm --filter @yurt/web build`
   - **Output Directory**: `.next`
4. Add environment variables:
   ```
   MONGODB_URI=mongodb+srv://...          # Atlas connection string
   NEXTAUTH_SECRET=your-secret-here       # Generate: openssl rand -hex 32
   NEXTAUTH_URL=https://yourdomain.com
   NEXT_PUBLIC_BACKEND_URL=https://api.yourdomain.com
   ```
5. Deploy → Vercel gives you a `*.vercel.app` URL free, or connect a custom domain

### Custom Domain (optional)

- In Vercel dashboard → Settings → Domains → Add `yourdomain.com`
- Update DNS: add CNAME record pointing to `cname.vercel-dns.com`
- SSL is automatic and free

---

## 3. Backend (NestJS) — Options (cheapest first)

### Option A: Railway ($5/month)

**Cost: ~$5/month** (Hobby plan — $5 credit, 8 GB RAM, 8 vCPU)

Railway is the easiest PaaS for Node.js/Docker backends.

1. Go to [railway.app](https://railway.app) → Create project
2. Connect your GitHub repo
3. Configure:
   - **Root Directory**: `/` (Railway builds from Dockerfile)
   - **Dockerfile Path**: `apps/backend/Dockerfile`
4. Add environment variables:
   ```
   MONGODB_URI=mongodb+srv://...
   NEXTAUTH_SECRET=your-secret-here        # Must match frontend!
   FRONTEND_URL=https://yourdomain.com
   PORT=4000
   NODE_ENV=production
   THROTTLE_TTL=60000
   THROTTLE_LIMIT=100
   ```
5. Railway auto-assigns a URL like `*.up.railway.app`
6. Custom domain: Settings → add `api.yourdomain.com`, update DNS

### Option B: Render (FREE with limits)

**Cost: $0/month** (free tier — 750 hrs/month, spins down after 15 min inactivity)

> ⚠️ Free tier sleeps after inactivity. First request after sleep takes ~30s.
> Good for development/staging, not ideal for production.

1. Go to [render.com](https://render.com) → New Web Service
2. Connect GitHub repo
3. Configure:
   - **Root Directory**: `.`
   - **Runtime**: Docker
   - **Dockerfile Path**: `apps/backend/Dockerfile`
4. Add same environment variables as above
5. Free `.onrender.com` URL, or add custom domain

### Option C: Fly.io ($0–5/month)

**Cost: $0–3/month** (3 shared VMs free, pay for extra resources)

1. Install flyctl: `brew install flyctl`
2. From repo root:
   ```bash
   cd apps/backend
   fly launch --dockerfile Dockerfile
   ```
3. Set secrets:
   ```bash
   fly secrets set MONGODB_URI="mongodb+srv://..."
   fly secrets set NEXTAUTH_SECRET="your-secret-here"
   fly secrets set FRONTEND_URL="https://yourdomain.com"
   fly secrets set NODE_ENV="production"
   fly secrets set PORT="4000"
   ```
4. Deploy: `fly deploy`

### Option D: VPS — Hetzner/DigitalOcean ($4–6/month)

**Cost: €3.79/month** (Hetzner CX22) or **$4/month** (DigitalOcean Basic)

Best value if you want full control. Run both backend + MongoDB on one server.

1. Provision a VPS (Ubuntu 22.04, 2 GB RAM minimum)
2. SSH in and install Docker:
   ```bash
   curl -fsSL https://get.docker.com | sh
   sudo apt install docker-compose-plugin
   ```
3. Clone your repo and create `.env`:
   ```bash
   git clone https://github.com/you/yurt.git
   cd yurt
   cp apps/backend/.env.example apps/backend/.env
   # Edit .env with production values
   ```
4. Run with docker-compose:
   ```bash
   docker compose up -d
   ```
5. Set up nginx reverse proxy + free SSL with Certbot:

   ```bash
   sudo apt install nginx certbot python3-certbot-nginx
   ```

   Nginx config for `/etc/nginx/sites-available/api`:

   ```nginx
   server {
       server_name api.yourdomain.com;

       location / {
           proxy_pass http://localhost:4000;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
       }
   }
   ```

   Enable and get SSL:

   ```bash
   sudo ln -s /etc/nginx/sites-available/api /etc/nginx/sites-enabled/
   sudo certbot --nginx -d api.yourdomain.com
   sudo systemctl reload nginx
   ```

---

## 4. Cost Comparison

| Setup                        | Frontend    | Backend     | Database   | Total      |
| ---------------------------- | ----------- | ----------- | ---------- | ---------- |
| **Cheapest** (dev/staging)   | Vercel free | Render free | Atlas free | **$0/mo**  |
| **Recommended** (production) | Vercel free | Railway $5  | Atlas free | **$5/mo**  |
| **Full control** (VPS)       | Vercel free | Hetzner €4  | On VPS     | **~$4/mo** |
| **All-in-one VPS**           | On VPS      | On VPS      | On VPS     | **~$4/mo** |

---

## 5. Recommended Production Setup

For a real coffee shop, the best budget setup is:

| Component | Platform                    | Cost      |
| --------- | --------------------------- | --------- |
| Frontend  | **Vercel** (free)           | $0        |
| Backend   | **Railway** (hobby)         | $5        |
| Database  | **MongoDB Atlas** (M0 free) | $0        |
| Domain    | Namecheap / Cloudflare      | ~$10/year |

**Total: ~$5/month + $10/year for domain**

### Environment variables summary

**Frontend (Vercel):**

```
MONGODB_URI=mongodb+srv://user:pass@cluster.mongodb.net/yurt
NEXTAUTH_SECRET=<same-secret-as-backend>
NEXTAUTH_URL=https://yourdomain.com
NEXT_PUBLIC_BACKEND_URL=https://api.yourdomain.com
```

**Backend (Railway):**

```
MONGODB_URI=mongodb+srv://user:pass@cluster.mongodb.net/yurt
NEXTAUTH_SECRET=<same-secret-as-frontend>
FRONTEND_URL=https://yourdomain.com
PORT=4000
NODE_ENV=production
THROTTLE_TTL=60000
THROTTLE_LIMIT=100
```

> ⚠️ **CRITICAL**: `NEXTAUTH_SECRET` must be identical on both frontend and backend.
> The backend decrypts the JWE tokens created by the frontend using this shared secret.

---

## 6. CORS Configuration

The backend CORS is configured in `apps/backend/src/main/main.ts` using `FRONTEND_URL`.
Make sure `FRONTEND_URL` matches your actual frontend domain exactly (including `https://`).

---

## 7. Deployment Checklist

- [ ] MongoDB Atlas cluster created, connection string obtained
- [ ] Generate a strong `NEXTAUTH_SECRET`: `openssl rand -hex 32`
- [ ] Frontend deployed to Vercel with correct env vars
- [ ] Backend deployed to Railway/Render/Fly with correct env vars
- [ ] `NEXTAUTH_SECRET` is identical on both services
- [ ] `FRONTEND_URL` in backend matches the actual frontend URL
- [ ] `NEXT_PUBLIC_BACKEND_URL` in frontend matches the actual backend URL
- [ ] Custom domains configured (optional)
- [ ] SSL working on both domains (automatic on Vercel/Railway)
- [ ] Test: login, browse menu, place an order, check admin dashboard

---

## 8. Mobile App — PWA + Capacitor

Your app is already mobile-first (responsive design). To make it installable
from the App Store and Play Market, there are two approaches:

### Approach A: PWA (Progressive Web App) — FREE, no store listing

Already configured in this project. Users can "Add to Home Screen" from
their mobile browser. The app works offline-capable and feels native.

- **Android**: Chrome → ⋮ menu → "Add to Home Screen" → installs as an app
- **iOS**: Safari → Share → "Add to Home Screen" → installs as an app

No App Store or Play Market submission needed. Free. Instant updates.

### Approach B: Capacitor (Native wrapper) — for App Store / Play Market

[Capacitor](https://capacitorjs.com/) wraps your Next.js web app in a native
WebView shell, giving you a real `.ipa` (iOS) and `.apk` (Android) file.

**Setup steps:**

```bash
cd apps/web

# Install Capacitor
pnpm add @capacitor/core @capacitor/cli
pnpm add -D @capacitor/ios @capacitor/android

# Initialize
npx cap init "Yurt Coffee" "com.yurt.coffee"

# Build the Next.js app as static export
# (requires output: 'export' in next.config.ts)
pnpm build

# Add platforms
npx cap add ios
npx cap add android

# Sync web build to native projects
npx cap sync

# Open in native IDEs
npx cap open ios      # Opens Xcode
npx cap open android  # Opens Android Studio
```

**Requirements for store submission:**

- **App Store** (iOS): Apple Developer account — $99/year, need a Mac + Xcode
- **Play Market** (Android): Google Play Developer account — $25 one-time

**Cost: $25 (Android only) or $124 (both platforms)**

### Recommendation

Start with **PWA** (free, already set up). If your customers specifically
request a store listing, add Capacitor later — the web code stays the same,
you just wrap it in a native shell.
