package com.yurt.admin;

import java.io.IOException;

/** A Bluetooth printer the tablet is connected to: classic Bluetooth (most printers) or Bluetooth Low Energy. */
interface PrinterConnection {
    /** Called once when the printer goes away (switched off, out of range), not when the app closes it. */
    interface LostListener {
        void onLost();
    }

    String name();

    /** Sends ESC/POS bytes; blocks until they are handed to the printer. */
    void write(byte[] data) throws IOException;

    void close();
}
