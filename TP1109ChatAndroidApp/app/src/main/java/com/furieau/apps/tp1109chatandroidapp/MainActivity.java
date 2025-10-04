package com.furieau.apps.tp1109chatandroidapp;

import static com.furieau.apps.tp1109chatandroidapp.server.ApiController.PERMISSION_REQUEST_CODE;
import static com.furieau.apps.tp1109chatandroidapp.server.ApiController.getPermissions;

import android.Manifest;
import android.app.Activity;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.webkit.WebView;
import android.widget.Toast;

import androidx.activity.EdgeToEdge;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;

import com.furieau.apps.tp1109chatandroidapp.client.WebAppInterface;
import com.furieau.apps.tp1109chatandroidapp.server.ApiController;
import com.furieau.apps.tp1109chatandroidapp.server.GenericRequest;
import com.furieau.apps.tp1109chatandroidapp.server.WebServer;

import java.io.File;
import java.io.IOException;
import java.util.Random;

public class MainActivity extends AppCompatActivity {
    private String pendingBluetoothOperation = null;
    private GenericRequest pendingBluetoothRequest = null;
    private ApiController apiController;
    private WebServer webServer;
    private int port;
    private static MainActivity _instance;

    public static MainActivity getInstance() {
        return _instance;
    }
    public WebServer getWebServer() {
        return webServer;
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        WebView.setWebContentsDebuggingEnabled(true);
        EdgeToEdge.enable(this);
        setContentView(R.layout.activity_main);
        MainApplication.setAppContext(this);
        ViewCompat.setOnApplyWindowInsetsListener(findViewById(R.id.main), (v, insets) -> {
            Insets systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars());
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom);
            return insets;
        });
        _instance = this;

        File appDataDir = new File(getFilesDir(), "userdata");
        apiController = new ApiController(appDataDir);
        Random random = new Random();
        port = random.nextInt(65535 - 60000) + 60000;
        if (((MainApplication.getAppContext().getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0))
            port = 60123;
        webServer = new WebServer(port, getAssets(), appDataDir, apiController);

        startWebServer();
    }

    private void startWebServer() {
        WebView w = findViewById(R.id.webview);
        w.getSettings().setJavaScriptEnabled(true);
        w.getSettings().setDomStorageEnabled(true);
        w.getSettings().setAllowFileAccess(true);
        w.getSettings().setMediaPlaybackRequiresUserGesture(false);
        w.getSettings().setAllowContentAccess(true);
        w.addJavascriptInterface(new WebAppInterface(this), "WebView");

        try {
            webServer.start();
            w.loadUrl("http://127.0.0.1:" + Integer.toString(port) + "/");
        } catch (IOException e) {
            w.loadData("服务器加载失败", "text/plain", "UTF-8");
        }
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (webServer != null) {
            webServer.stop();
        }
        _instance = null;
    }

    // 处理蓝牙操作队列
    private void executePendingBluetoothOperation() {
        if (pendingBluetoothOperation != null && pendingBluetoothRequest != null) {
            // 这里可以添加具体的操作逻辑
            // 例如重新发起连接请求等
            pendingBluetoothOperation = null;
            pendingBluetoothRequest = null;
        }
    }
}