<template>
    <div data-root>
        <h1 style="margin-top: 0;">串口设置</h1>
        <div class="row">
            <span>串口端口：</span>
            <ElSelect v-model="port" placeholder="请选择串口端口">
                <ElOption v-for="port in serialPorts" :label="port" :value="port"></ElOption>
            </ElSelect>
            <ElButton class="ml05" @click="scanPorts">重新扫描</ElButton>
        </div>
        <div class="row">
            <span>波特率：</span>
            <ElSelect v-model="baudRate" placeholder="请选择波特率">
                <ElOption v-for="rate in baudRates" :label="rate" :value="rate"></ElOption>
            </ElSelect>
            <ElButton class="ml05" @click="connectOrDisconnect">{{ isConnected ? '断开连接' : '连接' }}</ElButton>
        </div>
        <div class="row">
            <span class="mr05">请确认当前模组状态</span>
            <ElButton @click="toggleMode(0)">从AT模式切换到数据模式</ElButton>
            <ElButton @click="toggleMode(1)">从数据模式切换到AT模式</ElButton>
        </div>
        <fieldset style="margin-top: 0.5em;">
            <legend>数据模式</legend>
            <span>可以前往聊天页面进行聊天</span>
        </fieldset>
        <fieldset style="margin-top: 0.5em;">
            <legend>AT模式</legend>
            <div>操作:</div>
            <div class="row">
                <span>设置波特率:</span>
                <ElInput class="ml05" type="number" min="9600" max="115200" v-model="baudRateSet" />
                <ElButton class="ml05" @click="applyBR">应用</ElButton>
            </div>
            <div class="row">
                <span>设置模块 ID</span>
                <ElInput class="ml05" type="number" min="0" max="65535" v-model="moduleIDSet" />
                <ElButton class="ml05" @click="applyID">应用</ElButton>
            </div>
        </fieldset>
    </div>
</template>

<script setup>
import { ref } from 'vue';
import { ElMessage } from 'element-plus';

const serialPorts = ref([]);
const baudRate = ref("115200");
const baudRates = ref(["9600", "19200", "38400", "57600", "115200"]);
const moduleIDSet = ref("0");
const port = ref("");
const isConnected = ref(false);
const baudRateSet = ref("115200");

async function scanPorts() {
    ElMessage.info("正在扫描，请稍候…");
    try {
        const data = await (await fetch("/api/serial/scan")).json();
        serialPorts.value = data.ports;
        ElMessage.success("扫描到 " + data.ports.length + " 个串口端口");
    }
    catch (error) {
        ElMessage.error("扫描串口端口失败：" + error);
    }
}

async function connectOrDisconnect() {
    if (isConnected.value) {
        ElMessage.info("正在断开连接，请稍候…");
        try {
            await (await fetch("/api/serial/disconnect")).json();
            isConnected.value = false;
            ElMessage.success("已断开连接");
        }
        catch (error) {
            ElMessage.error("断开连接失败：" + error);
        }
    }
    else {
        ElMessage.info("正在连接，请稍候…");
        try {
            await (await fetch("/api/serial/connect", {
                method: "POST",
                body: JSON.stringify({
                    port: port.value,
                    baudRate: baudRate.value
                })
            })).json();
            isConnected.value = true;
            ElMessage.success("已连接到 " + port.value + "，波特率：" + baudRate.value);
        }
        catch (error) {
            ElMessage.error("连接失败：" + error);
        }
    }
}

async function toggleMode(mode) {
    if (!isConnected.value) {
        ElMessage.error("请先连接到串口");
        return;
    }
    ElMessage.info("正在切换模式，请稍候…");
    try {
        const { data } = await (await fetch("/api/serial/mode", {
            method: "POST",
            body: JSON.stringify({
                mode: mode
            })
        })).json();
        if (!data) {
            ElMessage.success("已切换到 " + (mode === 0 ? "数据模式" : "AT模式"));
        }
        else {
            ElMessage.error("切换模式失败: " + data);
        }
    }
    catch (error) {
        ElMessage.error("切换模式失败：" + error);
    }
}

async function applyBR() {
    if (!isConnected.value) {
        ElMessage.error("请先连接到串口");
        return;
    }
    ElMessage.info("正在应用波特率，请稍候…");
    try {
        const { data } = await (await fetch("/api/serial/baudrate", {
            method: "POST",
            body: JSON.stringify({
                baudRate: baudRateSet.value
            })
        })).json();
        if (!data) {
            ElMessage.success("已应用波特率 " + baudRateSet.value + '，请重新连接');
            connectOrDisconnect();
        }
        else {
            ElMessage.error("应用波特率失败: " + data);
        }
    }
    catch (error) {
        ElMessage.error("应用波特率失败：" + error);
    }
}

async function applyID() {
    if (!isConnected.value) {
        ElMessage.error("请先连接到串口");
        return;
    }
    ElMessage.info("正在应用模块 ID，请稍候…");
    try {
        const { data } = await (await fetch("/api/serial/moduleid", {
            method: "POST",
            body: JSON.stringify({
                moduleID: moduleIDSet.value
            })
        })).json();
        if (!data) {
            ElMessage.success("已应用模块 ID " + moduleIDSet.value);
        }
        else {
            ElMessage.error("应用模块 ID 失败: " + data);
        }
    }
    catch (error) {
        ElMessage.error("应用模块 ID 失败：" + error);
    }
}

</script>

<style scoped>
[data-root] {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: auto;
    padding: 1em;
}
.row {
    display: flex;
    flex-direction: row;
    white-space: nowrap;
    align-items: center;
}
.ml05 {
    margin-left: 0.5em;
}
.mr05 {
    margin-right: 0.5em;
}
.row+.row {
    margin-top: 0.5em;
}
</style>