# Checking Webkassa with a test account

Yurt's Webkassa adapter (`backend/src/Yurt.Infrastructure/Fiscal/WebkassaFiscalRegistrar.cs`) was written without
Webkassa's partner documentation, so several details are guesses marked "unverified". This page is how to check them.

## 1. Get a test account and the partner docs (Lev)

1. Write to Webkassa (webkassa.kz, "Партнёрам" / partners, or their support email or phone on the site).
2. Say: you build a POS / ordering system for cafes ("Yurt") and want to connect it to Webkassa through the API as an
   integration partner. Ask for:
   - the API documentation (the partner "API Webkassa" document),
   - a **test account** on the test server `devkkm.webkassa.kz` with at least one **test cash register**,
   - a **partner API key** (`X-API-KEY`), if they give one to partners.
3. You will get a login (usually an email), a password, maybe an API key, and a cash register number (it looks like `SWK00012345`).
4. Upload the PDF of the documentation to the project if you can. Never paste the password in a chat.

## 2. Run the check (on your computer, about one minute)

Needs Node.js (already there for `./scripts/dev.sh`). In the `yurt` folder:

```bash
WEBKASSA_LOGIN='test@login' WEBKASSA_PASSWORD='the password' \
WEBKASSA_API_KEY='the key, or leave this line out' \
WEBKASSA_CASHBOX='SWK...' \
node scripts/webkassa-check.mjs
```

`WEBKASSA_CASHBOX` can be left out; the script then takes the first register Webkassa lists. Add `WEBKASSA_VAT=16` if the
test register is set up for VAT. The script only talks to the test server and refuses the live one.

It logs in, makes two test sales (cash; card plus Kaspi with a discount), repeats one sale to see that no double
receipt is made, makes a return, sends a wrong token on purpose, and closes the shift. Each line prints ✓ or ✗.

It writes `webkassa-check-report.json` in the same folder, with the password, key and tokens hidden. Send that file
to Claude in the project (it is never committed). ✗ lines are expected at first: the answers show what to fix.

## 3. What Claude does with the report

Compare each answer with the guesses in the adapter (paths, operation, payment and tax codes, the token-expired code,
where the receipt number and link come back) and the documentation, fix the adapter and its tests, and run the check
again until every step is ✓. Then the "Not verified yet" note in `docs/modules.md` can go.
