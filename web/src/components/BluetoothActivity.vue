<template>
    <div data-root>
        <h1 style="margin-top: 0;">蓝牙连接</h1>
        <div class="row">
            <span>蓝牙设备：</span>
            <ElSelect :disabled="isConnected" v-model="device" placeholder="请选择蓝牙设备">
                <ElOption v-for="dev in devices" :label="getDevLabel(dev)" :value="dev.index"></ElOption>
            </ElSelect>
            <ElButton :disabled="isConnected" style="margin-left: 12px;" @click="scanDevices">扫描</ElButton>
            <ElButton class="ml05" @click="connectOrDisconnect">{{ isConnected ? '断开连接' : '连接' }}</ElButton>
        </div>
        <div class="row">
            <span>UUID参考:</span>
        </div>
        <div class="row uuid-ref">
            <span>服务UUID:</span>
            <span v-text="serviceUUID"></span>
        </div>
        <div class="row uuid-ref">
            <span>特征UUID:</span>
            <span v-text="characteristicUUID"></span>
        </div>
        <div class="row uuid-ref">
            <span>通知UUID:</span>
            <span v-text="notificationUUID"></span>
        </div>
        <dialog ref="progress" class="progress-dialog plain-dialog large-round-corner" @cancel="handleDialogCancel">
            <div class="progress-dialog-title">请等待...</div>
            <div style="display: flex; flex-direction: column; align-items: center; justify-content: center;">
                <ElProgress :percentage="progressPercentage" type="circle"></ElProgress>
            </div>
            <div class="progress-dialog-timer" v-text="progressTimeLeft.toFixed(0)"></div>
        </dialog>
    </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { ElMessage, ElProgress } from 'element-plus';

const devices = ref([]);
const device = ref('');
const isConnected = ref(false);
const progress = ref(null);
const progressTimeLeft = ref(0);
const progressTimerId = ref(null);
const progressPercentage = computed(() => {
    const v = Math.floor((10 - progressTimeLeft.value) / 10 * 100);
    return (0 <= v && v <= 100) ? v : 100;
});
const serviceUUID = ref('');
const characteristicUUID = ref('');
const notificationUUID = ref('');

onMounted(() => {
    fetch('/api/bluetooth/uuid')
        .then(res => res.json())
        .then(data => {
            serviceUUID.value = data.serviceUUID;
            characteristicUUID.value = data.characteristicUUID;
            notificationUUID.value = data.notificationUUID;
        })
        .catch(err => {
            ElMessage.error("获取UUID失败: " + err);
        });
})

async function scanDevices() {
    if (isConnected.value) {
        ElMessage.error("请先断开当前连接");
        return;
    }
    ElMessage.info("正在扫描蓝牙设备，请稍候…");
    progress.value.showModal();
    progressTimeLeft.value = 10;
    progressTimerId.value = setInterval(() => {
        progressTimeLeft.value -= 0.1;
    }, 100);
    try {
        // await new Promise(r => setTimeout(r, 12000)); // for debug purpose only
        const { devices: scannedDevices, error } = await (await fetch("/api/bluetooth/scandevices")).json();
        if (!scannedDevices || error) {
            ElMessage.error("扫描蓝牙设备失败: " + (error || "未知错误"));
            return;
        }
        if (scannedDevices.length === 0) {
            ElMessage.info("未发现蓝牙设备");
        }
        else {
            ElMessage.success(`发现 ${scannedDevices.length} 个蓝牙设备`);
        }
        devices.value = scannedDevices;
        device.value = '';
    }
    catch (error) {
        ElMessage.error("扫描蓝牙设备失败：" + error);
    }
    finally {
        progress.value.close();
        clearInterval(progressTimerId.value);
        progressTimerId.value = null;
    }
}

function handleDialogCancel() {
    setTimeout(() => {
        if (progress.value) progress.value.showModal();
    });
}

function getDevLabel(dev) {
    return `${dev.index}: ${dev.name} rssi=${dev.rssi} addr=0x${dev.addr.toString(16).padStart(16, '0')}`;
}

async function connectOrDisconnect() {
    try {
        if (!isConnected.value) {
            const { success, error } = await (await fetch("/api/bluetooth/connect", {
                method: "POST",
                body: JSON.stringify({ text1: 'n/a' })
            })).json();
            if (success) {
                isConnected.value = true;
                ElMessage.success("已连接");
                return;
            }
        }
    } catch {}
    if (!devices.value || !device.value) {
        ElMessage.error("请先选择蓝牙设备");
        return;
    }
    const addr = devices.value[device.value].addr;
    if (!addr) {
        ElMessage.error("请先选择蓝牙设备");
        return;
    }
    if (isConnected.value) {
        ElMessage.info("正在断开连接，请稍候…");
        try {
            await (await fetch("/api/bluetooth/disconnect", { method: "POST" })).json();
            isConnected.value = false;
            ElMessage.success("已断开连接");
        }
        catch (error) {
            ElMessage.error("断开连接失败：" + error);
        }
        return;
    }
    progress.value.showModal();
    progressTimeLeft.value = 10;
    progressTimerId.value = setInterval(() => {
        progressTimeLeft.value -= 0.1;
        progressPercentage.value = progressTimeLeft.value / 10 * 100;
    }, 100);
    try {
        const { success, error } = await (await fetch("/api/bluetooth/connect", {
            method: "POST",
            body: JSON.stringify({
                text1: addr,
            })
        })).json();
        if (!success || error) {
            ElMessage.error("连接到 " + getDevLabel(devices.value[device.value]) + " 失败: " + (error || "未知错误"));
            return;
        }
        isConnected.value = true;
        ElMessage.success("已连接到 " + getDevLabel(devices.value[device.value]));
    }
    catch (error) {
        ElMessage.error("连接到 " + getDevLabel(devices.value[device.value]) + " 失败：" + error);
    }
    finally {
        progress.value.close();
        clearInterval(progressTimerId.value);
        progressTimerId.value = null;
    }
}
</script>

<style scoped>
.progress-dialog {
    width: 300px;
}
.progress-dialog-title {
    font-size: 24px;
    font-weight: bold;
    text-align: center;
    margin-bottom: 1em;
}
.progress-dialog-timer {
    font-size: 20px;
    color: red;
    text-align: right;
    margin-top: 1em;
}
.uuid-ref > span+span {
    margin-left: 1em;
    font-family: Consolas, NSimsun, monospace;
    user-select: all;
    background-color: #f0f0f0;
    border: 1px solid #aaa;
    border-radius: 5px;
    padding: 10px;
}
.uuid-ref + .uuid-ref {
    margin-top: 0.5em;
}
</style>