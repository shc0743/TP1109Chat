<template>
    <div data-root>
        <div class="chat-title">
            <span>模式:</span>
            <ElRadioGroup :disabled="ws != null" v-model="chatMode" class="chat-mode-radio">
                <ElRadio value="bluetooth">蓝牙</ElRadio>
                <ElRadio value="serial">串口</ElRadio>
            </ElRadioGroup>
            <ElButton v-if="!ws" style="margin-left: 1em;" @click="openConnection">打开 WebSocket 连接</ElButton>
            <ElButton v-else style="margin-left: 1em;" @click="closeConnection">关闭 WebSocket 连接</ElButton>
        </div>

        <div class="chat-main-view">
            <div class="left-view" style="width: 200px;">
                <div class="chat-users-title" @click="switchUser(null)">
                    <span style="flex: 1;" title="点击此处或者按 Esc 可以关闭对话">好友列表</span>
                    <a href="javascript:" @click.prevent.stop="addUserDialog.showModal()">添加</a>
                </div>
                <div class="chat-users-list" @keydown.esc="switchUser(null)">
                    <div v-for="user in users" :key="user.id" role="link" :aria-label="user.name" tabindex="0"
                        :class="{'active': user.id === currentChatUser,'chat-user-item':true}"
                        @click="switchUser(user.id)" @keydown.enter.prevent="switchUser(user.id)">
                        <span>{{ user.name }}</span>
                        <!-- 使用 visibility 以便公共频道与其他元素有相同的高度 -->
                        <ElButton class="user-edit-button" :style="{ visibility: (user.id !== 0) ? 'visible' : 'hidden' }" text @click.prevent.stop="editUserInfo(user)"><el-icon><Edit /></el-icon></ElButton>
                    </div>
                </div>
            </div>
            <div class="right-view">
                <template v-if="currentChatUser === null || !currentChatUserInfo">
                    <!-- 显示一个占位符 -->
                    <div style="height: 100%; display: flex; align-items: center; justify-content: center;">
                        <span>请选择一个好友</span>
                    </div>
                </template>
                <template v-else>
                    <div class="chat-user-name-view">
                        <span>{{ currentChatUserInfo.name }}</span>
                    </div>
                </template>
            </div>
        </div>

        <dialog ref="addUserDialog" class="plain-dialog" @close="addUserAddr = addUserName = addUserPkey = addUserStatus = ''">
            <div class="dialog-content">
                <div style="font-weight: bold; margin-bottom: 0.5em; font-size: x-large; text-align: center;">{{ addUserStatus ? '编辑好友' : '添加好友' }}</div>
                <form @submit.prevent="handleAddUserSubmit">
                    <label class="form-group">
                        <span class="desc">好友名称:</span>
                        <ElInput v-model="addUserName" required autofocus="true"></ElInput>
                    </label>
                    <label class="form-group">
                        <span class="desc">好友地址:</span>
                        <ElInput v-model="addUserAddr" required></ElInput>
                    </label>
                    <label class="form-group">
                        <span class="desc">好友的 ECC 公钥:</span>
                        <ElInput v-model="addUserPkey" required></ElInput>
                    </label>
                    <div class="form-group action-buttons">
                        <ElButton v-if="addUserStatus" type="danger" plain @click="editUserInfo('delete')">删除此好友</ElButton>
                        <button class="el-button el-button--primary is-plain" type="submit">确定</button>
                        <ElButton @click="addUserDialog.close()">取消</ElButton>
                    </div>
                </form>
            </div>
        </dialog>
    </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { ElButton, ElMessage, ElMessageBox } from 'element-plus';
import { Edit } from '@element-plus/icons-vue';
import File from '../file';
import ECCCrypto from '../keys';

const chatMode = ref('bluetooth');
const ws = ref(null);
const addUserDialog = ref(null);
const addUserName = ref('');
const addUserAddr = ref('');
const addUserPkey = ref('');
const addUserStatus = ref('');
const users = ref([{
    id: 0,
    name: '公共频道'
}]);
const currentChatUser = ref(null);
const currentChatUserInfo = computed(() => {
    return users.value.find(user => user.id === currentChatUser.value);
});
const myKeysCrypto = ref(new ECCCrypto());
const myKeys = ref({
    pub: '',
    priv: '',
});

onMounted(() => {
    initUsers();
    initMyKeys();
});

async function openConnection() {
    if (ws.value) {
        ElMessage.error("请先关闭当前连接");
        return;
    }
    if (chatMode.value == 'serial') return ElMessage.error("暂未实现。");
    ws.value = new WebSocket('ws://' + location.hostname + ':' + location.port + '/ws/chat');
    ws.value.onopen = () => {
        ElMessage.success("WebSocket 连接已打开");
    };
    ws.value.onmessage = (event) => {
        ElMessage.info(`收到消息: ${event.data}`);
    };
    ws.value.onerror = (error) => {
        ElMessage.error(`WebSocket 错误: ${error}`);
        ws.value = null;
    };
    ws.value.onclose = () => {
        ElMessage.info("WebSocket 连接已关闭");
        ws.value = null;
    };
}

async function closeConnection() {
    if (!ws.value) {
        ElMessage.error("请先打开 WebSocket 连接");
        return;
    }
    ws.value.close();
}

async function initUsers() {
    try {
        if (!(await File.exists('users.json'))) {
            await File.writeJSON('users.json', users.value);
            return;
        }
        const usersJson = await File.readJSON('users.json');
        if (usersJson) users.value = usersJson;
    }
    catch (e) {
        ElMessage.error(`初始化好友列表失败: ${e}`);
    }
}

async function handleAddUserSubmit() {
    if (!addUserName.value || !addUserAddr.value || !addUserPkey.value) {
        ElMessage.error("请填写完整的信息");
        return;
    }
    if (!addUserStatus.value) {
        users.value.push({
            id: users.value.length,
            name: addUserName.value,
            addr: addUserAddr.value,
            pkey: addUserPkey.value,
        });
    } else {
        const index = users.value.findIndex(user => user.id === addUserStatus.value);
        if (index !== -1) {
            users.value[index].name = addUserName.value;
            users.value[index].addr = addUserAddr.value;
            users.value[index].pkey = addUserPkey.value;
        } else {
            ElMessage.error("指定的好友不存在");
            addUserDialog.value.close();
            return;
        }
    }
    addUserDialog.value.close();
    try {
        await File.writeJSON('users.json', users.value);
        if (!addUserStatus.value) ElMessage.success(`已添加好友 ${addUserName.value}`);
    }
    catch (e) {
        ElMessage.error(`保存好友列表失败: ${e}`);
    }
}

function switchUser(id) {
    currentChatUser.value = id;
}

async function editUserInfo(user) {
    if (user === 'delete') {
        addUserDialog.value.close();
        const index = users.value.findIndex(u => u.id === addUserStatus.value);
        if (index !== -1) {
            try {
                await ElMessageBox.confirm("确认要删除这个好友吗？\n" + users.value[index]?.name, "删除好友", {
                    confirmButtonText: "确定",
                    cancelButtonText: "取消",
                    type: "warning",
                });
            } catch { return; }
            users.value.splice(index, 1);
            ElMessage.success(`已删除好友 ${addUserName.value}`);
            // 保存好友列表
            try {
                await File.writeJSON('users.json', users.value);
            }
            catch (e) {
                ElMessage.error(`保存好友列表失败: ${e}`);
            }
            return;
        }
        ElMessage.error("指定的好友不存在");
        return;
    }
    addUserName.value = user.name;
    addUserAddr.value = user.addr;
    addUserPkey.value = user.pkey;
    addUserStatus.value = user.id;
    addUserDialog.value.showModal();
}

async function initMyKeys() {
    try {
        // 尝试加载已保存的密钥
        const savedKeys = await File.readJSON('keys.json');
        if (savedKeys && savedKeys.pub && savedKeys.priv) {
            // 导入已存在的密钥
            await myKeysCrypto.value.importKeyPair(savedKeys.pub, savedKeys.priv);
            myKeys.value = savedKeys;
            console.log("密钥加载成功");
        } else {
            // 首次使用，生成新密钥
            await generateNewKeys();
        }
    } catch (error) {
        console.log("首次使用或密钥文件不存在，生成新密钥:", error);
        await generateNewKeys();
    }
}

async function generateNewKeys() {
    try {
        // 生成新密钥对
        await myKeysCrypto.value.generateKeyPair();
        
        // 导出密钥
        myKeys.value.pub = await myKeysCrypto.value.exportPublicKey();
        myKeys.value.priv = await myKeysCrypto.value.exportPrivateKey();
        
        // 保存到文件
        await File.writeJSON('keys.json', myKeys.value);
        console.log("新密钥生成并保存成功");
    } catch (error) {
        console.error("密钥生成失败:", error);
        ElMessage.error("密钥生成失败");
    }
}
</script>

<style scoped>
[data-root] {
    font-family: Consolas, NSimsun, monospace;
    overflow: hidden;
}
.chat-title {
    display: flex;
    align-items: center;
    margin-bottom: 0.5em;
}
.chat-title > span {
    margin: 0 1em;
}
.chat-title > span:nth-child(1) {
    margin-left: 0;
}
.chat-main-view {
    flex: 1;
    display: flex;
    flex-direction: row;
    overflow: hidden;
}
.chat-main-view > * {
    overflow: hidden;
}
.left-view {
    min-width: 100px;
    margin-right: 10px;
    padding-right: 10px;
    border-right: 1px solid;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}
.right-view {
    flex: 1;
    background-color: #f0f0f0;
}
.chat-users-title {
    margin-bottom: 0.5em;
    background-color: #f7f7f7;
    padding: 10px;
    display: flex;
    align-items: center;
    white-space: nowrap;
}
.chat-users-list {
    overflow-y: auto;
    overflow-x: hidden;
}
.chat-users-list::-webkit-scrollbar {
    width: 8px;
}
.chat-users-list::-webkit-scrollbar-thumb {
    background-color: rgba(0, 0, 0, 0.2);
    border-radius: 10px;
}
.chat-users-list::-webkit-scrollbar-track {
    background-color: transparent;
}
.chat-user-item {
    padding: 10px;
    border: 1px solid #ccc;
    border-radius: 10px;
    cursor: pointer;
    transition: all .2s;
    overflow: hidden;
    display: flex;
    align-items: center;
    justify-content: space-between;
    white-space: nowrap;
}
.chat-user-item > span {
    overflow: hidden;
    text-overflow: ellipsis;
}
.chat-user-item + .chat-user-item {
    margin-top: 0.5em;
}
.chat-user-item:hover {
    background-color: #f0f0f0;
}
.chat-user-item:active, .chat-user-item.active {
    background-color: #e0e0e0;
}
.form-group {
    display: flex;
    flex-direction: column;
    white-space: nowrap;
}
.form-group.action-buttons {
    flex-direction: row;
    justify-content: flex-end;
}
.form-group + .form-group {
    margin-top: 0.5em;
}
.chat-user-name-view {
    background-color: #fff;
    padding: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    border: 1px solid #ccc;
    overflow: hidden;
}
.chat-user-name-view > span {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.user-edit-button {
    opacity: 0;
    transition: all .2s;
}
.chat-user-item:hover .user-edit-button,
.chat-user-item:focus .user-edit-button,
.chat-user-item.active .user-edit-button,
.user-edit-button:focus {
    opacity: 1;
}
</style>