# Fiscal receipts in Kazakhstan (own_pos)

_Written 2026-10-21. Rules change often; check the sources at the bottom before going live._

When a cafe stops using iiko, something still has to print the **official tax receipt** (фискальный чек). This page says, in plain words, what the law wants and how Yurt should plug it in.

## What the law wants, in short

- **Every sale needs an official receipt.** Cash, card, Kaspi QR and online payment in the app all count. Only a short list of groups is exempt (banks, religious groups and similar); cafes are not on it.
- **The receipt must come from a registered online cash register (ККМ).** "Online" means each receipt is sent to a **fiscal data operator (ОФД)**, a company approved by the tax office (Kazakhtelecom, Transtelecom, Kaspi and a few others). The ОФД gives the receipt its fiscal mark and passes it to the tax office within about 15 minutes.
- **What is on the receipt:** cafe name and БИН/ИИН, cash register serial and registration number, receipt number, items, prices, total, date and time, fiscal mark, ОФД name, and a QR code the buyer can scan to check it. A cafe that pays VAT also shows the VAT amount and rate (16% from 1 January 2026 under the new Tax Code).
- **Shifts:** a shift must be closed (Z report) at least once every 24 hours. A register may work without internet for up to 72 hours and then must send everything.
- **Fines:** a first miss is a warning; a repeat within a year is 15 to 20 MCI for a small business (about 65,000 to 87,000 tenge in 2026), more for bigger ones. Buyers get a small reward for reporting a missing receipt, so it does get checked.
- **Electronic receipts are fine.** For online orders the buyer can get the receipt as a link or QR instead of paper.

## You do not need to build a cash register

Yurt should **not** become a cash register itself. Certifying one with the tax office is slow and costly. Instead, cafes rent a **cloud cash register** from a provider, and Yurt sends each sale to the provider's API. The provider talks to the ОФД, keeps the register registered, and returns the fiscal mark and QR link.

Providers worth knowing:

| Provider | What it is | Fits Yurt? |
| --- | --- | --- |
| **Webkassa** (webkassa.kz) | The best known cloud cash register, used by many POS systems. Has a partner API (login, open/close shift, send receipt, return, Z report). Moved to the new tax protocol for 2026. | **Yes, first choice.** |
| **ReKassa** (rekassa.kz) | Newer cloud cash register with a partner API (register serial plus partner password). Popular with small shops. | Yes, good second adapter. |
| **Kaspi Kassa** | A switch inside Kaspi Pay. When it is on, every Kaspi payment gets its fiscal receipt automatically. | Covers Kaspi payments only. Yurt just must not send those twice. |
| **CloudPayments online kassa** | Fiscal receipts for online card payments. | Only works with CloudPayments acquiring, which Yurt does not use. Skip. |

Both Webkassa and ReKassa give the full API documentation and a test account to partners on request, so the exact calls are not in this repo yet.

## Recommendation

1. **Add a small "fiscal receipt" slot to own_pos**, the same way `IPosOrderSink` and `ILoyaltyLedger` work: an interface (for example `IFiscalRegistrar` with `OpenShift`, `SendSale`, `SendReturn`, `CloseShift`) with one adapter per provider. Build **Webkassa first**, ReKassa second. _(Both are built now and off by default; neither is verified against the real service yet.)_
2. **Per cafe and per branch settings:**
   - On the cafe: which provider (`none`, `webkassa`, `rekassa`). `none` is the default and keeps today's behaviour.
   - The provider login or API key goes in the cafe's encrypted secrets (the same store as payment keys, see `Secrets__MasterKey` in `docs/runbook.md`).
   - On each branch: the cash register number (one register per branch is the normal case).
   - Per payment method: "send to fiscal" or "skip" (skip Kaspi when the cafe has Kaspi Kassa on, so there are no double receipts).
3. **When to send:** when an order becomes paid (online payment confirmed, or the cashier takes the money). A cancelled paid order sends a return receipt. The fiscal QR link is saved on the order and shown on the printed receipt and in the customer's order screen. _(Done: see "Receipt link for the guest and on paper" in `docs/modules.md`.)_
4. **Never block a sale.** If the provider is down, the order still goes through, the receipt waits in a queue and is retried, and staff see a "not fiscalized yet" badge. The cashier page will not close a shift while receipts are still waiting, and closing a Yurt shift also closes the fiscal shift (Z report).
5. **Cut-over:** while iiko is live, iiko (or the cafe's current register) already prints fiscal receipts. Turn Yurt's fiscal sending on only on the day a cafe leaves iiko, so no sale gets two receipts. This step belongs in `docs/cutover-from-iiko.md`.

## What each cafe must do itself

Yurt cannot do these for the cafe, because the contract is in the cafe's name:

- Sign up with a provider (Webkassa or ReKassa) and an ОФД, and register the cloud cash register with the tax office (the provider usually helps; it can be done on egov.kz).
- Give Yurt the provider login or API key and the register number, entered in the admin settings.
- Decide whether Kaspi Kassa stays on for Kaspi payments.

## Still to check

- Exact Webkassa and ReKassa API calls and test accounts (ask both for partner access).
- Online card payments in the app go through Stripe, which makes no Kazakh fiscal receipt, so online orders still need the cafe's register.
- Item codes (НТИН/ГТИН): not needed for cafe food today, but the receipt format has a field for them.

## Sources

- [Online cash registers in Kazakhstan in 2026 (Uppersetup)](https://uppersetup.com/en/blog/online-cash-registers-in-kazakhstan-in-2026-who-must-use-a)
- [How to install a cash register (egov.kz)](https://egov.kz/cms/en/articles/cash_register)
- [Fiscalization in Kazakhstan, Webkassa and ReKassa setup (Paloma365 POS)](https://dashboard.paloma365.com/docs/en/pos-v2/fiscalization-kz)
- [Kaspi Pay does not always replace an online cash register (Finratings)](https://finratings.kz/news/9037-kaspi-pay-ne-vsegda-zameniaet-onlain-kassu-chto-dolzhny-znat-ip/amp)
- [New Tax Code key changes, VAT 16% (Moore Kazakhstan)](https://kazakhstan.moore-global.com/new-tax-code-key-changes/)
- [CloudPayments online kassa in Kazakhstan (Kapital.kz)](https://kapital.kz/finance/87635/cloudpayments-zapustil-v-kazakhstane-servis-arendy-oblachnykh-kass.html)
