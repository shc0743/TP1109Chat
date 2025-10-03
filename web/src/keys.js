// crypto.js
class ECCCrypto {
    constructor() {
        this.keyPair = null;
    }

    // 生成 ECC 密钥对
    async generateKeyPair() {
        try {
            this.keyPair = await window.crypto.subtle.generateKey(
                {
                    name: "ECDH",
                    namedCurve: "P-256",
                },
                true, // 可导出
                ["deriveKey", "deriveBits"]
            );

            return this.keyPair;
        } catch (error) {
            console.error("密钥生成失败:", error);
            throw error;
        }
    }

    // 导出公钥为 Base64 字符串
    async exportPublicKey() {
        if (!this.keyPair) {
            throw new Error("未生成密钥对");
        }

        const exported = await window.crypto.subtle.exportKey(
            "spki",
            this.keyPair.publicKey
        );

        return this.arrayBufferToBase64(exported);
    }

    // 导出私钥为 Base64 字符串
    async exportPrivateKey() {
        if (!this.keyPair) {
            throw new Error("未生成密钥对");
        }

        const exported = await window.crypto.subtle.exportKey(
            "pkcs8",
            this.keyPair.privateKey
        );

        return this.arrayBufferToBase64(exported);
    }

    // 从 Base64 字符串导入密钥对
    async importKeyPair(publicKeyBase64, privateKeyBase64) {
        try {
            // 导入公钥
            const publicKeyBuffer = this.base64ToArrayBuffer(publicKeyBase64);
            const publicKey = await window.crypto.subtle.importKey(
                "spki",
                publicKeyBuffer,
                {
                    name: "ECDH",
                    namedCurve: "P-256"
                },
                true,
                []
            );

            // 导入私钥
            const privateKeyBuffer = this.base64ToArrayBuffer(privateKeyBase64);
            const privateKey = await window.crypto.subtle.importKey(
                "pkcs8",
                privateKeyBuffer,
                {
                    name: "ECDH",
                    namedCurve: "P-256"
                },
                true,
                ["deriveKey", "deriveBits"]
            );

            this.keyPair = { publicKey, privateKey };
            return this.keyPair;
        } catch (error) {
            console.error("密钥导入失败:", error);
            throw error;
        }
    }

    // 工具函数：ArrayBuffer 转 Base64
    arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary);
    }

    // 工具函数：Base64 转 ArrayBuffer
    base64ToArrayBuffer(base64) {
        const binaryString = atob(base64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    }

    // 使用 ECDH 派生共享密钥并加密
    async encrypt(recipientPublicKeyBase64, message) {
        if (!this.keyPair) {
            throw new Error("未生成密钥对");
        }

        // 导入对方的公钥
        const recipientPublicKeyBuffer = this.base64ToArrayBuffer(recipientPublicKeyBase64);
        const recipientPublicKey = await window.crypto.subtle.importKey(
            "spki",
            recipientPublicKeyBuffer,
            {
                name: "ECDH",
                namedCurve: "P-256"
            },
            true,
            []
        );

        // 派生共享密钥
        const sharedSecret = await window.crypto.subtle.deriveKey(
            {
                name: "ECDH",
                public: recipientPublicKey
            },
            this.keyPair.privateKey,
            {
                name: "AES-GCM",
                length: 256
            },
            true,
            ["encrypt"]
        );

        // 加密
        const iv = window.crypto.getRandomValues(new Uint8Array(12));
        const encryptedData = await window.crypto.subtle.encrypt(
            {
                name: "AES-GCM",
                iv: iv
            },
            sharedSecret,
            new TextEncoder().encode(message)
        );

        return {
            iv: (iv),
            data: (encryptedData)
        };
    }

    // 使用 ECDH 派生共享密钥并解密
    async decrypt(senderPublicKeyBuffer, encryptedMessage) {
        if (!this.keyPair) {
            throw new Error("未生成密钥对");
        }

        // 导入对方的公钥
        const senderPublicKey = await window.crypto.subtle.importKey(
            "spki",
            senderPublicKeyBuffer,
            {
                name: "ECDH",
                namedCurve: "P-256"
            },
            true,
            []
        );

        // 派生共享密钥
        const sharedSecret = await window.crypto.subtle.deriveKey(
            {
                name: "ECDH",
                public: senderPublicKey
            },
            this.keyPair.privateKey,
            {
                name: "AES-GCM",
                length: 256
            },
            true,
            ["decrypt"]
        );

        // 解密
        const iv = (encryptedMessage.iv);
        const encryptedData = (encryptedMessage.data);

        const decryptedData = await window.crypto.subtle.decrypt(
            {
                name: "AES-GCM",
                iv: iv
            },
            sharedSecret,
            encryptedData
        );

        return new TextDecoder().decode(decryptedData);
    }
}

export default ECCCrypto;