<script setup>
import { ref, markRaw } from 'vue';
import ChatActivity from './components/ChatActivity.vue';
import BluetoothActivity from './components/BluetoothActivity.vue';
import SerialActivity from './components/SerialActivity.vue';
import SettingsActivity from './components/SettingsActivity.vue';

const page = ref('chat')
const pages = ref([
    ['chat', '聊天', markRaw(ChatActivity)],
    ['blue', '蓝牙', markRaw(BluetoothActivity)],
    ['seri', '串口', markRaw(SerialActivity)],
    ['sett', '设置', markRaw(SettingsActivity)],
]);
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
