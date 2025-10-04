package com.furieau.apps.tp1109chatandroidapp.server;

import static android.content.ContentValues.TAG;

import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.res.AssetManager;
import android.net.Uri;
import android.util.Log;
import android.widget.Toast;

import fi.iki.elonen.NanoHTTPD;

import com.furieau.apps.tp1109chatandroidapp.MainActivity;
import com.furieau.apps.tp1109chatandroidapp.MainApplication;
import com.furieau.apps.tp1109chatandroidapp.util.PermissionUtils;
import com.google.gson.Gson;

import java.io.File;
import java.io.IOException;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

public class WebServer extends NanoHTTPD {
    private final Gson gson = new Gson();
    private final ApiController apiController;
    private final ChatWebSocket chatWebSocket;

    private final File appDataDir;
    private final StaticFileModule staticFileModule;

    public WebServer(int port, AssetManager assetManager, File appDataDir, ApiController apiController) {
        super(((MainApplication.getAppContext().getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0) ? null : "127.0.0.1", port);
        this.appDataDir = appDataDir;
        this.apiController = apiController;
        this.staticFileModule = new StaticFileModule(assetManager);
        this.chatWebSocket = new ChatWebSocket(apiController, port);
        this.chatWebSocket.startHeartbeat();
    }

    public ChatWebSocket getChatWebSocket() {
        return  chatWebSocket;
    }

    @Override
    public Response serve(IHTTPSession session) {
        String uri = session.getUri();
        Method method = session.getMethod();

        try {
            if (uri.startsWith("/api/")) {
                return handleApiRoutes(uri.substring(5), method, session); // 去掉 "/api/"
            }
            // WebSocket 路由 - 对应 C# 的 /ws/chat
            else if (uri.equals("/ws/chat") && method == Method.GET) {
                return chatWebSocket.serve(session);
            }
            // 默认响应
            else {
                return staticFileModule.serve(uri);
            }

        } catch (Exception e) {
            return newFixedLengthResponse(Response.Status.INTERNAL_ERROR, "application/json",
                    "{\"error\":\"" + e.getMessage() + "\"}");
        }
    }

    private Response handleApiRoutes(String apiPath, Method method, IHTTPSession session) {
        switch (apiPath) {
            case "fs/object":
                try {
                    return handleFileSystemApi(method, session);
                } catch (IOException e) {
                    break;
                }
            case "platform":
                if (method == Method.GET) return apiController.getPlatformInfo();
                return newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED, "text/plain", "Method Not Allowed");
            case "settings/installed":
                if (method == Method.GET) return apiController.checkInstalled();
                return newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED, "text/plain", "Method Not Allowed");
            case "settings/uninstall":
                if (method == Method.POST) {
                    Map<String, String> files = new HashMap<>();
                    try {
                        session.parseBody(files);
                    } catch (IOException | ResponseException e) {
                        break;
                    }
                    Uri packageUri = Uri.parse("package:" + MainApplication.getAppContext().getPackageName()); // 这里是要卸载的应用包名
                    Intent uninstallIntent = new Intent(Intent.ACTION_DELETE);
                    uninstallIntent.setData(packageUri);
                    uninstallIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    MainApplication.getAppContext().startActivity(uninstallIntent, null);

                    return newFixedLengthResponse("{\"success\":true}");
                }
                return newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED, "text/plain", "Method Not Allowed");
            case "askPerm":
                if (method == Method.POST) {
                    Map<String, String> files = new HashMap<>();
                    try {
                        session.parseBody(files);
                    } catch (IOException | ResponseException e) {
                        break;
                    }

                    PermissionUtils.requestPermissions(MainActivity.getInstance(), 1001);
                    if (PermissionUtils.checkPermissions(MainApplication.getAppContext())) {
                        // 权限全部授予，返回true
                        Log.d("Permission", "所有权限已授予");
                        MainActivity.getInstance().runOnUiThread(() -> Toast.makeText(MainApplication.getAppContext(), "已经授权", Toast.LENGTH_SHORT).show());
                    } else {
                        // 权限被拒绝，返回false
                        Log.d("Permission", "部分或所有权限被拒绝");
                        MainActivity.getInstance().runOnUiThread(() -> Toast.makeText(MainApplication.getAppContext(), "用户拒绝授权", Toast.LENGTH_SHORT).show());
                    }

                    return newFixedLengthResponse("{\"success\":true}");
                }
                return newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED, "text/plain", "Method Not Allowed");
            default:
                if (apiPath.startsWith("bluetooth/")) {
                    try {
                        return handleBluetoothApi(method, apiPath, session);
                    } catch (IOException e) {
                        break;
                    }
                }

                return newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "API not found");
        }
        return newFixedLengthResponse(Response.Status.INTERNAL_ERROR, "text/plain", "Internal Server Error");
    }

    private Response handleFileSystemApi(Method method, IHTTPSession session) throws IOException {
        Map<String, List<String>> params = session.getParameters();
        String fileName = Objects.requireNonNull(params.get("name")).get(0);

        if (fileName == null || fileName.isEmpty()) {
            return newFixedLengthResponse(Response.Status.BAD_REQUEST, "text/plain", "文件名不能为空");
        }

        // 防止路径遍历攻击
        if (fileName.contains("..") || fileName.contains("/") || fileName.contains("\\")) {
            return newFixedLengthResponse(Response.Status.BAD_REQUEST, "text/plain", "无效的文件名");
        }

        switch (method) {
            case GET:
                return apiController.getFile(fileName);
            case HEAD:
                return apiController.headFile(fileName);
            case PUT:
                return apiController.putFile(fileName, session);
            case DELETE:
                return apiController.deleteFile(fileName);
            default:
                return newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED, "text/plain", "Method not allowed");
        }
    }

    private Response handleBluetoothApi(Method method, String uri, IHTTPSession session) throws IOException {
        switch (uri) {
            case "bluetooth/scandevices":
                if (method == Method.GET) {
                    return apiController.scanBluetoothDevices();
                }
                break;
            case "bluetooth/uuid":
                if (method == Method.GET) {
                    return apiController.getBluetoothUUID();
                }
                break;
            case "bluetooth/connect":
                if (method == Method.POST) {
                    try {
                        Map<String, String> files = new HashMap<>();
                        session.parseBody(files);
                        String postData = null;
                        if (files.containsKey("postData")) {
                            postData = files.get("postData");
                        }
                        GenericRequest request = gson.fromJson(postData != null ? postData : "{}", GenericRequest.class);
                        return apiController.connectBluetoothDevice(request);
                    } catch (Exception e) {
                        Log.e(TAG, "handleBluetoothApi: Unexpected body", e);
                    }
                }
                break;
            case "bluetooth/disconnect":
                if (method == Method.POST) {
                    return apiController.disconnectBluetoothDevice();
                }
                break;
        }
        return newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "API not found");
    }
}
