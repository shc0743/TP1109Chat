package com.furieau.apps.tp1109chatandroidapp.server;

import android.util.Log;

import fi.iki.elonen.NanoWSD;
import com.google.gson.Gson;
import java.io.IOException;
import java.util.concurrent.CopyOnWriteArraySet;
import java.util.Set;

public class ChatWebSocket extends NanoWSD {
    private final ApiController apiController;
    private final Gson gson = new Gson();
    private final Set<ChatWebSocketSession> activeSessions = new CopyOnWriteArraySet<>();

    public ChatWebSocket(ApiController apiController, int port) {
        super(port);
        this.apiController = apiController;
    }

    @Override
    protected WebSocket openWebSocket(IHTTPSession handshake) {
        ChatWebSocketSession session = new ChatWebSocketSession(handshake, apiController);
        activeSessions.add(session);
        return session;
    }

    public class ChatWebSocketSession extends WebSocket {
        private final ApiController apiController;
        private boolean isConnected = false;

        public ChatWebSocketSession(IHTTPSession handshake, ApiController apiController) {
            super(handshake);
            this.apiController = apiController;
        }

        @Override
        protected void onOpen() {
            isConnected = true;
            Log.d("ChatWebSocket", "WebSocket 连接已建立，当前连接数: " + activeSessions.size());
        }

        @Override
        protected void onClose(WebSocketFrame.CloseCode code, String reason, boolean initiatedByRemote) {
            isConnected = false;
            activeSessions.remove(this);
            Log.d("ChatWebSocket", "WebSocket 连接已关闭: " + reason + ", 剩余连接数: " + activeSessions.size());
        }

        @Override
        protected void onMessage(WebSocketFrame message) {
            try {
                byte[] binaryData = message.getBinaryPayload();
                // 将接收到的数据发送到蓝牙设备
                if (apiController != null) {
                    Log.d("ChatWebSocket", "Message received, wrote to device");
                    apiController.sendToBluetoothDevice(binaryData);
                }
            } catch (SecurityException e) {
                Log.w("ChatWebSocket", "Bluetooth Permission Denied");
            } catch (Exception e) {
                Log.e("ChatWebSocket", e.toString(), e);
            }
        }

        @Override
        protected void onPong(WebSocketFrame pong) {
            // 处理 pong，更新最后活动时间
            Log.d("ChatWebSocket", "收到 Pong");
        }

        @Override
        protected void onException(IOException exception) {
            Log.e("ChatWebSocket", "WebSocket 异常", exception);
        }

        protected void sendBinaryData(byte[] data) {
            if (isConnected) {
                try {
                    send(data);
                    Log.d("ChatWebSocket", "发送数据到 WebSocket 客户端，长度: " + data.length);
                } catch (IOException e) {
                    Log.e("ChatWebSocket", "发送数据失败", e);
                    isConnected = false;
                }
            }
        }

        // 添加心跳检测方法
        public void sendPing() {
            if (isConnected) {
                try {
                    ping(new byte[0]);
                } catch (IOException e) {
                    Log.e("ChatWebSocket", "发送 Ping 失败", e);
                    isConnected = false;
                }
            }
        }
    }

    // 改进的广播方法，支持多个客户端
    public void broadcastToWebSockets(byte[] data) {
        for (ChatWebSocketSession session : activeSessions) {
            if (session.isConnected) {
                session.sendBinaryData(data);
            }
        }
    }

    // 添加心跳维护
    public void startHeartbeat() {
        new Thread(() -> {
            while (!Thread.interrupted()) {
                try {
                    Thread.sleep(1000); // 每秒发送一次心跳

                    for (ChatWebSocketSession session : activeSessions) {
                        session.sendPing();
                    }
                } catch (InterruptedException e) {
                    Thread.currentThread().interrupt();
                    break;
                }
            }
        }).start();
    }

    // 获取当前活跃连接数
    public int getActiveSessionCount() {
        return activeSessions.size();
    }
}