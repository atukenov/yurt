# Deploying Angular Yurt Customer to Vercel

## Pre-Deployment Checklist

- [ ] Backend deployed to Railway at `https://altyncup-production.up.railway.app`
- [ ] Backend CORS configured for your Vercel domain
- [ ] Environment variables updated
- [ ] Build configuration verified
- [ ] Git repository pushed to GitHub

## Step 1: Prepare Your Repository

1. **Ensure clean git state:**

   ```bash
   cd /Users/amakenzi/Desktop/Dev/altyncup
   git status
   git add .
   git commit -m "Add notification system for order updates"
   git push origin main  # or your branch
   ```

2. **Verify build works locally:**
   ```bash
   cd frontend
   npm run build -- --configuration=production
   ```

## Step 2: Create Vercel Account & Project

1. **Go to [vercel.com](https://vercel.com) and sign in**

2. **Create new project:**
   - Click "Add New" → "Project"
   - Import your GitHub repository
   - Select the repository containing your Angular app

## Step 3: Configure Build Settings

When Vercel prompts for build settings:

**Framework Preset:** Other (for custom Angular build)

**Build Command:**

```bash
cd frontend && npm run build -- --configuration=production
```

**Output Directory:**

```
frontend/dist/yurt-customer
```

**Install Command:**

```bash
npm install --legacy-peer-deps
```

## Step 4: Environment Variables

In Vercel dashboard, add these environment variables:

```
ANGULAR_API_URL=https://altyncup-production.up.railway.app
ANGULAR_ENVIRONMENT=production
```

Update your `environment.ts` files:

**frontend/projects/yurt-customer/src/environments/environment.ts:**

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://altyncup-production.up.railway.app',
};
```

## Step 5: Create Vercel Configuration File

**Create `frontend/vercel.json`:**

```json
{
  "buildCommand": "npm run build -- --configuration=production",
  "outputDirectory": "dist/yurt-customer",
  "devCommand": "npm start",
  "framework": "angular",
  "installCommand": "npm install --legacy-peer-deps",
  "env": {
    "ANGULAR_ENVIRONMENT": "production"
  }
}
```

## Step 6: Configure Backend CORS

Update your backend to allow Vercel domain.

**In your `Program.cs`:**

```csharp
var vercelDomain = Environment.GetEnvironmentVariable("VERCEL_DOMAIN")
    ?? "https://yurt-customer.vercel.app";

services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder
            .WithOrigins(vercelDomain, "https://localhost:3000", "http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

app.UseCors("AllowFrontend");
```

Then on Railway, add environment variable:

```
VERCEL_DOMAIN=https://yurt-customer-yourdomain.vercel.app
```

## Step 7: Deploy

1. **Trigger deployment:**
   - Push changes to your main branch
   - Vercel will automatically deploy
   - Monitor build progress in Vercel dashboard

2. **Deployment will take 3-5 minutes**

3. **Get your Vercel URL** from the dashboard
   - Usually: `https://yurt-customer-yourdomain.vercel.app`

## Step 8: Post-Deployment Configuration

### Update Backend CORS

- Replace Vercel domain in `VERCEL_DOMAIN` environment variable on Railway

### Update API Configuration

- If API URL changes, update in environment files

### Test Functionality

1. **Test Authentication:**

   ```bash
   curl https://yurt-customer-yourdomain.vercel.app
   ```

2. **Test API Connection:**
   - Log in to the app
   - Verify API calls reach backend

3. **Test Notifications:**
   - Create an order
   - Change order status in admin panel
   - Verify notifications appear

4. **Test SignalR:**
   - Open DevTools → Network
   - Look for WebSocket connection to `/hubs/orders`

## Step 9: Configure Custom Domain (Optional)

1. **In Vercel Dashboard:**
   - Go to Project Settings → Domains
   - Add custom domain
   - Update DNS records with provided values

2. **Update Backend CORS:**
   ```bash
   VERCEL_DOMAIN=https://yurt-customer.yourdomain.com
   ```

## Deployment Troubleshooting

### Build Fails

**Error: "Cannot find module 'shared-api'"**

- Ensure path aliases in `tsconfig.json` are correct
- Verify shared libraries are referenced in `angular.json`

**Error: "Module not found"**

```bash
cd frontend
npm install --legacy-peer-deps
npm run build
```

### Application Shows Blank Page

1. Check browser console for errors
2. Check Network tab:
   - Verify HTML loads
   - Check for 404 on main.js
   - Check for CORS errors on API calls

3. Check Vercel build logs:
   - Deployment → Logs
   - Look for build or runtime errors

### API Connection Fails

1. **Verify backend is accessible:**

   ```bash
   curl https://altyncup-production.up.railway.app/api/health
   ```

2. **Check CORS configuration:**
   - Backend must allow your Vercel domain
   - Must include credentials if using JWT

3. **Check environment variables:**
   - API URL must match backend domain
   - Verify no trailing slashes

### Notifications Not Working

1. **Check browser console for errors**
2. **Verify SignalR connection:**
   - DevTools → Network → WS
   - Look for `/hubs/orders` connection
3. **Check backend logs** for connection issues

## Monitoring & Debugging

### Enable Vercel Analytics

1. In Project Settings → Analytics
2. Enable Web Analytics
3. Monitor performance and errors

### Monitor Backend Logs

Check Railway dashboard for backend errors related to:

- CORS issues
- SignalR connection failures
- Authentication errors

### Check Application Logs

1. **Browser Console (F12):**
   - Check for JavaScript errors
   - Look for API call failures

2. **Vercel Logs:**
   - Project → Deployments → Select deployment → Logs
   - Check build and runtime logs

## Scaling & Performance

### Frontend Optimization

1. **Enable Caching:**
   - Static assets are cached by default
   - Update `vercel.json` for custom caching

2. **Monitor Performance:**
   - Vercel Analytics shows Core Web Vitals
   - Optimize based on data

3. **Enable Edge Caching:**
   ```json
   {
     "headers": [
       {
         "source": "/dist/**",
         "headers": [
           {
             "key": "cache-control",
             "value": "public, max-age=31536000"
           }
         ]
       }
     ]
   }
   ```

### Backend Optimization

1. **Monitor Railway Resources:**
   - Check CPU and memory usage
   - Scale up if needed

2. **Optimize Database:**
   - Check query performance
   - Add indexes for frequently queried fields

## Next Steps

1. Monitor deployment for 24 hours
2. Test all major user flows
3. Gather user feedback
4. Set up error tracking (Sentry, etc.)
5. Configure monitoring/alerting

## Additional Resources

- [Vercel Angular Docs](https://vercel.com/docs/frameworks/angular)
- [Railway Deployment Guide](https://docs.railway.app)
- [Angular Deployment Guide](https://angular.dev/guide/deployment)
- [Notification Setup](./NOTIFICATION_SETUP.md)
