<template>
    <div class="chat-message-item chat-message-item" :data-sent="(!!props.message.isSender)">
        <div class="chat-user-avatar">
            <ElAvatar>User</ElAvatar>
        </div>
        <template v-if="props.message.type === 'system'">
            <div class="chat-message-system-text" v-text="props.message.content"></div>
        </template>
        <template v-if="props.message.type === 'text'">
            <div class="chat-message-contents-text" v-text="props.message.content"></div>
        </template>
        <template v-else>
            <div class="chat-message-contents-text">
                未知消息类型
            </div>
        </template>
    </div>
</template>

<script setup>

const props = defineProps({
    message: {
        type: Object,
        default: () => ({})
    }
});


</script>

<style scoped>
.chat-message-item {
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
}

.chat-message-item:has(.chat-message-system-text) {
    justify-content: center;
    font-size: small;
}

.chat-message-item .chat-message-system-text {
    padding: 5px;
    border-radius: 2px;
    color: #a6a6a6;
    font-family: monospace;
}

.chat-message-item[data-sent="true"] {
    flex-direction: row-reverse;
}

.chat-message-item .chat-user-avatar {
    margin-right: 0.5em;
    height: 100%;
}

.chat-message-item+.chat-message-item {
    margin-top: 1.5em;
}

.chat-message-item[data-sent="true"] .chat-user-avatar {
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

.chat-message-item[data-sent="true"] .chat-message-contents-text {
    background-color: #d2f4c1;
    margin-left: var(--margin-value);
    margin-right: 0;
}
</style>