<script setup>
import { ref, markRaw, onMounted } from 'vue';
import ChatActivity from './components/ChatActivity.vue';
import BluetoothActivity from './components/BluetoothActivity.vue';
import SerialActivity from './components/SerialActivity.vue';
import SettingsActivity from './components/SettingsActivity.vue';
import File from './file';
import { ElMessage } from 'element-plus';

const page = ref('chat')
const pages = ref([
    ['chat', '聊天', markRaw(ChatActivity)],
    ['blue', '蓝牙', markRaw(BluetoothActivity)],
    ['seri', '串口', markRaw(SerialActivity)],
    ['sett', '设置', markRaw(SettingsActivity)],
]);
const profileSelector = ref(null);
const profile = ref('');

function finalizeProfileSelector(accept) {
    if (!accept) {
        window.close();
    }
    if (profile.value.includes("/") || profile.value.includes("\\")) {
        ElMessage.error('配置文件名称不允许包含 / 或 \\');
        return;
    }
    // File.setProfile(profile.value);
    // profileSelector.value.close();
    sessionStorage.setItem("profile", profile.value);
    location.reload();
}

onMounted(() => {
    if (location.search.includes("select_profile=true")) {
        if (null === sessionStorage.getItem("profile")) {
            profileSelector.value.showModal();
        }
    }
})
</script>

<template>
    <div data-app-root>
        <nav>
            <a class="btn" v-for="item in pages" :key="item[0]" href="javascript:void(0)" :data-current="page === item[0]" @click="page = item[0]">{{ item[1] }}</a>
        </nav>

        <div class="app-area">
            <template v-for="item in pages" :key="item[0]">
                <component :is="item[2]" v-show="page === item[0]"/>
            </template>
        </div>

        <dialog ref="profileSelector" class="plain-dialog" @cancel="finalizeProfileSelector(false)">
            <div style="font-size: 1.5em; text-align: center;">选择配置文件</div>
            <ElInput @keydown.enter.prevent="finalizeProfileSelector(true)" style="margin: 0.5em 0;" autofocus v-model="profile" placeholder="默认配置文件" clearable />
            <div style="color: red;">注意: 请勿使用多个实例访问同一个配置文件</div>
            <div style="display: flex; margin-top: 0.5em;">
                <ElButton style="flex: 1;" type="success" plain @click="finalizeProfileSelector(true)">确定</ElButton>
                <ElButton style="flex: 1;" type="danger" plain @click="finalizeProfileSelector(false)">取消</ElButton>
            </div>
        </dialog>
    </div>
</template>

<style scoped>
[data-app-root] {
    position: absolute;
    inset: 0;
    overflow: hidden;
    display: flex;
    flex-direction: column;
}
nav {
    display: flex;
    flex-direction: row;
    justify-content: center;
    align-items: center;
    margin: 0.5em 0;
}
nav .btn {
    margin: 0 0.5em;
    padding: 0.5em 1em;
    border: 1px solid #000;
    border-radius: 0.5em;
    color: #000;
    transition: all .2s;
}
nav .btn:hover {
    background-color: #f0f0f0;
    text-decoration: none;
}
nav .btn[data-current="true"] {
    background-color: #000;
    color: #fff;
}
.app-area {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
}
</style>
