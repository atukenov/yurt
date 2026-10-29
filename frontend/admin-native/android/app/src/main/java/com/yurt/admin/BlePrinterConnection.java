package com.yurt.admin;

import android.annotation.SuppressLint;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCallback;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattService;
import android.bluetooth.BluetoothProfile;
import android.bluetooth.BluetoothStatusCodes;
import android.content.Context;
import android.os.Build;
import java.io.IOException;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * A printer on Bluetooth Low Energy, for printers that have no classic Bluetooth. Same services as the Chrome version
 * (PRINTER_SERVICES in yurt-admin/src/app/core/bluetooth/bluetooth-printing.ts).
 */
@SuppressLint("MissingPermission") // The plugin asks for BLUETOOTH_CONNECT before it gets here.
final class BlePrinterConnection implements PrinterConnection {
    static final UUID[] PRINTER_SERVICES = {
        UUID.fromString("000018f0-0000-1000-8000-00805f9b34fb"),
        UUID.fromString("e7810a71-73ae-499d-8c15-faa9aef0c3f2"),
        UUID.fromString("49535343-fe7d-4ae8-8fa9-9fafd205e455"),
        UUID.fromString("6e400001-b5a3-f393-e0a9-e50e24dcca9e"),
    };
    private static final int WANTED_MTU = 247;
    /** Pause between writes when the printer does not confirm each one, so its buffer does not overflow. */
    private static final long UNCONFIRMED_PAUSE_MS = 20;

    private final String name;
    private final LostListener lost;
    private BluetoothGatt gatt;
    private BluetoothGattCharacteristic characteristic;
    private int writeType;
    private volatile int chunk = 20;
    private volatile boolean ready;
    private volatile boolean closed;

    private final CountDownLatch connected = new CountDownLatch(1);
    private final CountDownLatch discovered = new CountDownLatch(1);
    private final CountDownLatch mtuSet = new CountDownLatch(1);
    private volatile CountDownLatch written;
    private volatile int writeStatus;
    private volatile boolean discoveredOk;

    private BlePrinterConnection(String name, LostListener lost) {
        this.name = name;
        this.lost = lost;
    }

    /** Connects and finds the characteristic that takes print data. Blocks for up to about 20 seconds. */
    static BlePrinterConnection open(Context context, BluetoothDevice device, String name, LostListener lost) throws IOException {
        BlePrinterConnection c = new BlePrinterConnection(name, lost);
        try {
            c.connect(context, device);
            return c;
        } catch (IOException e) {
            c.close();
            throw e;
        }
    }

    private void connect(Context context, BluetoothDevice device) throws IOException {
        gatt = device.connectGatt(context, false, callback, BluetoothDevice.TRANSPORT_LE);
        if (gatt == null) throw new IOException("Could not connect to the printer.");
        if (!await(connected, 15_000)) throw new IOException("Could not connect to the printer. Is it on and close to the tablet?");
        if (!gatt.discoverServices() || !await(discovered, 10_000) || !discoveredOk) {
            throw new IOException("Could not read the printer's Bluetooth services.");
        }
        BluetoothGattCharacteristic found = null;
        for (UUID uuid : PRINTER_SERVICES) {
            BluetoothGattService service = gatt.getService(uuid);
            if (service == null) continue;
            found = pickWritable(service);
            if (found != null) break;
        }
        if (found == null) throw new IOException("This device does not look like a Bluetooth printer.");
        characteristic = found;
        writeType = (found.getProperties() & BluetoothGattCharacteristic.PROPERTY_WRITE) != 0
            ? BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT
            : BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE;
        // Bigger packets print faster; a printer that refuses keeps the 20-byte default.
        if (gatt.requestMtu(WANTED_MTU)) await(mtuSet, 3_000);
        ready = true;
    }

    /** The first characteristic that accepts writes, confirmed writes first (same order as the Chrome version). */
    private static BluetoothGattCharacteristic pickWritable(BluetoothGattService service) {
        for (BluetoothGattCharacteristic c : service.getCharacteristics()) {
            if ((c.getProperties() & BluetoothGattCharacteristic.PROPERTY_WRITE) != 0) return c;
        }
        for (BluetoothGattCharacteristic c : service.getCharacteristics()) {
            if ((c.getProperties() & BluetoothGattCharacteristic.PROPERTY_WRITE_NO_RESPONSE) != 0) return c;
        }
        return null;
    }

    @Override
    public String name() {
        return name;
    }

    @Override
    public synchronized void write(byte[] data) throws IOException {
        if (closed || !ready) throw new IOException("The tablet lost the Bluetooth printer.");
        for (int at = 0; at < data.length; at += chunk) {
            byte[] part = java.util.Arrays.copyOfRange(data, at, Math.min(data.length, at + chunk));
            writePart(part);
            if (writeType == BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE) pause();
        }
    }

    @SuppressWarnings("deprecation") // setValue/writeCharacteristic(c) are the only way below Android 13.
    private void writePart(byte[] part) throws IOException {
        written = new CountDownLatch(1);
        writeStatus = -1;
        boolean started;
        if (Build.VERSION.SDK_INT >= 33) {
            started = gatt.writeCharacteristic(characteristic, part, writeType) == BluetoothStatusCodes.SUCCESS;
        } else {
            characteristic.setWriteType(writeType);
            characteristic.setValue(part);
            started = gatt.writeCharacteristic(characteristic);
        }
        if (!started) throw new IOException("The Bluetooth printer did not take the data.");
        if (!await(written, 5_000)) throw new IOException("The Bluetooth printer did not answer.");
        if (writeStatus != BluetoothGatt.GATT_SUCCESS) throw new IOException("The Bluetooth printer refused the data.");
    }

    @Override
    public void close() {
        closed = true;
        if (gatt != null) {
            gatt.disconnect();
            gatt.close();
        }
    }

    private final BluetoothGattCallback callback = new BluetoothGattCallback() {
        @Override
        public void onConnectionStateChange(BluetoothGatt g, int status, int newState) {
            if (newState == BluetoothProfile.STATE_CONNECTED) {
                connected.countDown();
            } else if (newState == BluetoothProfile.STATE_DISCONNECTED) {
                boolean wasReady = ready;
                ready = false;
                CountDownLatch w = written;
                if (w != null) w.countDown();
                if (!closed) {
                    closed = true;
                    g.close();
                    if (wasReady) lost.onLost();
                }
            }
        }

        @Override
        public void onServicesDiscovered(BluetoothGatt g, int status) {
            discoveredOk = status == BluetoothGatt.GATT_SUCCESS;
            discovered.countDown();
        }

        @Override
        public void onMtuChanged(BluetoothGatt g, int mtu, int status) {
            if (status == BluetoothGatt.GATT_SUCCESS) chunk = Math.max(20, Math.min(mtu - 3, 512));
            mtuSet.countDown();
        }

        @Override
        public void onCharacteristicWrite(BluetoothGatt g, BluetoothGattCharacteristic c, int status) {
            writeStatus = status;
            CountDownLatch w = written;
            if (w != null) w.countDown();
        }
    };

    private static boolean await(CountDownLatch latch, long ms) {
        try {
            return latch.await(ms, TimeUnit.MILLISECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return false;
        }
    }

    private static void pause() {
        try {
            Thread.sleep(UNCONFIRMED_PAUSE_MS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }
}
