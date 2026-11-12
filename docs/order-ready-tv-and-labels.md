# Order-ready TV screen, cup labels, here / to go

## Here or to go
Every order now has a type: **To go** (default) or **Here**. The guest picks it in the cart of the customer app (an order
to a table QR code is always Here). The cashier picks it at the till in Yurt POS. It shows on the kitchen screen, the Live
Orders page, the kitchen ticket, the receipt and the cup labels. Orders made before this change count as To go.

## Order-ready TV screen
Open `/tv` (sidebar: **Order-ready screen**, needs the `own_pos` module) in the browser of a TV above the counter, sign in
once with a staff account, pick the branch and press **Full screen**. Two columns: **Preparing** (accepted / being made)
and **Ready**. The number is the same 6-character number the guest sees in the app. A number moves to Ready the moment
staff press Ready on the kitchen screen, with a soft sound (the browser needs one click on the page first). The buttons
hide a few seconds after the mouse stops. The list reloads every 2 minutes in case a live update was missed.

## Cup labels
On **Printers** add a printer of type **Cup labels**. Give it a station (for example Bar) to label only that station's
items; with no station every item gets a label. It prints one small sticker per piece (3 lattes = 3 stickers): number,
Here / To go, `n/total`, drink, size and add-ons, guest note. "Print by itself" works like for kitchen printers (labels
print when an order is accepted). A **Print cup labels** button on every kitchen card prints them by hand. Labels use the
same network, print-agent and Bluetooth paths as other printers.

Kitchen tickets now show the same 6-character number as the app (they used the first 8 characters of the id before).
