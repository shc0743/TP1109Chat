import ViewBuffer from "./ViewBuffer";

// 协议常量
export const PROTOCOL = {
    MAGIC: [0xAA55, 0x1207, 0x1111, 0xF0D3, 0x0818, 0x0413, 0xC2F4, 0xF2F5], // 16
    VERSION: 1, // 1
    TYPE: {
        DATA: 0,
        ACK: 1,
        NACK: 2,
        CONTROL: 3,
    }, // 1
    SENDER_ID: 0, // 2
    MESSAGE_ID: 0, // 4
    TOTAL_FRAGS: 0, // 2
    FRAG_INDEX: 0, // 2
    DATA_LENGTH: 0, // 2
    RESERVED: 0 // 2
};

export class PacketBuffer {
    constructor() {
        this.buffer = new Uint8Array(0);
    }

    // 追加新数据
    append(data) {
        const newBuffer = new Uint8Array(this.buffer.length + data.byteLength);
        newBuffer.set(this.buffer);
        newBuffer.set(new Uint8Array(data), this.buffer.length);
        this.buffer = newBuffer;
    }

    // 提取完整数据包
    extractPackets() {
        const packets = [];
        let offset = 0;

        while (offset <= this.buffer.length - 32) {
            const dataView = new DataView(this.buffer.buffer, offset);

            // 验证魔数
            let magicMatch = true;
            for (let i = 0; i < PROTOCOL.MAGIC.length; i++) {
                if (dataView.getUint16(i * 2, false) !== PROTOCOL.MAGIC[i]) {
                    magicMatch = false;
                    break;
                }
            }

            if (!magicMatch) {
                offset += 2; // 移动一个字节继续查找
                continue;
            }

            // 读取数据包长度
            const dataLength = dataView.getUint16(28, false); // DATA_LENGTH 在偏移28处
            const packetLength = 32 + dataLength;

            // 检查是否有完整数据包
            if (offset + packetLength > this.buffer.length) {
                break; // 数据不完整，等待更多数据
            }

            // 提取完整数据包
            const packetData = this.buffer.slice(offset, offset + packetLength);
            packets.push(packetData);
            offset += packetLength;
        }

        // 保留剩余的不完整数据
        this.buffer = this.buffer.slice(offset);

        return packets;
    }

    // 清空缓冲区
    clear() {
        this.buffer = new Uint8Array(0);
    }
}

// 协议编码器
export class PacketEncoder {
    static encode(packet) {
        const header = new ArrayBuffer(32);
        const view = new DataView(header);
        const buffer = new ViewBuffer(view);

        for (const i of PROTOCOL.MAGIC) {
            buffer.writeUint16(i);
        }
        // big-endian
        buffer.writeUint8(PROTOCOL.VERSION);
        buffer.writeUint8(packet.type || PROTOCOL.TYPE.DATA);
        buffer.writeUint16(packet.senderId);
        buffer.writeUint32(packet.messageId);
        buffer.writeUint16(packet.totalFrags);
        buffer.writeUint16(packet.fragIndex);
        buffer.writeUint16(packet.dataLength);
        buffer.writeUint16(0); // reserved

        if (packet.payload) {
            const fullPacket = new Uint8Array(32 + packet.dataLength);
            fullPacket.set(new Uint8Array(header), 0);
            fullPacket.set(packet.payload, 32);
            return fullPacket.buffer;
        }

        return header;
    }
    static encodeWithTargetId(targetId, packet) {
        const ab = this.encode(packet);
        const u8 = new Uint8Array(2 + ab.byteLength);
        const view = new DataView(u8.buffer);
        view.setUint16(0, targetId);
        u8.set(new Uint8Array(ab), 2);
        return u8.buffer;
    }
}

// 协议解码器
export class PacketDecoder {
    static decode(data) {
        const view = new DataView(data);

        if (view.byteLength < 32) {
            throw new Error('数据包太小');
        }

        const buffer = new ViewBuffer(view);

        const magic = [];
        for (let i = 0; i < PROTOCOL.MAGIC.length; i ++) {
            magic.push(buffer.readUint16());
        }
        if (magic.join(',') !== PROTOCOL.MAGIC.join(',')) {
            throw new Error('魔数不匹配');
        }

        const version = buffer.readUint8();
        const type = buffer.readUint8();
        const senderId = buffer.readUint16();
        const messageId = buffer.readUint32();
        const totalFrags = buffer.readUint16();
        const fragIndex = buffer.readUint16();
        const dataLength = buffer.readUint16();
        const reserved = buffer.readUint16();

        return {
            magic: magic,
            version: version,
            type: type,
            senderId,
            messageId,
            totalFrags,
            fragIndex,
            dataLength,
            reserved,
            payload: data.byteLength > 32 ? new Uint8Array(data, 32) : null
        };
    }
}

// 分片管理器
export class FragmentManager {
    constructor() {
        this.reassemblyBuffers = new Map(); // senderId -> { messageId -> { fragments, timer } }
    }

    // 处理接收到的分片
    processFragment(receivedPacket, onCompleteMessage) {
        const { senderId, messageId, totalFrags, fragIndex, payload } = receivedPacket;

        // 创建发送者的缓冲区键
        const senderKey = senderId;
        if (!this.reassemblyBuffers.has(senderKey)) {
            this.reassemblyBuffers.set(senderKey, new Map());
        }

        const messageBuffers = this.reassemblyBuffers.get(senderKey);
        const messageKey = messageId;

        // 初始化或获取消息缓冲区
        if (!messageBuffers.has(messageKey)) {
            messageBuffers.set(messageKey, {
                fragments: new Array(totalFrags),
                receivedCount: 0,
                timer: setTimeout(() => {
                    // 超时清理未完成的消息
                    messageBuffers.delete(messageKey);
                }, 30000) // 30秒超时
            });
        }

        const messageBuffer = messageBuffers.get(messageKey);

        // 存储分片
        if (!messageBuffer.fragments[fragIndex]) {
            messageBuffer.fragments[fragIndex] = payload;
            messageBuffer.receivedCount++;
        }

        // 检查是否已收齐所有分片
        if (messageBuffer.receivedCount === totalFrags) {
            // 组合所有分片
            let totalLength = 0;
            for (let i = 0; i < totalFrags; i++) {
                totalLength += messageBuffer.fragments[i].length;
            }

            const fullMessage = new Uint8Array(totalLength);
            let offset = 0;
            for (let i = 0; i < totalFrags; i++) {
                fullMessage.set(messageBuffer.fragments[i], offset);
                offset += messageBuffer.fragments[i].length;
            }

            // 清理缓冲区
            clearTimeout(messageBuffer.timer);
            messageBuffers.delete(messageKey);

            // 回调完整消息
            onCompleteMessage({
                senderId,
                messageId,
                data: fullMessage
            });
        }
    }

    // 发送消息时进行分片
    createFragments(senderId, messageId, data, chunkSize = 500 - 2 - 32) {
        const fragments = [];
        const totalFrags = Math.ceil(data.length / chunkSize);

        for (let i = 0; i < totalFrags; i++) {
            const start = i * chunkSize;
            const end = Math.min(start + chunkSize, data.length);
            const chunk = data.slice(start, end);

            fragments.push({
                type: PROTOCOL.TYPE.DATA,
                senderId,
                messageId,
                totalFrags,
                fragIndex: i,
                dataLength: chunk.length,
                payload: chunk
            });
        }

        return fragments;
    }
}

