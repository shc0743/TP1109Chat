package com.furieau.apps.tp1109chatandroidapp.client;

import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.webkit.JavascriptInterface;
import android.widget.Toast;

public class WebAppInterface {
    private Context mContext;

    public WebAppInterface(Context context) {
        this.mContext = context;
    }

    @JavascriptInterface
    public void writeClipboard(String text) {
        // 在 Java 端处理剪贴板
        ClipboardManager clipboard = (ClipboardManager) mContext.getSystemService(Context.CLIPBOARD_SERVICE);
        ClipData clip = ClipData.newPlainText("Copied Text", text);
        clipboard.setPrimaryClip(clip);

        // 可选：显示Toast提示
        Toast.makeText(mContext, "已复制到剪贴板", Toast.LENGTH_SHORT).show();
    }

    @JavascriptInterface
    public String readClipboard() {
        // 读取剪贴板内容
        ClipboardManager clipboard = (ClipboardManager) mContext.getSystemService(Context.CLIPBOARD_SERVICE);
        if (clipboard.hasPrimaryClip()) {
            ClipData.Item item = clipboard.getPrimaryClip().getItemAt(0);
            return item.getText().toString();
        }
        return "";
    }
}