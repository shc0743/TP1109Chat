package com.furieau.apps.tp1109chatandroidapp.server;
import static androidx.core.app.ActivityCompat.startActivityForResult;

import fi.iki.elonen.NanoHTTPD;

import com.furieau.apps.tp1109chatandroidapp.MainActivity;
import com.furieau.apps.tp1109chatandroidapp.MainApplication;
import com.furieau.apps.tp1109chatandroidapp.util.PermissionUtils;
import com.google.gson.Gson;

import android.Manifest;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCallback;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattDescriptor;
import android.bluetooth.BluetoothGattService;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothProfile;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanFilter;
import android.bluetooth.le.ScanResult;
import android.bluetooth.le.ScanSettings;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import androidx.annotation.RequiresPermission;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.*;

public class ApiController {
    private final Gson gson = new Gson();
    private final File appDataDir;
    // 蓝牙相关
    private BluetoothAdapter bluetoothAdapter;
    private BluetoothGatt bluetoothGatt;
    private boolean bluetoothConnected = false;
    private final Object bluetoothLock = new Object();
    public static final int PERMISSION_REQUEST_CODE = 1001;

    // UUID
    private static final UUID SERVICE_UUID = UUID.fromString("00002760-08c2-11e1-9073-0e8ac72e1001");
    private static final UUID NOTIFY_CHAR_UUID = UUID.fromString("00002760-08c2-11e1-9073-0e8ac72e0002");
    private static final UUID WRITE_CHAR_UUID = UUID.fromString("00002760-08c2-11e1-9073-0e8ac72e0001");

    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private ChatWebSocket.ChatWebSocketSession webSocketSession;

    public ApiController(File appDataDir) {
        this.appDataDir = appDataDir;

        // 初始化蓝牙适配器
        BluetoothManager bluetoothManager = (BluetoothManager)
                MainApplication.getAppContext().getSystemService(Context.BLUETOOTH_SERVICE);
        if (bluetoothManager != null) {
            bluetoothAdapter = bluetoothManager.getAdapter();
        }

        // 确保应用数据目录存在
        if (!appDataDir.exists()) {
            appDataDir.mkdirs();
        }
    }

    public void setWebSocketSession(ChatWebSocket.ChatWebSocketSession session) {
        this.webSocketSession = session;
    }

    // 文件系统 API
    public NanoHTTPD.Response getFile(String fileName) {
        try {
            File file = new File(appDataDir, fileName);

            if (!file.exists()) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.NOT_FOUND, "text/plain", "文件不存在");
            }

            FileInputStream fis = new FileInputStream(file);
            long fileSize = file.length();

            return NanoHTTPD.newChunkedResponse(
                    NanoHTTPD.Response.Status.OK,
                    "application/octet-stream",
                    fis
            );

        } catch (Exception e) {
            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.INTERNAL_ERROR, "text/plain",
                    "读取文件失败: " + e.getMessage());
        }
    }

    public NanoHTTPD.Response headFile(String fileName) {
        try {
            File file = new File(appDataDir, fileName);

            if (!file.exists()) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.NOT_FOUND, "text/plain", "文件不存在");
            }

            NanoHTTPD.Response response = NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.OK, "text/plain", "");
            response.addHeader("X-File-Size", String.valueOf(file.length()));
            response.addHeader("X-File-Last-Modified", String.valueOf(file.lastModified()));
            return response;

        } catch (Exception e) {
            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.INTERNAL_ERROR, "text/plain",
                    "获取文件信息失败: " + e.getMessage());
        }
    }

    public NanoHTTPD.Response putFile(String fileName, NanoHTTPD.IHTTPSession session) {
        FileOutputStream fos = null;
        try {
            if (fileName == null || fileName.isEmpty()) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.BAD_REQUEST, "text/plain", "文件名不能为空");
            }

            if (fileName.contains("..") || fileName.contains("/") || fileName.contains("\\")) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.BAD_REQUEST, "text/plain", "无效的文件名");
            }

            File file = new File(appDataDir, fileName);
            File canonicalFile = file.getCanonicalFile();
            File canonicalAppDataDir = appDataDir.getCanonicalFile();

            if (!canonicalFile.getPath().startsWith(canonicalAppDataDir.getPath())) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.BAD_REQUEST, "text/plain", "无效的文件路径");
            }

            // 确保父目录存在
            File parentDir = file.getParentFile();
            if (parentDir != null && !parentDir.exists()) {
                parentDir.mkdirs();
            }

            // 读取请求体数据
            Map<String, String> headers = session.getHeaders();
            String contentLengthHeader = headers.get("content-length");
            if (contentLengthHeader == null) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.BAD_REQUEST, "text/plain", "缺少Content-Length");
            }

            int contentLength = Integer.parseInt(contentLengthHeader);
            Log.d("ApiController", "PUT文件: " + fileName + ", 大小: " + contentLength + "字节");

            fos = new FileOutputStream(file);
            InputStream inputStream = session.getInputStream();
            byte[] buffer = new byte[8192];
            int bytesRead;
            int totalBytes = 0;

            while (totalBytes < contentLength && (bytesRead = inputStream.read(buffer, 0,
                    Math.min(buffer.length, contentLength - totalBytes))) != -1) {
                fos.write(buffer, 0, bytesRead);
                totalBytes += bytesRead;
                Log.d("ApiController", "已写入: " + bytesRead + "字节, 总计: " + totalBytes + "/" + contentLength);
            }

            fos.close();

            Log.d("ApiController", "文件写入完成: " + totalBytes + "字节");

            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.CREATED, "text/plain",
                    "文件创建成功，大小: " + totalBytes + "字节");

        } catch (Exception e) {
            if (fos != null) {
                try { fos.close(); } catch (Exception ignored) {}
            }
            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.INTERNAL_ERROR, "text/plain",
                    "写入文件失败: " + e.getMessage());
        }
    }
    public NanoHTTPD.Response deleteFile(String fileName) {
        try {
            File file = new File(appDataDir, fileName);

            if (!file.exists()) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.NOT_FOUND, "text/plain", "文件不存在");
            }

            if (file.delete()) {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.NO_CONTENT, "text/plain", "");
            } else {
                return NanoHTTPD.newFixedLengthResponse(
                        NanoHTTPD.Response.Status.INTERNAL_ERROR, "text/plain", "删除文件失败");
            }

        } catch (Exception e) {
            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.INTERNAL_ERROR, "text/plain",
                    "删除文件失败: " + e.getMessage());
        }
    }

    public NanoHTTPD.Response checkInstalled() {
        Map<String, Object> result = new HashMap<>();
        result.put("installed", true); // Android 上总是已"安装"
        String json = gson.toJson(result);
        return NanoHTTPD.newFixedLengthResponse(
                NanoHTTPD.Response.Status.OK, "application/json", json);
    }

    // 蓝牙 API
    public NanoHTTPD.Response scanBluetoothDevices() {
        if (bluetoothAdapter == null) {
            return errorResponse("蓝牙不可用");
        }

        if (!bluetoothAdapter.isEnabled()) {
            Intent enableBtIntent = new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE);
            startActivityForResult(MainActivity.getInstance(), enableBtIntent, 1002, null);
            return errorResponse("请先启用蓝牙");
        }

        if (!PermissionUtils.checkPermissions(MainApplication.getAppContext())) {
            return errorResponse("没有权限，请先授权");
        }

        try {
            Map<String, Map<String, Object>> deviceMap = new HashMap<>();
            BluetoothLeScanner scanner = bluetoothAdapter.getBluetoothLeScanner();

            if (scanner == null) {
                return errorResponse("蓝牙扫描器不可用");
            }

            ScanCallback scanCallback = new ScanCallback() {
                @Override
                public void onScanResult(int callbackType, ScanResult result) {
                    BluetoothDevice device = result.getDevice();
                    if (device == null || device.getAddress() == null) return;

                    Map<String, Object> deviceInfo = new HashMap<>();
                    deviceInfo.put("name", device.getName());
                    // 去掉冒号，转成纯16进制字符串
                    deviceInfo.put("addr", "0x" + device.getAddress().replace(":", ""));
                    deviceInfo.put("rssi", result.getRssi());

                    deviceMap.put(device.getAddress(), deviceInfo);
                }

                @Override
                public void onScanFailed(int errorCode) {
                    Log.e("Bluetooth", "扫描失败，错误码: " + errorCode);
                }
            };

            // 配置扫描参数
            ScanSettings settings = new ScanSettings.Builder()
                    .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
                    .build();

            List<ScanFilter> filters = new ArrayList<>(); // 不设置过滤器，扫描所有设备

            // 开始扫描
            scanner.startScan(filters, settings, scanCallback);

            // 扫描5秒
            Thread.sleep(5000);
            scanner.stopScan(scanCallback);

            List<Map<String, Object>> devices = new ArrayList<>(deviceMap.values());
            for (int i = 0; i < devices.size(); i++) {
                devices.get(i).put("index", i);
            }

            Map<String, Object> result = new HashMap<>();
            result.put("devices", devices);
            result.put("error", null);

            String json = gson.toJson(result);
            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.OK, "application/json", json);
        } catch (SecurityException e) {
            return errorResponse("缺少蓝牙权限: " + e.getMessage());
        } catch (Exception e) {
            return errorResponse("扫描设备失败: " + e.getMessage());
        }
    }

    public NanoHTTPD.Response getBluetoothUUID() {
        Map<String, Object> uuids = new HashMap<>();
        uuids.put("serviceUUID", SERVICE_UUID.toString());
        uuids.put("characteristicUUID", WRITE_CHAR_UUID.toString());
        uuids.put("notificationUUID", NOTIFY_CHAR_UUID.toString());

        String json = gson.toJson(uuids);
        return NanoHTTPD.newFixedLengthResponse(
                NanoHTTPD.Response.Status.OK, "application/json", json);
    }

    public NanoHTTPD.Response connectBluetoothDevice(GenericRequest request) {
        synchronized (bluetoothLock) {
            if (bluetoothConnected) {
                return successResponse("已连接");
            }

            if (bluetoothAdapter == null || !bluetoothAdapter.isEnabled()) {
                return errorResponse("蓝牙不可用");
            }

            String deviceAddress = request.text1;
            if (deviceAddress == null || deviceAddress.isEmpty()) {
                return errorResponse("设备地址不能为空");
            }

            if (!checkBluetoothConnectPermission()) {
                // 返回需要权限的错误信息
                return errorResponse("需要蓝牙连接权限，请在应用中授予权限");
            }

            String formattedAddress = formatBluetoothAddress(deviceAddress);
            if (formattedAddress == null) {
                return errorResponse("无效的蓝牙地址: " + deviceAddress);
            }

            try {
                BluetoothDevice device = bluetoothAdapter.getRemoteDevice(formattedAddress);
                if (device == null) {
                    return errorResponse("未找到设备");
                }

                bluetoothGatt = device.connectGatt(
                        MainApplication.getAppContext(),
                        false,
                        gattCallback
                );
                return successResponse("连接中...");

            } catch (Exception e) {
                return errorResponse("连接失败: " + e.getMessage());
            }
        }
    }

    /**
     * 格式化蓝牙地址为标准格式 (XX:XX:XX:XX:XX:XX)
     */
    private String formatBluetoothAddress(String address) {
        if (address == null) return null;

        // 移除所有空格和特殊字符
        String cleanAddress = address.replaceAll("[\\s:\\-]", "").toUpperCase();

        // 如果是十六进制格式（包含0x前缀或A-F字符）
        if (cleanAddress.matches("(0X)?[A-F0-9]+")) {
            // 移除0x前缀
            if (cleanAddress.startsWith("0X")) {
                cleanAddress = cleanAddress.substring(2);
            }

            // 处理十六进制字符串
            return formatHexBluetoothAddress(cleanAddress);
        }
        // 如果是纯数字，可能是十进制表示
        else if (cleanAddress.matches("\\d+")) {
            try {
                // 将十进制转换为十六进制
                long decimalValue = Long.parseLong(cleanAddress);
                String hexString = Long.toHexString(decimalValue).toUpperCase();
                return formatHexBluetoothAddress(hexString);
            } catch (NumberFormatException e) {
                return null;
            }
        }

        return null;
    }

    /**
     * 处理十六进制字符串的蓝牙地址格式化
     */
    private String formatHexBluetoothAddress(String hexAddress) {
        // 补齐前导零到12位
        while (hexAddress.length() < 12) {
            hexAddress = "0" + hexAddress;
        }

        // 如果超过12位，取后12位（低位）
        if (hexAddress.length() > 12) {
            hexAddress = hexAddress.substring(hexAddress.length() - 12);
        }

        // 格式化为标准蓝牙地址
        StringBuilder formatted = new StringBuilder();
        for (int i = 0; i < 12; i += 2) {
            if (i > 0) {
                formatted.append(":");
            }
            formatted.append(hexAddress.substring(i, i + 2));
        }

        return formatted.toString();
    }

    private boolean checkBluetoothConnectPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            return ContextCompat.checkSelfPermission(MainApplication.getAppContext(), Manifest.permission.BLUETOOTH_CONNECT)
                    == PackageManager.PERMISSION_GRANTED;
        } else {
            return ContextCompat.checkSelfPermission(MainApplication.getAppContext(), Manifest.permission.BLUETOOTH)
                    == PackageManager.PERMISSION_GRANTED;
        }
    }

    public NanoHTTPD.Response disconnectBluetoothDevice() {
        if (ActivityCompat.checkSelfPermission(MainApplication.getAppContext(), Manifest.permission.BLUETOOTH_CONNECT) != PackageManager.PERMISSION_GRANTED) {
            return errorResponse("没有权限");
        }
        synchronized (bluetoothLock) {
            if (bluetoothGatt != null) {
                bluetoothGatt.disconnect();
                bluetoothGatt.close();
                bluetoothGatt = null;
            }

            bluetoothConnected = false;
            return successResponse("已断开连接");
        }
    }

    private BluetoothGattCharacteristic writeChar, notifyChar;
    // 蓝牙 GATT 回调
    private final BluetoothGattCallback gattCallback = new BluetoothGattCallback() {
        @RequiresPermission(Manifest.permission.BLUETOOTH_CONNECT)
        @Override
        public void onConnectionStateChange(BluetoothGatt gatt, int status, int newState) {
            synchronized (bluetoothLock) {
                if (newState == BluetoothProfile.STATE_CONNECTED) {
                    // 连接成功，发现服务
                    boolean mtuRequested = gatt.requestMtu(512);
                    Log.d("Bluetooth", "MTU request result: " + mtuRequested);
                    gatt.discoverServices();
                } else if (newState == BluetoothProfile.STATE_DISCONNECTED) {
                    bluetoothConnected = false;
                }
            }
        }

        @RequiresPermission(Manifest.permission.BLUETOOTH_CONNECT)
        @Override
        public void onServicesDiscovered(BluetoothGatt gatt, int status) {
            if (status == BluetoothGatt.GATT_SUCCESS) {
                BluetoothGattService service = gatt.getService(SERVICE_UUID);
                if (service != null) {
                    writeChar = service.getCharacteristic(WRITE_CHAR_UUID);
                    notifyChar = service.getCharacteristic(NOTIFY_CHAR_UUID);

                    if (notifyChar != null) {
                        // 启用通知
                        gatt.setCharacteristicNotification(notifyChar, true);
                        BluetoothGattDescriptor descriptor = notifyChar.getDescriptor(
                                UUID.fromString("00002902-0000-1000-8000-00805f9b34fb")
                        );
                        if (descriptor != null) {
                            descriptor.setValue(BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE);
                            gatt.writeDescriptor(descriptor);
                            Log.d("BluetoothConn", "Enabled notifications with CCCD");
                        } else {
                            Log.e("BluetoothConn", "Notification descriptor not found");
                        }
                    }
                    else {
                        Log.e("BluetoothConn", "Unable to find notify char");
                    }

                    synchronized (bluetoothLock) {
                        bluetoothConnected = true;
                    }
                }
            }
        }

        @Override
        public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) {
            if (characteristic.getUuid().equals(NOTIFY_CHAR_UUID)) {
                byte[] data = characteristic.getValue();
                Log.d("BluetoothCb", "data received, len=" + data.length);
                if (data.length > 0) {
                    forwardToWebSocket(data);
                }
            }
        }
    };

    private void forwardToWebSocket(byte[] data) {
        new Thread(() -> {
            MainActivity.getInstance().getWebServer().getChatWebSocket().broadcastToWebSockets(data);
        }).start();
    }

    // 发送数据到蓝牙设备
    @RequiresPermission(Manifest.permission.BLUETOOTH_CONNECT)
    public void sendToBluetoothDevice(byte[] data) {
        synchronized (bluetoothLock) {
            if (bluetoothConnected && bluetoothGatt != null) {
                if (writeChar != null) {
                    writeChar.setValue(data);
                    bluetoothGatt.writeCharacteristic(writeChar);
                    Log.d("sendToBluetoothDevice", "wrote data to device, len=" + data.length);
                } else {
                    Log.d("sendToBluetoothDevice", "writeChar is null, bluetooth device is not open");
                }
            }
        }
    }

    // 工具方法
    private NanoHTTPD.Response successResponse(Object data) {
        Map<String, Object> result = new HashMap<>();
        result.put("success", true);
        result.put("data", data);
        String json = gson.toJson(result);
        return NanoHTTPD.newFixedLengthResponse(
                NanoHTTPD.Response.Status.OK, "application/json", json);
    }

    private NanoHTTPD.Response errorResponse(String error) {
        Map<String, Object> result = new HashMap<>();
        result.put("success", false);
        result.put("error", error);
        String json = gson.toJson(result);
        return NanoHTTPD.newFixedLengthResponse(
                NanoHTTPD.Response.Status.OK, "application/json", json);
    }

    public static String[] getPermissions() {
        String[] permissions;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            permissions = new String[]{
                    Manifest.permission.BLUETOOTH_CONNECT,
                    Manifest.permission.BLUETOOTH_SCAN
            };
        } else {
            permissions = new String[]{
                    Manifest.permission.BLUETOOTH,
                    Manifest.permission.BLUETOOTH_ADMIN,
                    Manifest.permission.ACCESS_FINE_LOCATION
            };
        }
        return permissions;
    }

    public NanoHTTPD.Response getPlatformInfo() {
        Map<String, Object> result = new HashMap<>();
        result.put("platform", "Android");
        result.put("version", Build.VERSION.RELEASE);
        result.put("model", Build.MODEL);
        result.put("manufacturer", Build.MANUFACTURER);

        String json = gson.toJson(result);
        return NanoHTTPD.newFixedLengthResponse(
                NanoHTTPD.Response.Status.OK, "application/json", json);
    }
}
