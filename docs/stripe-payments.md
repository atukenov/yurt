# Online payments with Stripe

_Written 2026-10-27._

Customers can pay an order online in the app: they tap **Pay online** in the cart, Stripe's own payment page opens,
and they pay by card, Apple Pay or Google Pay. The money goes straight into **the cafe's own Stripe account**. Each
cafe is set up on its own on the Platform screen; a cafe that is not set up simply does not show "Pay online", and
its customers keep paying at the counter.

## How it works, in short

1. The customer places the order with **Pay online** chosen. The order is created as usual (unpaid).
2. The app asks the server for a payment page. The server opens a Stripe Checkout page with the cafe's secret key
   and sends the customer there.
3. The customer pays. Stripe sends the cafe's webhook to Yurt (`/api/v1/payments/stripe/webhook/<cafe slug>`). Yurt
   checks Stripe's signature with the cafe's signing secret and marks the order **Paid**.
4. Staff see the paid order in **Orders** and accept it like any other order.
5. If staff **decline** the order (or the customer cancels it), the money goes back to the card automatically. If
   the customer had not paid yet, the payment page is closed so they cannot pay for a declined order.
6. Staff can also give the money back later with **Refund to card (online payment)** on the order.
   A refund made in the Stripe dashboard is copied onto the order too.

Apple Pay and Google Pay appear on Stripe's page by themselves when they are switched on in the cafe's Stripe
dashboard and the customer's phone supports them. No Apple merchant id, certificate or native code is needed for
this. (The unfinished Freedom Pay Apple Pay code was removed.) In the phone apps the payment page opens in the
phone's browser (Safari / Chrome); after paying, the customer comes back to the app and the order shows as paid.

## Test mode first

Only Stripe **test keys** (`sk_test_...`) work until the server owner switches on live payments with the
environment variable `Stripe__AllowLiveKeys=true`. With test keys no real money moves. Test cards:
`4242 4242 4242 4242`, any future date, any CVC. More at https://docs.stripe.com/testing.

A test Stripe account can be made without a company. **Live payments need a company in a country Stripe supports;
Stripe does not accept businesses registered in Kazakhstan** (https://stripe.com/global, checked 2026-10-27). So a
cafe can go live only with a company in, for example, the UAE, the UK, the EU or the US. A local Kazakh provider
(Freedom Pay or Kaspi) can be added later next to Stripe; each cafe then picks one.

## Setting up a cafe (about 10 minutes)

In the cafe's Stripe dashboard (https://dashboard.stripe.com, **Test mode** switched on):

1. **Developers → API keys**: copy the **Secret key** (`sk_test_...`).
2. **Developers → Webhooks → Add endpoint**:
   - Endpoint URL: copy it from Yurt's **Platform → Online payments** (it ends with `/payments/stripe/webhook/<cafe slug>`).
   - Events: `checkout.session.completed`, `checkout.session.expired`, `checkout.session.async_payment_succeeded`,
     `checkout.session.async_payment_failed`, `charge.refunded`.
   - Save, then copy the **Signing secret** (`whsec_...`).
3. **Settings → Payment methods**: make sure Cards, Apple Pay and Google Pay are on.

In Yurt, **Platform** screen, with the cafe selected:

4. **Secrets**: set `Stripe:SecretKey` and `Stripe:WebhookSecret`.
5. **Online payments**: choose `stripe` and press **Save**. The badge shows **Test mode** with test keys.
6. Make sure the **Online payments** module is on, and that the branch offers **Card** (branch dialog, payment
   methods). In the customer app, **Card** now means "pay online".

The first cafe (default) may instead use server-wide environment variables `Stripe__SecretKey` and
`Stripe__WebhookSecret`. Other cafes never use them.

## Where the customer comes back to

After paying (or pressing back) Stripe sends the customer to the order page of the app:

- In the browser: the address the app runs on, if it is one of the allowed addresses (`AllowedOrigins`) or the
  cafe's own domain.
- Otherwise (phone apps): `https://<cafe domain>`, or `Payment__CustomerAppUrl` when the cafe has no domain.

If none of these is set, opening the payment page fails with a clear message, so set `Payment__CustomerAppUrl` on
the server.

## Things to know

- Amounts are sent in tenge (KZT). Stripe's smallest charge is about 0.50 USD, so very small orders can be refused.
- Stripe makes no Kazakh fiscal receipt. With tax receipts on, online orders get one from the cafe's cash register
  like card payments at the counter, and a refund gets a return receipt.
- Stripe fees are charged to the cafe's Stripe account (see https://stripe.com/pricing for the country of the company).
- Not checked yet on real phones: that the payment page opens in the phone's browser and Apple Pay / Google Pay show
  there. Please try it once with test keys on an iPhone and an Android phone.
