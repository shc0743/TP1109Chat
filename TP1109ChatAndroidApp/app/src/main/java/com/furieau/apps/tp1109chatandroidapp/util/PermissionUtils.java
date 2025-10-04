package com.furieau.apps.tp1109chatandroidapp.util;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Build.VERSION;
import android.os.Build.VERSION_CODES;
import android.util.Log;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import java.util.ArrayList;
import java.util.List;

public class PermissionUtils {

    public static boolean checkPermissions(Context context) {
        if (VERSION.SDK_INT >= VERSION_CODES.S) {
            boolean btConn = ContextCompat.checkSelfPermission(context, Manifest.permission.BLUETOOTH_CONNECT) == PackageManager.PERMISSION_GRANTED,
                    btScan = ContextCompat.checkSelfPermission(context, Manifest.permission.BLUETOOTH_SCAN) == PackageManager.PERMISSION_GRANTED,
                    loc = hasLocationPermission(context);
            Log.d("PermissionUtils", "权限检查结果: " +
                    "BLUETOOTH_CONNECT=" + btConn + ", " +
                    "BLUETOOTH_SCAN=" + btScan + ", " +
                    "LOCATION=" + loc);
            return btConn && btScan && loc;
        } else {
            return hasLocationPermission(context);
        }
    }

    private static boolean hasLocationPermission(Context context) {
        return ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED;
    }

    public static void requestPermissions(Activity activity, int requestCode) {
        List<String> permissionsToRequest = new ArrayList<>();

        if (VERSION.SDK_INT >= VERSION_CODES.S) {
            permissionsToRequest.add(Manifest.permission.BLUETOOTH_CONNECT);
            permissionsToRequest.add(Manifest.permission.BLUETOOTH_SCAN);
        }
        permissionsToRequest.add(Manifest.permission.ACCESS_FINE_LOCATION);

        ActivityCompat.requestPermissions(activity,
                permissionsToRequest.toArray(new String[0]),
                requestCode);
    }

    public static boolean isPermissionsGranted(int[] grantResults) {
        for (int result : grantResults) {
            if (result != PackageManager.PERMISSION_GRANTED) {
                return false;
            }
        }
        return true;
    }
}