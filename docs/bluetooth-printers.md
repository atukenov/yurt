# Bluetooth printers (through a tablet in the cafe)

Some cafes use a small Bluetooth receipt printer instead of one on the network. The server cannot reach it, so a tablet
next to the printer does the printing: the admin app on the tablet asks the server for that printer's jobs every 3
seconds and sends them over Bluetooth. It works like the print agent (`docs/print-agent.md`), but on the tablet itself.

## What you need
- A tablet (or a Windows or Mac computer) with **Google Chrome**. Android tablets work best.
  Safari on iPad and iPhone has no Bluetooth for web pages, so an iPad cannot do this.
- A thermal printer with **Bluetooth Low Energy** ("BLE", "Bluetooth 4.0" or newer). Most cheap 58 mm and 80 mm
  printers have it. A printer that only has old "classic" Bluetooth does not show up in the list.
- The `own_pos` module switched on.

## Set it up
1. In the admin app open **Printers** and press **Add printer**. Fill in the name, branch, kind and paper width, and
   tick **Bluetooth, through a tablet in the cafe**. No address is needed.
2. On the tablet next to the printer, open the admin app in Chrome and sign in (any staff account works).
3. Switch the printer on. On **Printers**, press **Connect this tablet** on that printer, pick the printer in Chrome's
   list and press **Pair**. The printer now shows **Connected to this tablet**.
4. Press **Test print**. The page prints within a few seconds.

## Keep it running
- The tablet prints only while the admin app is open on it. It can be on any page (Kitchen, Cashier, Orders).
- Keep the screen on (Android: Settings → Display → Screen timeout, or "Stay awake" in developer options) and keep the
  tablet charging. When Chrome is closed or the tablet sleeps, jobs wait; a job not printed within 15 minutes is dropped.
- After the page reloads, the tablet connects to the printer again by itself where Chrome allows it. If the printer
  shows **Not connected to this tablet**, press **Connect this tablet** again.
- One printer connects to one tablet. A second tablet can have its own Bluetooth printer.

## If it does not print
- The printer is not in Chrome's list: check that it is on, close to the tablet, and not connected to another phone or
  tablet. Turn on Bluetooth and Location on the tablet (Android needs Location for Bluetooth search).
- "This device does not look like a Bluetooth printer": the printer uses a Bluetooth service the app does not know yet.
  Tell us the printer's make and model.
- Letters come out as wrong symbols: change the **Character table** of the printer (17 on most printers).
- The printer stops half way on long receipts: tell us the model; the size of each Bluetooth write may need to go down.

## What to try first on a real printer (not tested on hardware yet)
This was built and tested with a pretend printer only. On the first real printer, please check:
1. The printer shows up in Chrome's list and **Connect this tablet** works.
2. **Test print** comes out, with Russian letters right.
3. A long receipt (an order with many items) prints in full, not cut off.
4. Reload the page: the printer connects again by itself (or tell us it does not).
5. Switch the printer off and on: the page shows **Not connected**, and **Connect this tablet** brings it back.
6. With a cash drawer on the receipt printer, take a cash payment: the drawer opens.
