package com.yurt.admin;

import android.annotation.SuppressLint;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothSocket;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.UUID;

/**
 * A printer on classic Bluetooth: the "serial port" profile (SPP), which is what most cheap 58 mm and 80 mm receipt
 * printers speak. Chrome cannot reach these, which is why the tablet app exists.
 */
@SuppressLint("MissingPermission") // The plugin asks for BLUETOOTH_CONNECT before it gets here.
final class ClassicPrinterConnection implements PrinterConnection {
    private static final UUID SERIAL_PORT = UUID.fromString("00001101-0000-1000-8000-00805F9B34FB");
    /** Bytes per write; the printer's own flow control sets the pace. */
    private static final int CHUNK = 512;

    private final String name;
    private final BluetoothSocket socket;
    private final OutputStream out;
    private volatile boolean closed;

    private ClassicPrinterConnection(String name, BluetoothSocket socket) throws IOException {
        this.name = name;
        this.socket = socket;
        this.out = socket.getOutputStream();
    }

    /** Connects; blocks for up to about 12 seconds when the printer is off. */
    static ClassicPrinterConnection open(BluetoothDevice device, String name, LostListener lost) throws IOException {
        BluetoothSocket socket;
        try {
            socket = device.createRfcommSocketToServiceRecord(SERIAL_PORT);
            socket.connect();
        } catch (IOException secureFailed) {
            // Some printers only accept the "insecure" connection (no encryption on the link).
            socket = device.createInsecureRfcommSocketToServiceRecord(SERIAL_PORT);
            try {
                socket.connect();
            } catch (IOException e) {
                closeQuietly(socket);
                throw new IOException("Could not connect to the printer. Is it on and close to the tablet?", e);
            }
        }
        ClassicPrinterConnection connection = new ClassicPrinterConnection(name, socket);
        connection.watch(lost);
        return connection;
    }

    @Override
    public String name() {
        return name;
    }

    @Override
    public synchronized void write(byte[] data) throws IOException {
        if (closed) throw new IOException("The tablet lost the Bluetooth printer.");
        for (int at = 0; at < data.length; at += CHUNK) {
            out.write(data, at, Math.min(CHUNK, data.length - at));
        }
        out.flush();
    }

    @Override
    public void close() {
        closed = true;
        closeQuietly(socket);
    }

    /**
     * Classic Bluetooth has no "gone" event: a thread reads what the printer sends back (usually nothing) and the read
     * ends when the link drops.
     */
    private void watch(LostListener lost) {
        Thread reader = new Thread(() -> {
            byte[] buffer = new byte[64];
            try {
                InputStream in = socket.getInputStream();
                while (in.read(buffer) >= 0) {
                    // Status bytes from the printer are not used.
                }
            } catch (IOException ignored) {
                // The link dropped.
            }
            if (!closed) {
                closed = true;
                closeQuietly(socket);
                lost.onLost();
            }
        }, "printer-" + name);
        reader.setDaemon(true);
        reader.start();
    }

    private static void closeQuietly(BluetoothSocket socket) {
        try {
            socket.close();
        } catch (IOException ignored) {
            // Already closed.
        }
    }
}
