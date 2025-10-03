<template>
    <div data-root>
        <h1 style="margin-top: 0;">串口设置</h1>
        <div class="row">
            <span>串口端口：</span>
            <ElSelect :disabled="isConnected" v-model="port" placeholder="请选择串口端口" @focus="scanPorts">
                <ElOption v-for="port in serialPorts" :label="port" :value="port"></ElOption>
            </ElSelect>
            <ElButton :disabled="isConnected" class="ml05" @click="scanPorts">重新扫描</ElButton>
        </div>
        <div class="row">
            <span>波特率：</span>
            <ElSelect :disabled="isConnected" v-model="baudRate" placeholder="请选择波特率">
                <ElOption v-for="rate in baudRates" :label="rate" :value="rate"></ElOption>
            </ElSelect>
            <ElButton class="ml05" @click="connectOrDisconnect">{{ isConnected ? '断开连接' : '连接' }}</ElButton>
        </div>
        <div class="row" style="overflow-x: auto;">
            <span class="mr05">请确认当前模组状态</span>
            <ElButton :disabled="!isConnected" @click="toggleMode(1)">从AT模式切换到数据模式</ElButton>
            <ElButton :disabled="!isConnected" @click="toggleMode(2)">从AT模式切换到定向数据模式</ElButton>
            <ElButton :disabled="!isConnected" @click="toggleMode(0)">从数据模式切换到AT模式</ElButton>
        </div>
        <fieldset style="margin-top: 0.5em;" :disabled="!isConnected">
            <legend>数据模式</legend>
            <span>可以前往聊天页面进行聊天</span>
        </fieldset>
        <fieldset style="margin-top: 0.5em;" :disabled="!isConnected">
            <legend>AT模式</legend>
            <div class="row">操作:</div>
            <div class="row" hidden>
                <span>设置波特率:</span>
                <ElInput :disabled="!isConnected" class="ml05" type="number" min="9600" max="115200" v-model="baudRateSet" />
                <ElButton :disabled="!isConnected" class="ml05" @click="applyBR">应用</ElButton>
            </div>
            <div class="row">
                <span>设置模块地址</span>
                <ElInput :disabled="!isConnected" class="ml05" type="number" min="0" max="65535" v-model="moduleIDSet" />
                <ElButton :disabled="!isConnected" class="ml05" @click="applyID">应用</ElButton>
            </div>
        </fieldset>
        <fieldset style="margin-top: 0.5em;" :disabled="!isConnected">
            <legend>串口调试</legend>
            <div class="row">
                <ElCheckbox :disabled="!isConnected" class="ml05" v-model="autoCRLF">AT指令自动回车</ElCheckbox>
            </div>
            <div class="row">
                <span>发送数据:</span>
                <ElInput :disabled="!isConnected" class="ml05" style="flex: 1;" v-model="sendData" type="textarea" autosize @keydown.enter="ev => { if (ev.shiftKey) return; ev.preventDefault(); execSendData(); }" />
                <ElButton :disabled="!isConnected" class="ml05" @click="execSendData">发送</ElButton>
            </div>
            <div style="margin-top: 0.5em; display: flex; flex-direction: column;">
                <div style="display: flex; align-items: center;">
                    <span>接收数据:</span>
                    <ElButton :disabled="!isConnected" class="ml05" @click="execRecvData">手动接收</ElButton>
                    <ElButton :disabled="!isConnected" class="ml05" @click="recvData = ''">清空</ElButton>
                    <ElCheckbox :disabled="!isConnected" class="ml05" v-model="autoRecvAfterSent">发送后自动接收</ElCheckbox>
                    <ElCheckbox :disabled="!isConnected" class="ml05" v-model="autoRecv">定时器自动接收</ElCheckbox>
                </div>
                <ElInput :disabled="!isConnected" class="mt05" v-model="recvData" type="textarea" autosize readonly style="width: 100%;" />
            </div>
        </fieldset>
    </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { ElCheckbox, ElMessage } from 'element-plus';

const serialPorts = ref([]);
const baudRate = ref("115200");
const baudRates = ref(["9600", "19200", "38400", "57600", "115200"]);
const moduleIDSet = ref("0");
const sendData = ref("");
const recvData = ref("");
const port = ref("");
const isConnected = ref(false);
const baudRateSet = ref("115200");
const autoRecv = ref(false);
const autoRecvAfterSent = ref(true);
const autoRecvTimerId = ref(0);
const autoCRLF = ref(true);

onMounted(() => {
    autoRecvTimerId.value = setInterval(() => {
        if (autoRecv.value && isConnected.value) {
            execRecvData({ ignoreEmpty: true });
        }
    }, 1000);
});
onBeforeUnmount(() => {
    clearInterval(autoRecvTimerId.value);
    autoRecvTimerId.value = 0;
});

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
            await (await fetch("/api/serial/disconnect", {
                method: "POST"
            })).json();
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
                    text1: port.value,
                    number1: +baudRate.value
                })
            })).json();
            isConnected.value = true;
            ElMessage.success("已连接到 " + port.value + "，波特率：" + baudRate.value);

            // 查询相关信息
            const { data, error } = await (await fetch("/api/serial/moduleaddr")).json();
            if (data) {
                // 提取数字地址
                const match = data.match(/Addr:(\d+)/);
                if (match) {
                    moduleIDSet.value = match[1];
                }
                else {
                    ElMessage.error("无法提取模块地址: " + data + ", 请再次确认设置的波特率是否正确");
                }
            }
            else {
                // 可能当前处于数据模式，忽略错误
                ElMessage.info("串口通信失败: " + error + ", 可能是波特率不正确或者当前处于数据模式");
            }
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
            body: JSON.stringify({ number1: +mode })
        })).json();
        if (!data) {
            ElMessage.success("已切换");
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
                number1: +baudRateSet.value
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
        const { data } = await (await fetch("/api/serial/moduleaddr", {
            method: "POST",
            body: JSON.stringify({
                number1: +moduleIDSet.value
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

async function execSendData() {
    if (!isConnected.value) {
        ElMessage.error("请先连接到串口");
        return;
    }
    if (!sendData.value) {
        ElMessage.warning("请输入要发送的数据");
        return;
    }
    try {
        const command = sendData.value;
        const text1 = command.replace(/\n/g, '\r\n') + (autoCRLF.value ? (/^at/i.test(command) && !/\n$/.test(command) ? '\r\n' : '') : '');
        const { data } = await (await fetch("/api/serial/send", {
            method: "POST",
            body: JSON.stringify({ text1 })
        })).json();
        if (!data) {
            sendData.value = "";
            if (autoRecvAfterSent.value) {
                setTimeout(() => execRecvData({ ignoreEmpty: true }), 100);
            }
        }
        else {
            ElMessage.error("发送数据失败: " + data);
        }
    }
    catch (error) {
        ElMessage.error("发送数据失败：" + error);
    }
}

async function execRecvData({ ignoreEmpty = false } = {}) {
    if (!isConnected.value) {
        ElMessage.error("请先连接到串口");
        return;
    }
    try {
        const { data } = await (await fetch("/api/serial/recv")).json();
        if (!data) {
            if (!ignoreEmpty) ElMessage.info("无数据")
        }
        else {
            recvData.value = data;
        }
    }
    catch (error) {
        ElMessage.error("接收数据失败：" + error);
    }
}
</script>

<style scoped>

</style>