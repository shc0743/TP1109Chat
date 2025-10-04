<template>
    <div class="chat-message-item-container" :data-sent="(!!props.message.isSender)" :data-is-system="(props.message.type === 'system')" :data-selected="selected" @click="handleClick">
        <div class="sender-name-container" v-if="!!props.message.sender">
            <ElCheckbox v-if="isMultipleSelection" v-model="selected" class="checkbox" />
            <div class="checkbox-whitespace" v-if="isMultipleSelection"></div>
            <div class="sender-name" v-text="props.message.isSender ? '我' : props.message.sender"></div>
        </div>
        <div class="chat-message-item" @contextmenu="onContextMenu" ref="messageItem">
            <div class="chat-user-avatar">
                <ElAvatar class="chat-user-avatar-btn" tabindex="0" role="link" :aria-label="props.message.sender + '的个人信息'" @click="openAvatar" @keydown.enter="openAvatar">User</ElAvatar>
            </div>
            <template v-if="props.message.type === 'system'">
                <div class="chat-message-system-text" v-text="props.message.content"></div>
            </template>
            <template v-if="props.message.type === 'text'">
                <div class="chat-message-contents-text"><span class="text-node" v-text="props.message.content"></span></div>
            </template>
            <template v-else>
                <div class="chat-message-contents-text">
                    未知消息类型
                </div>
            </template>
        </div>
    </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { AppendMenu, CreatePopupMenu, TrackPopupMenu } from 'simple-w32-context-menu';

const props = defineProps({
    message: {
        type: Object,
        default: () => ({})
    },
    isMultipleSelection: {
        type: Boolean,
        default: false
    },
});

const emit = defineEmits(['open-avatar', 'delete-message', 'start-multiple-selection', 'update-selection']);

const messageItem = ref(null);
const selected = ref(false);

watch(() => props.isMultipleSelection, (newVal) => {
    if (!newVal) {
        selected.value = false;
        emit('update-selection', false);
    }
})
watch(() => selected.value, (newVal) => {
    emit('update-selection', newVal);
})

onMounted(() => {
    
});
onBeforeUnmount(() => {
    
});

function onContextMenu(e) {
    if (props.isMultipleSelection) return;
    const s = window.getSelection();
    if (!s.isCollapsed) return;
    e.preventDefault();

    const hMenu = CreatePopupMenu();
    AppendMenu(hMenu, String, {}, "复制", () => {
        navigator.clipboard.writeText(props.message.content);
    });
    AppendMenu(hMenu, String, {}, "全选", () => {
        const node = messageItem.value.querySelector('.text-node');
        if (!node) return;
        const range = document.createRange();
        range.selectNode(node);
        window.getSelection().addRange(range);
    });
    AppendMenu(hMenu, String, {}, "删除", () => {
        emit('delete-message', props.message);
    });
    AppendMenu(hMenu, String, {}, "多选", () => {
        emit('start-multiple-selection');
        selected.value = true;
    });
    TrackPopupMenu(hMenu, e.x, e.y, 0);
}

function openAvatar() {
    emit('open-avatar', props.message);
}

function handleClick() {
    if (props.isMultipleSelection) {
        selected.value = !selected.value;
    }
}
</script>

<style scoped>
.chat-message-item-container {
    display: flex;
    flex-direction: column;
}

.chat-message-item {
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
}

.sender-name-container {
    display: flex;
    flex-direction: row;
    justify-content: flex-start;
}

.sender-name {
    font-size: small;
    color: #a6a6a6;
    font-family: monospace;
    margin-bottom: 5px;
    max-width: calc(0.5em * 20);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.chat-message-item-container[data-sent="true"] > .sender-name-container {
    justify-content: flex-end;
}

.chat-message-item-container[data-is-system="true"] > .chat-message-item {
    justify-content: center;
    font-size: small;
}

.chat-message-item-container[data-selected="true"] {
    background-color: #e0e0e0;
}

.chat-message-item .chat-message-system-text {
    padding: 5px;
    border-radius: 2px;
    color: #a6a6a6;
    font-family: monospace;
}

.chat-message-item-container[data-sent="true"] > .chat-message-item {
    flex-direction: row-reverse;
}

.chat-message-item > .chat-user-avatar {
    margin-right: 0.5em;
    height: 100%;
    user-select: none;
}

.chat-message-item > .chat-user-avatar > .chat-user-avatar-btn {
    cursor: pointer;
}

.chat-message-item > .chat-user-avatar > .chat-user-avatar-btn:focus,
.chat-message-item > .chat-user-avatar > .chat-user-avatar-btn:focus-visible {
    outline: 2px solid rgb(160, 207, 255);
    outline-offset: 1px;
    transition: outline-offset 0s, outline 0s;
}

.chat-message-item-container+.chat-message-item-container {
    margin-top: 1.5em;
}

.chat-message-item-container[data-sent="true"] > .chat-message-item > .chat-user-avatar {
    margin-left: 0.5em;
    margin-right: 0;
}

.chat-message-item .chat-message-contents-text {
    padding: 10px;
    border-radius: 5px;
    box-shadow: 0 0 4px 0 #bbb;
    word-break: break-all;
    overflow: hidden;
    --margin-value: 10%;
    margin-right: var(--margin-value);
    background-color: #f7f7f7;
    white-space: pre-wrap;
}

.chat-message-item-container[data-sent="true"] > .chat-message-item > .chat-message-contents-text {
    background-color: #d2f4c1;
    margin-left: var(--margin-value);
    margin-right: 0;
}

.checkbox-whitespace {
    flex: 1;
}
</style>