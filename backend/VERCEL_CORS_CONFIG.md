# Backend Configuration for Vercel Deployment

## Update CORS Policy

When your Angular frontend is deployed to Vercel, your backend needs to allow requests from that domain.

### Step 1: Identify Your Vercel Domain

After deployment, your Vercel URL will be one of:

- `https://yurt-customer-[random-id].vercel.app` (Vercel default)
- `https://yurt-customer.yourdomain.com` (if you connect custom domain)

### Step 2: Update Program.cs

**Location:** `backend/src/Yurt.WebApi/Program.cs`

Replace your CORS configuration with:

```csharp
var vercelDomain = Environment.GetEnvironmentVariable("VERCEL_DOMAIN")
    ?? "https://yurt-customer.vercel.app";

var allowedOrigins = new[]
{
    vercelDomain,
    "http://localhost:4200",           // Local development
    "http://localhost:3000",           // Web dev server
    "capacitor://localhost",            // Capacitor mobile
};

services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // IMPORTANT for SignalR & JWT
    });
});

// Later in app configuration:
app.UseCors("AllowFrontend");
```

### Step 3: Update Railway Environment Variables

On Railway dashboard for your backend:

1. **Settings** → **Variables**
2. Add or update:
   ```
   VERCEL_DOMAIN=https://yurt-customer-[your-id].vercel.app
   ```

Replace `[your-id]` with your actual Vercel domain suffix.

### Step 4: Verify SignalR Configuration

Ensure your SignalR hub is properly configured:

```csharp
app.MapHub<OrderHub>("/hubs/orders");
```

**In OrderHub.cs:**

```csharp
public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Verify user is authenticated
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    // ... rest of your hub implementation
}
```

### Step 5: Test CORS & SignalR

1. **Deploy to Railway** (if you made changes)
2. **Test from your Vercel frontend:**
   - Open browser DevTools (F12)
   - Open Network tab, filter by WS (WebSocket)
   - Trigger an action that connects to SignalR
   - You should see a WebSocket connection to `/hubs/orders`

3. **Test API calls:**
   - Open Console tab
   - You shouldn't see CORS errors

### Step 6: Production Secrets

Ensure these are NOT in your code, but in Railway environment:

```
# Railway Variables (NEVER commit to git)
JwtSecret=<your-secret-key>
DatabaseConnectionString=<postgresql-connection>
VERCEL_DOMAIN=https://yurt-customer-yourdomain.vercel.app
```

## Common CORS Issues

### Issue: "Access to XMLHttpRequest blocked by CORS policy"

**Solution:**

```csharp
// Make sure you're using the correct domain
// Check that AllowCredentials() is set for SignalR
builder.AllowCredentials();
```

### Issue: "SignalR connection fails silently"

**Solution:**

1. Check browser Network tab for WebSocket connection
2. Verify CORS headers are present
3. Check backend logs for errors
4. Ensure `AllowCredentials()` is set

### Issue: WebSocket connection hangs

**Solution:**

```csharp
// In SignalR hub configuration:
services.AddSignalR(opts =>
{
    opts.HandshakeTimeout = TimeSpan.FromSeconds(30);
    opts.EnableDetailedErrors = true;  // For debugging
});
```

## Health Check Endpoint

Add a health check endpoint for monitoring:

```csharp
app.MapGet("/api/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
});
```

Test from Vercel:

```bash
curl https://altyncup-production.up.railway.app/api/health
```

## Deployment Checklist

- [ ] CORS policy updated in Program.cs
- [ ] `VERCEL_DOMAIN` environment variable set on Railway
- [ ] Backend deployed to Railway
- [ ] Health check endpoint accessible
- [ ] SignalR hub properly configured
- [ ] Testing domain in environment variable
- [ ] No hardcoded origins (use environment variables)

## Monitoring

### Railway Dashboard

1. **View Logs:**
   - Railway → Project → Logs
   - Search for CORS errors
   - Monitor SignalR connection events

2. **Performance:**
   - Monitor CPU and memory usage
   - Check request/response times

### Browser DevTools

**Network Tab:**

- Filter by "XHR" to see API calls
- Filter by "WS" to see WebSocket (SignalR)
- Check response headers for CORS headers

**Console Tab:**

- Look for CORS errors
- Check for connection failures
- Monitor error logs

## Testing Matrix

| Scenario            | Expected                     | How to Test                                 |
| ------------------- | ---------------------------- | ------------------------------------------- |
| Create Order        | Order appears in list        | Create order via app                        |
| Order Status Update | Notification appears         | Update status in admin, monitor Network tab |
| SignalR Connection  | WebSocket established        | Check Network → WS tab                      |
| API Call            | 200 OK response              | Check Network → XHR tab                     |
| Auth Token          | 401 if invalid, 200 if valid | Check Auth headers                          |

## Next Steps

1. **Update CORS** in your Program.cs
2. **Set VERCEL_DOMAIN** in Railway variables
3. **Deploy** changes to Railway
4. **Verify** health check: `curl https://altyncup-production.up.railway.app/api/health`
5. **Test** from Vercel frontend after deployment

---

Your backend is already at: `https://altyncup-production.up.railway.app`

Just update CORS and environment variables to support your new Vercel domain! 🚀
