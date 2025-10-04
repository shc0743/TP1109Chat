<template>
    <div data-root>
        <div class="chat-title">
            <ElButton class="mr05 btn-menu" @click="friendsMenu = true"><el-icon>
                    <Expand />
                </el-icon></ElButton>
            <span>模式:</span>
            <ElRadioGroup :disabled="ws != null" v-model="chatMode" class="chat-mode-radio">
                <ElRadio value="bluetooth">蓝牙</ElRadio>
                <ElRadio value="serial">串口</ElRadio>
            </ElRadioGroup>
            <ElButton v-if="!ws" style="margin-left: 1em;" @click="openConnection">打开 WebSocket 连接</ElButton>
            <ElButton v-else style="margin-left: 1em;" @click="closeConnection">关闭 WebSocket 连接</ElButton>
            <ElButton style="margin-left: 1em;" @click="ElMessageBox.prompt('我的公钥', {
                inputValue: myKeys.pub,
            })">我的公钥</ElButton>
            <span style="margin-left: 1em;">注意: 加密功能尚未完成!! 数据将明文传输!</span>
            <ElButton style="margin-left: 1em;" @click="updateNickname" @contextmenu.prevent="notes = myNickname">我的昵称
            </ElButton>
        </div>

        <div class="chat-main-view">
            <div class="left-view" style="width: 200px;" :data-open="friendsMenu">
                <div class="chat-users-title" @click="switchUser(null)">
                    <span style="flex: 1;" title="点击此处或者按 Esc 可以关闭对话">好友列表</span>
                    <a href="javascript:" @click.prevent.stop="addUserDialog.showModal()">添加</a>
                </div>
                <div class="chat-users-list" @keydown.esc="switchUser(null)">
                    <div v-for="user in users" :key="user.id" role="link" :aria-label="user.name" tabindex="0"
                        :class="{'active': user.id === currentChatUser,'chat-user-item':true}" :title="user.id"
                        @click="switchUser(user.id)" @keydown.enter.prevent="switchUser(user.id)">
                        <span>{{ user.name }}</span>
                        <!-- 使用 visibility 以便公共频道与其他元素有相同的高度 -->
                        <ElButton class="user-edit-button"
                            :style="{ visibility: (user.id !== 0) ? 'visible' : 'hidden' }" text
                            @click.prevent.stop="editUserInfo(user)"><el-icon>
                                <Edit />
                            </el-icon></ElButton>
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
                        <ElButton text @click="userOptionDialog.showModal()"><el-icon>
                                <MoreFilled />
                            </el-icon></ElButton>
                    </div>

                    <div class="chat-messages-container" ref="messageArea" :data-is-multiple="multiSelection">
                        <template v-for="msg in messages" :key="msg.id">
                            <ChatMessageRenderer :message="msg" :is-multiple-selection="multiSelection"
                                @delete-message="handleDeleteMessage" @open-avatar="handleOpenAvatar"
                                @start-multiple-selection="multiSelection = true"
                                @update-selection="(selected) => msg.isSelected = selected" />
                        </template>
                    </div>

                    <div class="chat-multiple-functions" v-if="multiSelection">
                        <ElButton @click="multiSelection = false"><el-icon>
                                <Close />
                            </el-icon></ElButton>
                        <ElButton type="danger" plain @click="handleDeleteSelectedMessages">删除选中消息</ElButton>
                    </div>

                    <div class="chat-input">
                        <ElInput v-model="inputMessage.text" placeholder="输入消息" type="textarea" :rows="2" clearable
                            @keydown.enter="ev => {
                            if (ev.shiftKey) return;
                            ev.preventDefault();
                            sendMessage();
                        }"></ElInput>
                        <ElButton type="primary" plain @click="sendMessage">发送</ElButton>
                        <ElButton><el-icon>
                                <Plus />
                            </el-icon></ElButton>
                    </div>
                </template>
            </div>
        </div>

        <div class="notes-view" v-show="!!notes">
            <div class="notes-content" v-text="notes"></div>
        </div>

        <dialog ref="addUserDialog" class="plain-dialog" style="width: 250px;"
            @close="addUserAddr = addUserName = addUserPkey = addUserStatus = ''">
            <div class="dialog-content">
                <div style="font-weight: bold; margin-bottom: 0.5em; font-size: x-large; text-align: center;">{{
                    addUserStatus ? '编辑好友' : '添加好友' }}</div>
                <form @submit.prevent="handleAddUserSubmit">
                    <label class="form-group">
                        <span class="desc">好友名称:</span>
                        <ElInput v-model="addUserName" required autofocus></ElInput>
                    </label>
                    <label class="form-group">
                        <span class="desc">好友地址(建议填0):</span>
                        <ElInput v-model="addUserAddr" required></ElInput>
                    </label>
                    <label class="form-group">
                        <span class="desc">好友的 ECC 公钥:</span>
                        <ElInput v-model="addUserPkey" required></ElInput>
                    </label>
                    <div class="form-group action-buttons">
                        <ElButton v-if="addUserStatus" type="danger" plain @click="editUserInfo('delete')">删除此好友
                        </ElButton>
                        <button class="el-button el-button--primary is-plain" type="submit">确定</button>
                        <ElButton @click="addUserDialog.close()">取消</ElButton>
                    </div>
                </form>
            </div>
        </dialog>

        <dialog ref="userOptionDialog" class="plain-dialog" style="width: 250px;">
            <div class="dialog-content">
                <div class="col" style="display: flex; flex-direction: column;">
                    <ElButton type="danger" plain @click="(clearCurrentChatHistory(), userOptionDialog.close())">清空聊天记录
                    </ElButton>
                    <ElButton @click="userOptionDialog.close()">取消</ElButton>
                </div>
            </div>
        </dialog>

        <dialog ref="userProfileDialog" class="plain-dialog" style="min-width: 250px; max-width: calc(100% - 50px); box-sizing: border-box;">
            <div class="dialog-content">
                <div class="user-profile-dialog-title">{{ userProfileDialogData.name || userProfileDialogData.nickname }}</div>
                <div v-if="(userProfileDialogData.name && userProfileDialogData.nickname)" class="user-profile-dialog-nickname">昵称: {{ userProfileDialogData.nickname }}</div>
                <div class="form-group">
                    <span class="desc">{{ userProfileDialogData.isFriend ? "好友" : "用户" }}的 ECC 公钥:</span>
                    <ElInput :modelValue="userProfileDialogData.pkey" readonly @update:modelValue="() => { }" />
                </div>
                <div class="form-group action-buttons">
                    <ElButton @click="userProfileDialog.close()">取消</ElButton>
                </div>
            </div>
        </dialog>
    </div>
</template>

<script setup>
import { computed, nextTick, onMounted, ref } from 'vue';
import { ElButton, ElMessage, ElMessageBox } from 'element-plus';
import { Edit, Plus, MoreFilled, Expand, Close } from '@element-plus/icons-vue';
import File from '../file';
import ECCCrypto from '../keys';
import { FragmentManager, PacketBuffer, PacketDecoder, PacketEncoder, PROTOCOL } from '../protocol';
import ChatMessageRenderer from './ChatMessageRenderer.vue';

const props = defineProps({
    platform: {
        type: Object,
        default: () => ({})
    }
});

const chatMode = ref('bluetooth');
const ws = ref(null);
const userOptionDialog = ref(null);
const addUserDialog = ref(null);
const addUserName = ref('');
const addUserAddr = ref('');
const addUserPkey = ref('');
const addUserStatus = ref('');
const users = ref([{
    id: 0,
    name: '公共频道',
    addr: 0,
    pkey: '*',
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
const myNickname = ref('');
const fragmentManager = ref(new FragmentManager());
const packetBuffer = ref(new PacketBuffer());
const currentMessageId = ref(0);
const messages = ref([]);
const inputMessage = ref({
    text: ''
});
const messageArea = ref(null);
const friendsMenu = ref(false);
const notesTimer = ref(null);
const notesData = ref('');
const notes = computed({
    get: () => notesData.value,
    set: (val) => {
        notesData.value = val;
        if (notesTimer.value) clearTimeout(notesTimer.value);
        notesTimer.value = setTimeout(() => {
            notesData.value = '';
            notesTimer.value = null;
        }, 3000);
    }
});
const multiSelection = ref(false);
const userProfileDialog = ref(null);
const userProfileDialogData = ref({});

onMounted(() => {
    initUsers();
    initMyKeys();
    // 初始化昵称
    File.readText('nickname.txt').then(nick => {
        if (nick) myNickname.value = nick;
        else updateNickname();
    }).catch(() => updateNickname());
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
    ws.value.onmessage = async (event) => {
        try {
            // 追加数据到缓冲区
            packetBuffer.value.append(await event.data.arrayBuffer());

            // 提取所有完整数据包
            const packets = packetBuffer.value.extractPackets();

            for (const packetData of packets) {
                const packet = PacketDecoder.decode(packetData.buffer);

                if (packet.type === PROTOCOL.TYPE.DATA) {
                    fragmentManager.value.processFragment(packet, (completeMessage) => {
                        const jsonString = new TextDecoder().decode(completeMessage.data);
                        const { d, t, u } = JSON.parse(jsonString);
                        if (!d || !t || !u) return console.warn('无效消息:', jsonString);
                        handleNewMessage(d, t, u);
                    });
                } else if (packet.type === PROTOCOL.TYPE.ACK) {
                    console.log('收到ACK');
                }
            }
        } catch (error) {
            console.warn('处理消息时出错:', error);
        }
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

async function sendData(d, targetAddress) {
    if (!ws.value) return;

    // 序列化消息
    const encoder = new TextEncoder();
    const data = encoder.encode(JSON.stringify(d));

    // 创建分片
    const senderId = crypto.getRandomValues(new Uint16Array(1))[0];
    const fragments = fragmentManager.value.createFragments(
        senderId, // 发送者ID
        ++currentMessageId.value,
        data
    );

    // 发送所有分片
    for (const i of fragments) {
        const packetData = PacketEncoder.encodeWithTargetId(targetAddress, i);
        ws.value.send(packetData);
        await new Promise(r => setTimeout(r, 100)); // 等待
    }
}

async function initUsers(count = 0) {
    try {
        if (!(await File.exists('users.json')) || !(await File.info('users.json'))?.size) {
            await File.writeJSON('users.json', users.value);
            return;
        }
        const usersJson = await File.readJSON('users.json');
        if (Array.isArray(usersJson)) users.value = usersJson;
        else ElMessage.error("好友列表文件格式错误");
    }
    catch (e) {
        if (count < 3) {
            await new Promise(r => setTimeout(r, 10)); // 等待
            initUsers(count + 1); // 重试3次
            return;
        }
        ElMessage.error(`初始化好友列表失败: ${e}`);
    }
}

async function handleAddUserSubmit() {
    if (!addUserName.value || !addUserAddr.value || !addUserPkey.value || Number.isNaN(+addUserAddr.value)) {
        ElMessage.error("请填写完整有效的信息");
        return;
    }
    if (!addUserStatus.value) {
        users.value.push({
            id: crypto.randomUUID(),
            name: addUserName.value,
            addr: +addUserAddr.value,
            pkey: addUserPkey.value,
        });
    } else {
        const index = users.value.findIndex(user => user.id === addUserStatus.value);
        if (index !== -1) {
            users.value[index].name = addUserName.value;
            users.value[index].addr = +addUserAddr.value;
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

async function switchUser(id) {
    currentChatUser.value = id;
    messages.value = [];
    friendsMenu.value = false;
    if (null == id) return;
    // 加载消息
    try {
        if (!(await File.exists(`MsgAttach$${id}.db`))) {
            return; // 消息文件是空的
        }
        messages.value = await File.readJSON(`MsgAttach$${id}.db`);
        nextTick(() => {
            messageArea.value.scrollTop = messageArea.value.scrollHeight;
        });
    }
    catch (e) {
        ElMessage.error(`加载好友${id}的消息失败: ${e}`);
    }
}

async function editUserInfo(user) {
    if (user === 'delete') {
        const id = currentChatUser.value;
        const index = users.value.findIndex(u => u.id === id);
        addUserDialog.value.close();
        if (index !== -1) {
            try {
                await ElMessageBox.confirm("确认要删除这个好友吗？\n" + users.value[index]?.name, "删除好友", {
                    confirmButtonText: "确定",
                    cancelButtonText: "取消",
                    type: "warning",
                });
            } catch { return; }
            users.value.splice(index, 1);
            try { await File.unlink(`MsgAttach$${id}.db`) } catch { }
            // 保存好友列表
            try {
                await File.writeJSON('users.json', users.value);
                ElMessage.success(`已删除好友`);
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
    addUserAddr.value = String(user.addr);
    addUserPkey.value = user.pkey;
    addUserStatus.value = user.id;
    addUserDialog.value.showModal();
}

async function clearCurrentChatHistory() {
    const id = currentChatUser.value;
    try {
        // 确认清空
        await ElMessageBox.confirm("确认要清空当前聊天记录吗？", "清空聊天记录", {
            confirmButtonText: "清空",
            cancelButtonText: "取消",
            type: "warning",
        });
        await File.unlink(`MsgAttach$${id}.db`);
        messages.value = [];
        ElMessage.success(`已清空聊天记录`);
    }
    catch (e) {
        if (e === 'cancel') return;
        ElMessage.error(`清空聊天记录失败: ${e}`);
    }
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

async function updateNickname() {
    try {
        const { value } = await ElMessageBox.prompt("设置一个昵称吧，方便大家认出你~", '设置昵称', {
            inputValue: myNickname.value,
            inputPlaceholder: '请输入昵称',
            confirmButtonText: '更新',
            cancelButtonText: '取消',
        });
        myNickname.value = value;
        // 保存
        await File.writeText('nickname.txt', myNickname.value);
    } catch { }
}

async function sendMessage() {
    if (!inputMessage.value.text) return;
    const messageObj = {
        type: 'text',
        content: inputMessage.value.text,
        timestamp: Date.now(),
        sender: myNickname.value,
    };
    inputMessage.value.text = '';
    try {
        await sendData({
            u: myKeys.value.pub,
            t: [currentChatUserInfo.value.pkey],
            d: messageObj,
        }, currentChatUserInfo.value.addr);
    }
    catch (e) {
        return ElMessage.error(`发送消息失败: ${e}`);
    }
    messageObj.isSender = true;
    messageObj.pkey = myKeys.value.pub;
    messages.value.push(messageObj);
    nextTick(() => {
        messageArea.value.scrollTop = messageArea.value.scrollHeight;
    });

    // 保存消息记录
    try {
        await File.writeJSON(`MsgAttach$${currentChatUser.value}.db`, messages.value);
    }
    catch (e) {
        ElMessage.error(`保存消息记录失败: ${e}`);
    }
}

async function handleNewMessage(data, target, source) {
    if (!Array.isArray(target)) return;
    let isReceiver = false, isBroadcast = false;
    for (const i of target) {
        if (i === myKeys.value.pub || i === '*') {
            isReceiver = true;
            isBroadcast = (i === '*');
            break;
        }
    }
    if (!isReceiver) return; // ignore messages not for me
    data.pkey = source;
    data.expectedTarget = target;
    if (currentChatUserInfo.value && (source === currentChatUserInfo.value.pkey || currentChatUserInfo.value.pkey === '*')) {
        messages.value.push(data);
        nextTick(() => {
            messageArea.value.scrollTop = messageArea.value.scrollHeight;
        });
        // 保存消息记录
        try {
            await File.writeJSON(`MsgAttach$${currentChatUser.value}.db`, messages.value);
        }
        catch (e) {
            ElMessage.error(`保存消息记录失败: ${e}`);
        }
    }
    else {
        // 不是当前聊天对象的消息（位于后台）
        try {
            if (isBroadcast && (target.length === 1)) {
                // 公共频道
                const pubmessages = (await File.exists(`MsgAttach$0.db`)) ? (await File.readJSON(`MsgAttach$0.db`)) : [];
                pubmessages.push(data);
                await File.writeJSON(`MsgAttach$0.db`, pubmessages);
            } else {
                const { id, name } = (users.value.find(u => u.pkey === source) || {});
                if (!id) {
                    notes.value = `${source} 试图向您发送消息，但他/她不是您的好友。`
                    return; // 非好友
                }
                const filename = `MsgAttach$${id}.db`;
                const messages = (await File.exists(filename)) ? (await File.readJSON(filename)) : [];
                messages.push(data);
                await File.writeJSON(filename, messages);
                notes.value = `收到了来自${name}的消息`;
            }
        }
        catch (e) {
            ElMessage.error("后台消息处理失败: " + e);
        }
    }
}

async function handleDeleteMessage(msg) {
    const index = messages.value.indexOf(msg);
    if (index === -1) return;
    // 确认删除
    try {
        await ElMessageBox.confirm("确定删除这条消息吗？", "删除确认", {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            type: "warning",
        });
    } catch { return }
    messages.value.splice(index, 1);
    // 保存消息记录
    try {
        await File.writeJSON(`MsgAttach$${currentChatUser.value}.db`, messages.value);
    }
    catch (e) {
        ElMessage.error(`保存消息记录失败: ${e}`);
    }
}
async function handleDeleteSelectedMessages() {
    try {
        await ElMessageBox.confirm(`确定删除选中的 ${messages.value.filter(msg => msg.isSelected).length} 条消息吗？`, "删除确认", {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            type: "warning",
        });
    } catch { return }
    messages.value = messages.value.filter(msg => !msg.isSelected);
    multiSelection.value = false;
    // 保存消息记录
    try {
        await File.writeJSON(`MsgAttach$${currentChatUser.value}.db`, messages.value);
    }
    catch (e) {
        ElMessage.error(`保存消息记录失败: ${e}`);
    }
}
async function handleOpenAvatar(msg) {
    userProfileDialogData.value = {
        nickname: msg.sender,
        pkey: msg.pkey,
        name: '',
        isFriend: false,
    };
    userProfileDialog.value.showModal();
    // 查找用户信息
    if (!msg.pkey) return;
    const user = users.value.find(u => u.pkey === msg.pkey);
    if (user) {
        userProfileDialogData.value.name = user.name;
        userProfileDialogData.value.isFriend = true;
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
    overflow: auto;
    white-space: nowrap;
}
.chat-title > span {
    margin: 0 1em;
    white-space: nowrap;
}
.chat-title > span:nth-child(1) {
    margin-left: 0;
}
.chat-title > .el-radio-group {
    flex-wrap: nowrap;
}
.chat-main-view {
    flex: 1;
    display: flex;
    flex-direction: row;
    overflow: hidden;
}
.chat-main-view > * {
    overflow: hidden;
    display: flex;
    flex-direction: column;
}
.left-view {
    min-width: 100px;
    margin-right: 10px;
    padding-right: 10px;
    border-right: 1px solid;
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
.chat-user-item:focus-visible {
    outline: 2px solid rgb(160, 207, 255);
    transition: outline-offset 0s, outline 0s;
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
    flex: 1;
    text-align: center;
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
.chat-messages-container {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: auto;
    padding: 0.5em;
}
.chat-multiple-functions {
    background-color: #fff;
    padding: 10px;
    border: 1px solid #ccc;
    border-bottom: 0;
}
.chat-input {
    padding: 10px;
    border: 1px solid #ccc;
    background-color: #fff;
    display: flex;
}
.chat-input > button {
    height: auto;
    margin-left: 10px;
}
.notes-view {
    align-items: center;
    background-color: rgb(253, 246, 236);
    border-color: rgb(250, 236, 216);
    border-radius: 5px;
    border-style: solid;
    border-width: 2px;
    box-sizing: border-box;
    display: flex;
    gap: 8px;
    max-width: calc(100% - 32px);
    padding: 10px;
    position: fixed;
    transition: transform .4s, top .4s, bottom .4s;
    width: -moz-fit-content;
    width: fit-content;
    left: 0; right: 0;
    margin: 0 auto;
    color: #e6a23c;
    top: 10px;
    z-index: 3001;
}
.dialog-content .col > .el-button+ .el-button {
    margin-top: 0.5em;
    margin-left: 0;
}
.user-profile-dialog-title, .user-profile-dialog-nickname {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.user-profile-dialog-title {
    font-weight: bold;
    font-size: larger;
    text-align: center;
    margin-bottom: 5px;
}
.user-profile-dialog-nickname {
    color: gray;
    margin-bottom: 0.5em;
}
.chat-title > .btn-menu, .mobile-backdrop {
    display: none;
}
@media screen and (max-width: 600px) {
    .left-view {
        display: none;
    }
    .chat-title > .btn-menu {
        display: block;
    }
    .left-view[data-open="true"] {
        display: flex;
        position: absolute;
        left: 0;
        top: 0; bottom: 0;
        /* width: 200px; */
        /* max-width: 100vw; */
        width: revert !important;
        right: 0;
        background-color: #fff;
        padding: 10px;
        margin-right: 0;
        border-right: 0;
        box-sizing: border-box;
        z-index: 200;
    }
}
</style>