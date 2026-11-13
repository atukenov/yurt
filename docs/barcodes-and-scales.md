# Barcode scanners and scales

Yurt POS (the till) and the admin app work with an ordinary barcode scanner and a shop scale. Nothing here is needed to
sell: without them the cashier taps items and types a weight.

## What you need

- **A barcode scanner** that plugs in by USB or pairs by Bluetooth in *keyboard mode* (most do: it "types" the number and
  presses Enter). No driver or setting in Yurt.
- **A scale** (optional), either
  - a **label scale** that prints barcode stickers with the weight in them (the usual deli counter scale), or
  - a **scale with a USB or serial cable** that sends its weight to the tablet or computer. Chrome or Edge only, on https
    or localhost (Web Serial).

## Setting up the menu (admin app)

1. **Menu → edit a dish → Barcode.** Click in the field and scan the pack, or type the number. A barcode belongs to one
   dish in a cafe. Letters, digits and `- . _` only.
2. **Sold by weight.** Tick it for a dish sold by weight (cheese, sweets, coffee beans). The **price is for 1 kg**. Such a
   dish has no sizes, can't be a combo, and is sold at the till only: the guest apps hide it and the server refuses an
   app order for it. In the dish's recipe (Stock → Recipes) write what **1 kg** uses; a sale of 350 g uses 0.35 of it.
3. For a dish from a **label scale**, put the scale's **item number (PLU)** into Barcode instead of a pack number.
4. **Stock → Ingredients:** each ingredient has a barcode field (type or scan the supplier's pack). A pack the system
   does not know yet can be linked while receiving, see below.

## At the till

- **Scan** anywhere on the till screen (not while a window is open or on the payment step): a normal dish is added to the
  sale, as if tapped (a dish with sizes or add-ons opens its choice window). The search box also finds a dish by its
  barcode. A code nobody has shows "Unknown barcode" for a few seconds.
- **Scale label:** a barcode sticker from a label scale (`2` + 6-digit item number + 5-digit weight in grams + check
  digit, EAN-13) adds that weight of that dish straight to the sale. The check digit is verified, so a half-read label
  is not taken.
- **Weighing:** tapping a dish sold by weight, or scanning its plain code, opens the weigh window with the price per kg.
  The cashier either types the grams, or uses the scale reading when a scale is connected.
- **Connect a cable scale:** the **Scale** button in the top bar opens Chrome's list of serial devices; pick the scale
  once and the till reconnects by itself after a reload. Pick the **speed (baud)** in the list next to the button before
  connecting (9600 is the usual one; 8 data bits, no parity). The button then shows the live weight. The till takes the
  reading only while it is steady, recent (under 3 seconds) and above zero. Lines such as `ST,GS,   0.350kg`,
  `+000.350 kg`, `350 g` or `W 0.35` are understood; `US` or `?` means "still moving". Typing a weight always wins over
  the scale.
- Each weighing is **its own line** in the sale, with the weight shown (`0.350 kg`); remove it with −. Receipts print
  `0.350 kg Cheese`.
- **Without internet:** everything above works offline. The barcodes and the weighed flag come with the menu the tablet
  keeps, and the sale line is sent with its weight when the internet is back.

## What the server does

`POST /admin/counter-sales` takes `weightGrams` on a line of a dish sold by weight (1 g to 50 kg, quantity 1, no size or
add-ons). The price is worked out on the server: menu price for 1 kg × grams ÷ 1000, rounded to the cent. A line
for a dish that is not sold by weight must not send a weight. Stock and the profit cost use the weight: recipe amounts
are per 1 kg, so a 350 g sale takes 0.35 × the recipe off the ingredients. Counts of whole portions (dish stock) do not
apply to weighed dishes; count their ingredients instead.

## Receiving deliveries

- **Stock → Ingredients → "Delivery arrived":** scan the pack in **Scan a pack**, the ingredient is picked for you. If the
  code is new, pick the ingredient yourself and press **Link … to this ingredient**: the next scan finds it.
- **Suppliers and deliveries → a delivery → Scan a pack:** each scan adds one pack of that ingredient to the delivery
  (the supplier's pack size and price when the supplier lists it, else 1 unit). Scan three packs of milk = three packs.
  Check amounts and prices against the paper invoice before posting, as always.

## Not done yet

- Scanning with the phone camera. The scanner has to be a keyboard-mode device for now.
- Menu import from Excel does not read a barcode column yet.
- Weight in a label that holds the **price** instead of the weight is not read.
- Not tried on real hardware (a scanner, a label scale and a cable scale were not available): the keyboard timing
  (60 ms between keys, 4 characters at least) and the scale line formats follow common devices, so please test with yours
  and tell us what it sends if it does not work.
