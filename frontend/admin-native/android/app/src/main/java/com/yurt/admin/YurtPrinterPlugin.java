package com.yurt.admin;

import android.Manifest;
import android.annotation.SuppressLint;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.content.Context;
import android.os.Build;
import android.util.Base64;
import androidx.appcompat.app.AlertDialog;
import com.getcapacitor.JSObject;
import com.getcapacitor.PermissionState;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import com.getcapacitor.annotation.Permission;
import com.getcapacitor.annotation.PermissionCallback;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Bluetooth receipt printers for the admin app on an Android tablet. The web side (NativeBluetoothConnector in
 * yurt-admin/src/app/core/bluetooth/native-printer.ts) uses it in place of Chrome's Web Bluetooth.
 *
 * Printers are picked from the devices already paired in the tablet's Bluetooth settings. Most printers speak classic
 * Bluetooth (Chrome cannot); printers that only have Bluetooth Low Energy are reached over BLE.
 */
@SuppressLint("MissingPermission") // Every method checks BLUETOOTH_CONNECT first (see withPermission).
@CapacitorPlugin(
    name = "YurtPrinter",
    permissions = { @Permission(alias = "bluetooth", strings = { Manifest.permission.BLUETOOTH_CONNECT }) }
)
public class YurtPrinterPlugin extends Plugin {
    private final Map<String, PrinterConnection> printers = new ConcurrentHashMap<>();
    private final ExecutorService work = Executors.newCachedThreadPool();

    /** Shows the paired devices; connects the one staff tap. Options: title, cancel, none (texts in the app language). */
    @PluginMethod
    public void pick(PluginCall call) {
        withPermission(call, "pickAllowed");
    }

    @PermissionCallback
    private void pickAllowed(PluginCall call) {
        if (!allowed(call)) return;
        BluetoothAdapter adapter = adapter(call);
        if (adapter == null) return;
        List<BluetoothDevice> devices = new ArrayList<>(adapter.getBondedDevices());
        if (devices.isEmpty()) {
            call.reject(call.getString("none", "No paired Bluetooth devices. Pair the printer in the tablet's Bluetooth settings first."), "NO_DEVICES");
            return;
        }
        String[] labels = new String[devices.size()];
        for (int i = 0; i < devices.size(); i++) labels[i] = labelOf(devices.get(i));
        getActivity().runOnUiThread(() -> {
            final boolean[] chosen = { false };
            new AlertDialog.Builder(getActivity())
                .setTitle(call.getString("title", "Choose the printer"))
                .setItems(labels, (dialog, which) -> {
                    chosen[0] = true;
                    open(call, devices.get(which));
                })
                .setNegativeButton(call.getString("cancel", "Cancel"), (dialog, which) -> dialog.cancel())
                .setOnDismissListener((dialog) -> {
                    if (!chosen[0]) call.reject("Closed without picking a printer.", "CANCELLED");
                })
                .show();
        });
    }

    /** Connects again to a printer picked before (deviceId = its Bluetooth address), without asking staff. */
    @PluginMethod
    public void connect(PluginCall call) {
        withPermission(call, "connectAllowed");
    }

    @PermissionCallback
    private void connectAllowed(PluginCall call) {
        if (!allowed(call)) return;
        BluetoothAdapter adapter = adapter(call);
        if (adapter == null) return;
        String address = call.getString("deviceId", "");
        if (!BluetoothAdapter.checkBluetoothAddress(address)) {
            call.reject("Unknown printer.");
            return;
        }
        open(call, adapter.getRemoteDevice(address));
    }

    /** Sends ESC/POS bytes (base64 in data) to a connected printer. */
    @PluginMethod
    public void write(PluginCall call) {
        PrinterConnection printer = printers.get(call.getString("deviceId", ""));
        if (printer == null) {
            call.reject("The tablet lost the Bluetooth printer.");
            return;
        }
        byte[] data;
        try {
            data = Base64.decode(call.getString("data", ""), Base64.DEFAULT);
        } catch (IllegalArgumentException e) {
            call.reject("The print job is damaged.");
            return;
        }
        work.execute(() -> {
            try {
                printer.write(data);
                call.resolve();
            } catch (IOException e) {
                call.reject(e.getMessage() != null ? e.getMessage() : "Could not print over Bluetooth.");
            }
        });
    }

    @PluginMethod
    public void disconnect(PluginCall call) {
        PrinterConnection printer = printers.remove(call.getString("deviceId", ""));
        if (printer != null) work.execute(printer::close);
        call.resolve();
    }

    @Override
    protected void handleOnDestroy() {
        for (PrinterConnection printer : printers.values()) printer.close();
        printers.clear();
        work.shutdown();
    }

    private void open(PluginCall call, BluetoothDevice device) {
        String address = device.getAddress();
        String name = device.getName() != null ? device.getName() : "Bluetooth printer";
        work.execute(() -> {
            PrinterConnection old = printers.remove(address);
            if (old != null) old.close();
            // Only tell the page about the connection it is using, not an old one that was replaced or closed.
            final PrinterConnection[] self = new PrinterConnection[1];
            PrinterConnection.LostListener lost = () -> {
                if (self[0] == null || !printers.remove(address, self[0])) return;
                JSObject event = new JSObject();
                event.put("deviceId", address);
                notifyListeners("disconnected", event);
            };
            try {
                PrinterConnection printer = connectTo(device, name, lost);
                self[0] = printer;
                printers.put(address, printer);
                JSObject result = new JSObject();
                result.put("deviceId", address);
                result.put("name", name);
                call.resolve(result);
            } catch (IOException e) {
                call.reject(e.getMessage() != null ? e.getMessage() : "Could not connect to the printer.");
            }
        });
    }

    /** Classic Bluetooth first (what most printers speak); BLE for printers that have only that. */
    private PrinterConnection connectTo(BluetoothDevice device, String name, PrinterConnection.LostListener lost) throws IOException {
        int type = device.getType();
        if (type == BluetoothDevice.DEVICE_TYPE_LE) return BlePrinterConnection.open(getContext(), device, name, lost);
        try {
            return ClassicPrinterConnection.open(device, name, lost);
        } catch (IOException classicFailed) {
            if (type == BluetoothDevice.DEVICE_TYPE_CLASSIC) throw classicFailed;
            return BlePrinterConnection.open(getContext(), device, name, lost);
        }
    }

    private static String labelOf(BluetoothDevice device) {
        String name = device.getName();
        return (name != null && !name.isEmpty() ? name : "?") + "  (" + device.getAddress() + ")";
    }

    /** Android 12 and newer ask the user once for "Nearby devices"; older versions need nothing at run time. */
    private void withPermission(PluginCall call, String then) {
        if (Build.VERSION.SDK_INT >= 31 && getPermissionState("bluetooth") != PermissionState.GRANTED) {
            requestPermissionForAlias("bluetooth", call, then);
        } else if (then.equals("pickAllowed")) {
            pickAllowed(call);
        } else {
            connectAllowed(call);
        }
    }

    private boolean allowed(PluginCall call) {
        if (Build.VERSION.SDK_INT >= 31 && getPermissionState("bluetooth") != PermissionState.GRANTED) {
            call.reject("The app may not use Bluetooth. Allow \"Nearby devices\" for it in the tablet settings.", "PERMISSION");
            return false;
        }
        return true;
    }

    private BluetoothAdapter adapter(PluginCall call) {
        BluetoothManager manager = (BluetoothManager) getContext().getSystemService(Context.BLUETOOTH_SERVICE);
        BluetoothAdapter adapter = manager != null ? manager.getAdapter() : null;
        if (adapter == null) {
            call.reject("This tablet has no Bluetooth.");
            return null;
        }
        if (!adapter.isEnabled()) {
            call.reject("Bluetooth is off. Turn it on in the tablet settings.");
            return null;
        }
        return adapter;
    }
}
