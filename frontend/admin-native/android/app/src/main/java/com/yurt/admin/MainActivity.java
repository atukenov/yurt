package com.yurt.admin;

import android.os.Bundle;
import android.view.WindowManager;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {

    @Override
    public void onCreate(Bundle savedInstanceState) {
        // Local plugin: must be registered before the bridge starts.
        registerPlugin(YurtPrinterPlugin.class);
        super.onCreate(savedInstanceState);
        // The tablet prints only while the app is on screen, so the screen must not go off by itself.
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
    }
}
