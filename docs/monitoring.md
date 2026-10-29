# Error alerts and uptime check

Two things tell you when the live server has a problem, before a cafe calls you:

| What | Catches | Where it comes from | Cost |
|---|---|---|---|
| **Error alerts** | Something broke inside the server: a crash in a request, a failed payment webhook, a printer job that keeps failing, iiko or Webkassa errors. Also a message every time the server starts, so an unexpected restart is visible. | Built into the backend, sends to a Telegram chat | Free |
| **Uptime check** | The server is down completely, or it is up but cannot reach the database. | An outside service opens `/health/ready` every few minutes | Free plan |

You need both: if the server is down it cannot send its own alerts, and the uptime check does not see errors inside a working server.

## Health addresses

| Address | Answers | Used by |
|---|---|---|
| `/health` | `Healthy` while the server process runs | Smoke test |
| `/health/ready` | `Healthy` (200) when the database answers too, otherwise `Unhealthy` (503) | The uptime check |

Both need no login and skip rate limits. `/health/ready` gives up after 5 seconds.

Optional: Railway can wait for `/health/ready` before it switches traffic to a new build (service **Settings**,
**Healthcheck Path**). Its check is sent with the host name `healthcheck.railway.app`, so if the live server sets
`AllowedHosts`, add that name to it first or every deploy will fail.

## 1. Error alerts in Telegram (about 5 minutes)

1. In Telegram, open **@BotFather**, send `/newbot`, pick a name (for example "Yurt alerts") and a username.
   BotFather replies with a **token** like `123456789:AA...`. Keep it secret.
2. Create a Telegram group (for example "Yurt alerts"), add the new bot to it, and send any message in the group.
3. Open `https://api.telegram.org/bot<token>/getUpdates` in a browser (put your token in place of `<token>`).
   Find `"chat":{"id":-100...` and copy that number, minus sign included. That is the **chat id**.
   (If the list is empty, send another message in the group and reload.)
4. In Railway, open the backend service, **Variables**, and add:
   - `Alerts__TelegramBotToken` = the token
   - `Alerts__TelegramChatId` = the chat id
   - optional `Alerts__ServerName` = `production` (shown in every alert; use `staging` on staging so one chat can serve both)
5. Railway redeploys. Within a minute the group gets **"✅ Yurt server started [production]"**. That proves it works.

What an alert looks like:

```
🔴 Yurt error [production]
Unhandled exception for "POST" "/api/v1/orders"
Cafe: blue-cafe
Request id: 3f9c1a2b7d4e

InvalidOperationException: ...
at Yurt.Application.Features.Orders...

2026-10-28 14:02:11 UTC
```

The request id is the same `CorrelationId` that is in the server logs, so you can search the logs for it.

### What keeps it quiet

- Only errors are sent (log level Error or Fatal), never warnings or normal activity.
- The same error is sent at most once per 10 minutes. Repeats are counted: the next alert says "Happened 12 more time(s) since the last alert".
- At most 20 alerts per hour in total. Anything over that is counted and mentioned in the next alert that goes out.
- Change these with `Alerts__RepeatMinutes`, `Alerts__MaxPerHour`. Turn the start message off with `Alerts__NotifyOnStart=false`.
- Remove either variable to switch alerts off completely.

If Telegram cannot be reached, the server writes a warning to its own log and carries on. Orders are never slowed down
by alerts: they are queued and sent in the background.

## 2. Uptime check (about 5 minutes)

Use any outside uptime service with a free plan. Two options:

- **Better Stack** (betterstack.com, "Uptime"): free plan, checks every few minutes, alerts by email and in their phone app.
- **UptimeRobot** (uptimerobot.com): simplest, has a Telegram alert option, but its free plan is now meant for personal,
  non-commercial use, so for a business use its cheapest paid plan or pick Better Stack.

Read the free plan terms when you sign up; they change.

Set up one monitor:

1. Type: HTTP(s) / keyword. URL: `https://<your-backend-address>/health/ready`.
2. Check every 3 to 5 minutes. Alert when the page does not answer, or the answer is not 200, or does not contain `Healthy`.
3. Send alerts to your email and phone app (and to the same Telegram group if the service supports it).
4. Optional: a second monitor for the customer web app address, so you also know when Vercel or the domain is down.

Test it once: add a wrong address to the monitor, check that you get an alert, then fix it back.

## When an alert comes

1. Open the Railway logs and search for the request id or the error text.
2. If one cafe is affected, check its settings and keys on the Platform page first.
3. If everything is broken right after a deploy, redeploy the previous build (see `runbook.md`, Rollback).
4. "Server started" without a deploy means it crashed and restarted. Look at the logs just before that time.
