package com.furieau.apps.tp1109chatandroidapp;
import android.app.Application;
import android.content.Context;

public class MainApplication extends Application {
    private static Context appContext;

    @Override
    public void onCreate() {
        super.onCreate();
        appContext = this;
    }

    public static Context getAppContext() {
        return appContext;
    }

    // 这个方法用于在 Activity 中设置上下文（如果还没有 Application 类）
    public static void setAppContext(Context context) {
        appContext = context;
    }
}