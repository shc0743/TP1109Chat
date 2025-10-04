package com.furieau.apps.tp1109chatandroidapp.server;
import fi.iki.elonen.NanoHTTPD;
import android.content.res.AssetManager;
import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

public class StaticFileModule {
    private final AssetManager assetManager;
    private static final Map<String, String> MIME_TYPES = new HashMap<>();

    static {
        MIME_TYPES.put("css", "text/css");
        MIME_TYPES.put("js", "application/javascript");
        MIME_TYPES.put("html", "text/html");
        MIME_TYPES.put("svg", "image/svg+xml");
        MIME_TYPES.put("png", "image/png");
        MIME_TYPES.put("jpg", "image/jpeg");
        MIME_TYPES.put("json", "application/json");
        MIME_TYPES.put("txt", "text/plain");
    }

    public StaticFileModule(AssetManager assetManager) {
        this.assetManager = assetManager;
    }

    public NanoHTTPD.Response serve(String uri) {
        String filePath = normalizePath(uri);

        try {
            InputStream inputStream;

            // 尝试打开请求的文件
            try {
                inputStream = assetManager.open(filePath);
            } catch (IOException e) {
                // Fallback to index.html for SPA
                try {
                    inputStream = assetManager.open("index.html");
                } catch (IOException e2) {
                    return NanoHTTPD.newFixedLengthResponse(
                            NanoHTTPD.Response.Status.NOT_FOUND, "text/plain", "File not found");
                }
            }

            String mimeType = getMimeType(filePath);
            return NanoHTTPD.newChunkedResponse(NanoHTTPD.Response.Status.OK, mimeType, inputStream);

        } catch (Exception e) {
            return NanoHTTPD.newFixedLengthResponse(
                    NanoHTTPD.Response.Status.INTERNAL_ERROR, "text/plain",
                    "Error: " + e.getMessage());
        }
    }

    private String normalizePath(String uri) {
        if (uri.equals("/") || uri.isEmpty()) {
            return "index.html";
        }

        // 移除开头的斜杠
        String path = uri.startsWith("/") ? uri.substring(1) : uri;

        // 安全检查
        if (path.contains("..") || path.contains("//")) {
            return "index.html";
        }

        return path;
    }

    private String getMimeType(String filename) {
        int dotIndex = filename.lastIndexOf('.');
        if (dotIndex > 0) {
            String extension = filename.substring(dotIndex + 1).toLowerCase();
            return MIME_TYPES.getOrDefault(extension, "application/octet-stream");
        }
        return "application/octet-stream";
    }
}