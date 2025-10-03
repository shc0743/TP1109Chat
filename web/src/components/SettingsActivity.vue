<template>
    <div data-root>
        <h1 style="margin-top: 0;">应用程序设置</h1>

        <fieldset class="install-and-uninstall">
            <legend>安装与卸载</legend>
            <div v-if="installed === true">
                <p>应用程序已安装</p>
                <ElButton :disabled="isInstalling" type="danger" plain @click="setupTools('uninstall')">卸载应用程序</ElButton>
            </div>
            <div v-else-if="installed === false">
                <p>应用程序未安装</p>
                <div class="row" style="margin-bottom: 0.5em;">
                    <span class="mr05">安装位置:</span>
                    <ElInput :disabled="isPicking || isInstalling" v-model="installLocation" placeholder="可选" />
                    <ElButton :disabled="isPicking || isInstalling" class="ml05" @click="selectInstallLocation">选取...</ElButton>
                </div>
                <ElButton :disabled="isInstalling || isPicking" type="primary" plain @click="setupTools('install', false)">安装应用程序 (仅为我)</ElButton>
                <ElButton :disabled="isInstalling || isPicking" type="primary" plain @click="setupTools('install', true)">安装应用程序 (所有用户)</ElButton>
            </div>
            <div v-else>
                <p>检查安装状态中...</p>
            </div>
        </fieldset>

        <fieldset>
            <legend>配置文件</legend>
            <div>当前配置文件: <ElButton @click="getProfile">{{ (currentProfile == null) ? "点击获取" : (currentProfile || "默认配置文件") }}</ElButton></div>
            <ElButton @click="setProfile">选择配置文件</ElButton>
        </fieldset>
    </div>
</template>

<script setup>
import { onMounted, ref } from 'vue';
import { ElMessage } from 'element-plus';
import File from '../file';

const installed = ref(null);
const isInstalling = ref(false);
const installLocation = ref('');
const isPicking = ref(false);
const currentProfile = ref(null);

onMounted(() => {
    fetch("/api/settings/installed").then(res => res.json()).then(data => {
        installed.value = data.installed;
    }).catch(error => {
        ElMessage.error("获取安装状态失败：" + error);
    });
})

function setupTools(action, systemwide) {
    if (action !== 'install' && action !== 'uninstall') {
        ElMessage.error("无效的操作：" + action);
        return;
    }
    isInstalling.value = true;
    fetch("/api/settings/" + action, {
        method: 'POST', body: JSON.stringify({
            number1: systemwide ? 1 : 0,
            text1: installLocation.value,
        })
    }).then(res => res.json()).then(data => {
        if (data.success) {
            window.close();
        } else {
            ElMessage.error((action === 'install' ? "安装" : "卸载") + "失败：" + data.error);
        }
    }).catch(error => {
        ElMessage.error((action === 'install' ? "安装" : "卸载") + "失败：" + error);
    }).finally(() => {
        isInstalling.value = false;
    });
}
function selectInstallLocation() {
    isPicking.value = true;
    fetch('/api/pick-directory').then(v => {
        if (!v.ok) throw 'cancel';
        return v.json()
    }).then(({ value }) => {
        if (value) installLocation.value = value;
    }).catch(() => { }).finally(() => {
        isPicking.value = false;
    });
}

function getProfile() {
    currentProfile.value = File.getProfile();
}
function setProfile() {
    sessionStorage.removeItem("profile");
    history.replaceState({}, "", "/?select_profile=true");
    location.reload();
}
</script>

<style scoped>
.install-and-uninstall p {
    margin: 0.5em 0;
}
.install-and-uninstall p:nth-child(1) {
    margin-top: 0;
}
</style>
