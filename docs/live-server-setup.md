# Finish the live server setup

Four things only you can do, because they need your Railway and admin logins. Each one takes a few minutes.
Do them in this order. Today the live API runs on Railway (`altyncup-production.up.railway.app`) and the
customer and admin apps run on Vercel (projects `altyncup` and `altyncup-admin`).

After each step, open the admin panel as the platform owner, go to **Platform → Integrations**, and check the
row turned green ("Configured").

---

## 1. Master key (locks the cafes' secret keys)

**What it is.** Every cafe can store its own secret keys on the Platform screen (Stripe, iiko, push, WhatsApp
codes). The server locks them with one master key. Without it, saving a cafe key fails with
"Secrets:MasterKey is not configured".

**Why it matters.** If you ever lose this key, every stored cafe key becomes unreadable and has to be typed in
again. If someone steals it together with the database, they can read the cafe keys. So: make it once, save a
copy somewhere safe, never change it.

**Steps.**
1. On your computer, open a terminal and run:
   ```bash
   openssl rand -base64 32
   ```
   You get one line of about 44 characters ending in `=`. That line is the key.
2. Save that line in your password manager (1Password, Bitwarden, Apple Passwords...), named
   "Yurt master key – production". This copy is the backup.
3. Railway → project → the API service → **Variables** → **New variable**:
   - Name: `Secrets__MasterKey` (two underscores in the middle)
   - Value: the line from step 1, with no quotes and no spaces
4. Railway redeploys by itself. When it is done, **Platform → Integrations → "Master key for cafe secrets"**
   should say *Configured*. If it still says *Not configured*, the value was not copied whole; the server log
   also prints a warning about it.

---

## 2. Return link after online payment

**What it is.** When a customer pays by card in the **phone app**, Stripe opens a payment page. After paying,
Stripe needs an address to send the customer back to. For cafes with their own web address that address is
used; for the others the server uses this setting.

**Why it matters.** Without it, card payment in the phone app stops with an error before the payment page
opens.

**Steps.**
1. Find the customer app's web address: Vercel → project `altyncup` → **Domains**. Use the main one, for
   example `https://altyncup.vercel.app` or your own domain if you added one.
2. Railway → API service → **Variables** → **New variable**:
   - Name: `Payment__CustomerAppUrl`
   - Value: that address, starting with `https://`, without a `/` at the end
3. After the redeploy, **Platform → Integrations → "Return link after online payment"** should say
   *Configured*.

Nothing else for Stripe is needed on the server now. Each cafe's Stripe keys go on the Platform screen
(see `docs/stripe-payments.md`). Only test keys (`sk_test_...`) work until you add `Stripe__AllowLiveKeys=true`.

---

## 3. Staging: a safe copy of the live system

**What it is.** A second, separate copy of the server and apps with its own empty database. New versions go
there first, you click through them, and only then they go to the live cafes.

**Why it matters.** Today every update goes straight to the live cafe. A broken database change would hit real
customers. Staging costs about 5 USD a month on Railway's Hobby plan plus database usage.

**Steps (Railway).**
1. Open your Railway project → **Environments** (top left) → **New environment** → name it `staging` →
   choose **Duplicate production**. Railway copies the services and their variables.
2. In the `staging` environment, open the **Postgres** service and make sure it is a new, empty database (the
   duplicate makes a new one). Check that the API's `DATABASE_URL` points to *this* Postgres, not production's.
3. In the staging API → **Variables**, change these so they are different from production:
   - `Jwt__Secret`: a new random value (`openssl rand -base64 48`)
   - `Secrets__MasterKey`: a **new** key from `openssl rand -base64 32` (save it as "Yurt master key – staging")
   - `SEED_ADMIN_USERNAME` / `SEED_ADMIN_PASSWORD`: a staging admin login
   - `Otp__Required` = `false` (no real WhatsApp codes on staging)
   - `Webkassa__BaseUrl` = `https://devkkm.webkassa.kz` (tax receipts go to Webkassa's test server)
   - remove `Stripe__AllowLiveKeys` if it is there; staging uses Stripe test keys only
   - `Firebase__Enabled` = `false` unless you want to test pushes on your own phone
4. In the staging API → **Settings → Source**, set the branch to `staging` (create it on GitHub from `main`
   first), so staging only updates when you push to that branch.
5. Open **Settings → Networking → Generate domain** for the staging API. You get an address like
   `altyncup-staging.up.railway.app`.

**Steps (Vercel, the two apps).** The apps have the API address built in, so staging needs its own two Vercel
projects. Tell me the staging API address from step 5 and I will add a staging build setting to the repo; then
you only import the repo twice in Vercel (customer and admin) and pick the `staging` branch.

Once it runs, the smoke test in `docs/runbook.md` (log in, open the menu, place an order, see it in the admin
panel) is how you check every new version before it reaches the cafes.

---

## 4. Put the Instagram link back

**What it is.** The customer app used to have the cafe's Instagram link built into the code. It was moved into
the brand settings so every cafe can have its own, and the old one has to be typed in once.

**Steps.**
1. Admin panel → **Platform** → under **Manage cafe** pick your first cafe.
2. **Brand** section → **Links (Instagram, website, ...)** → **Add link**:
   - Label: `Instagram`
   - Link URL: the full address, for example `https://instagram.com/altyncup`
3. Save. The link shows on the customer app's Profile screen.

---

## Quick check at the end

| Where | What you should see |
|---|---|
| Platform → Integrations | "Master key for cafe secrets" and "Return link after online payment" are green |
| Platform → a cafe → Secrets | saving a test value works (then press Clear) |
| Customer app → Profile | the Instagram link opens your page |
| `https://<staging api>/health` | `Healthy` |
