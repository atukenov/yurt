# Card payment terminals (Kaspi, Halyk)

Goal: when a guest pays by card at the till, Yurt sends the amount to the bank's card terminal and reads the answer
back, so the cashier does not type the sum twice and the sale is confirmed by the terminal, not by hand.

**Status (this PR):** the safe foundation is built and **off by default**: terminal list per branch, payment states, the
agent contract, a simulator, history, refunds, and the doubtful-payment rule. **Not built yet:** the real Kaspi and Halyk
adapters (they need the banks' integration documents, see "What we need from the banks"), the "Pay by card" button in
the POS, and the agent code. Nothing changes for a cafe until someone adds a terminal in Admin.

## What other Kazakhstan cashier programs do (research, 2026-11-13)

Only two cashier-program help pages were readable (Paloma365 and Poster). They name no protocol or SDK, so the
details below are what those pages show, not the banks' own specifications.

| | Paloma365 | Poster |
|---|---|---|
| Banks | Kaspi, Halyk, Forte, Freedom, Alatau, Alsep, BCC; "Halyk Restaurants" (QR, pairing code) | Kaspi; Halyk (page has no detail) |
| How it connects | Terminal and till on the **same Wi-Fi / router**; the till reaches the terminal by **IP address, port 8080**; "search the network" finds it | Kaspi terminal on the **same Wi-Fi** as the till; terminals named: Kaspi Smart POS, Aisino A80 / A90 / ASD18 |
| Flow | Till sends the amount (rounded to whole tenge), shows "waiting for terminal", closes the receipt on success | Choose Card, press Pay, the amount appears on the terminal |
| Refund | Through the terminal using the original transaction code (RRN at some banks); same-shift refund at some banks is a full cancel only | not described |
| Doubtful case | "Card terminal timed out. Check the device or batch report before retrying." | n/a |
| Conflict | | Kaspi works either in "integration mode" or with Kaspi's own fiscalization, not both ("Payments are blocked") |
| Closing the day | Halyk, Forte, Freedom, Alatau, BCC: "Close shift" from the till; Kaspi and Alsep settle on the terminal | |

Other options seen on the Paloma page: Halyk over **HTTP**, Halyk PAX terminals, Halyk **Tap2Pay** (phone as a
contactless reader), and cloud/QR flows with keys from the bank portal.

**What this means for Yurt:**
1. Bank terminals live on the **cafe's own network**, so a server in the cloud cannot reach them. Same problem as
   network printers, same answer: the **branch agent** (the print agent, same key) talks to the terminal.
2. Each bank has its own protocol, so each gets its own adapter behind one contract (`ICardTerminalAdapter`).
3. Whole tenge only. Kaspi/Halyk fiscalization and Yurt's own tax receipts (`docs/fiscal-receipts-kz.md`) can clash: if
   the terminal also prints the fiscal check, turn off the other one (see the Poster "integration mode" note).

## What we need from the banks (Lev's part)

Not found in public pages, so it must come from the banks / the cafe's acquiring manager:
- **Kaspi:** how to enable "integration mode" on the cafe's terminal, and the protocol/SDK description (Paloma lists "Kaspi v2" and "Kaspi v3", so there are at least two generations). Ask whether Kaspi accepts Yurt as a partner (see the gap list: Kaspi only lists a few cashier programs).
- **Halyk:** the HTTP/LAN protocol description for the terminal model the cafe has (Halyk PAX / HTTP), and a test terminal or test mode.
- For each: terminal model, whether the amount can include tips, how to read the bank transaction number (RRN) for refunds, how to close the day (settlement), and whether the terminal prints its own fiscal check.
Until that is in hand the adapters stay unwritten; a terminal of type `kaspi` or `halyk` can be saved but only through the agent, and the agent needs the protocol code.

## How it works

Branch → **Terminal** (`CardTerminal`: type, address, port, through-agent yes/no, active). A charge is a
**CardTerminalPayment** that moves through these states:

| Status | Meaning | Money |
|---|---|---|
| Pending | waiting for the agent | not charged |
| Sending | the agent has it and talks to the terminal | unknown yet |
| Approved | the terminal said yes | **charged** |
| Declined | the terminal said no | not charged |
| Failed | never reached the terminal (offline, nobody picked it up in 2 min) | not charged |
| Cancelled | staff cancelled before anyone took it | not charged |
| **Unknown** | sent, but no answer in 3 min | **may be charged** |

Safety rules (tested):
- A sale is "paid by card" only when the status is **Approved**. A missing answer is never treated as paid or as failed.
- An **Unknown** payment blocks new charges on that terminal until an Admin looks at the terminal slip and resolves it ("charged" or "not charged"); this is audit logged.
- A refund is only for an Approved sale, once, for the full amount, through the terminal. The terminal's bank number is passed along.
- A late report from the agent can never overwrite a finished or staff-resolved payment.
- Only whole tenge, up to 100,000,000.
- Only the last four digits of the card are stored.

## API

Admin (module `own_pos`; setting up and refunding needs an Admin, starting and watching works for any staff):
- `GET/POST/PUT/DELETE /api/v1/admin/card-terminals` (a terminal with history is switched off, not deleted)
- `POST /api/v1/admin/card-terminals/payments` `{terminalId, amount, orderId?}` → payment (`201`). The till then polls `GET .../payments/{id}` every 1-2 s until the status is not Pending/Sending.
- `POST .../payments/{id}/cancel` (only while Pending), `.../refund`, `.../resolve` `{charged, transactionRef?}`
- `GET .../payments?limit=` history

Agent (same `X-Print-Agent-Key` + `X-Tenant` headers as printing, created in Printers → agents):
- `POST /api/v1/print-agent/terminal/claim` → `[{id, provider, host, port, kind, amount, originalTransactionRef}]` (marks them Sending; one request per terminal at a time)
- `POST /api/v1/print-agent/terminal/{id}/result` `{outcome: approved|declined|failed|unknown, approvalCode?, transactionRef?, cardLast4?, error?}`

Rule for the agent: if the request was sent to the terminal and the answer is lost, report `unknown`, never `failed`. Use `failed` only when it is sure the terminal never got the request.

## Adding a real adapter later

Implement `ICardTerminalAdapter` (server side, only if the server can reach the terminal) or the same steps in the agent: `ChargeAsync(host, port, {reference, amount})` and `RefundAsync(..., originalTransactionRef)`, returning Approved / Declined / Failed / Unknown. Use the payment id as the `reference` so a repeated request after a lost answer is not charged twice. Then wire the till.

## Trying it

`docker-compose` sets `CardTerminals__AllowSimulator=true`. In Admin (API for now) add a terminal of type `simulator` with "through the agent" off. The amount picks the answer: ends in 13 = declined, 77 = offline (failed), 99 = no answer (unknown), anything else = approved. **Never** set this on a live server: the simulator approves fake cards.

## Still to do

1. Real Kaspi / Halyk adapters (waiting on the banks' documents and a test terminal).
2. "Pay by card" in the POS (`docs/yurt-pos.md`): start a payment, show "waiting for terminal", create the counter sale only when Approved, offline behavior (card payments need the terminal and the server).
3. Agent code for the terminals, and an Admin screen to add terminals.
4. Daily settlement ("close shift" on the terminal) next to the cashier's Z report.
