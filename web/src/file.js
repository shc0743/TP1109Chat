export default class File {
    /**
     * 构建文件URL
     * @param {string} filename 
     * @returns {string}
     */
    static _buildUrl(filename) {
        const url = new URL('/api/fs/object', window.location.origin);
        url.searchParams.set('name', filename);
        return url.toString();
    }

    /**
     * 处理HTTP响应
     * @param {Response} response 
     * @returns {Response}
     */
    static _handleResponse(response) {
        if (!response.ok) {
            throw new Error(`文件操作失败: ${response.status} ${response.statusText}`);
        }
        return response;
    }

    /**
     * 读取文件为Blob对象
     * @param {string} filename - 文件名
     * @returns {Promise<Blob>}
     */
    static async read(filename) {
        const url = this._buildUrl(filename);
        const response = await fetch(url);
        return this._handleResponse(response).blob();
    }

    /**
     * 读取文件为文本
     * @param {string} filename - 文件名
     * @param {string} encoding - 编码格式，默认为UTF-8
     * @returns {Promise<string>}
     */
    static async readText(filename, encoding = 'UTF-8') {
        const url = this._buildUrl(filename);
        const response = await fetch(url);
        const buffer = await this._handleResponse(response).arrayBuffer();
        const decoder = new TextDecoder(encoding.toLowerCase());
        return decoder.decode(buffer);
    }

    /**
     * 读取文件为JSON对象
     * @param {string} filename - 文件名
     * @returns {Promise<object>}
     */
    static async readJSON(filename) {
        const text = await this.readText(filename);
        return JSON.parse(text);
    }

    /**
     * 写入文件（二进制数据）
     * @param {string} filename - 文件名
     * @param {Blob|ArrayBuffer|Uint8Array} data - 二进制数据
     * @returns {Promise<void>}
     */
    static async write(filename, data) {
        const url = this._buildUrl(filename);

        let body;
        if (data instanceof Blob) {
            body = data;
        } else if (data instanceof ArrayBuffer) {
            body = new Uint8Array(data);
        } else if (data instanceof Uint8Array) {
            body = data;
        } else {
            throw new Error('不支持的数据类型，请使用Blob、ArrayBuffer或Uint8Array');
        }

        const response = await fetch(url, {
            method: 'PUT',
            body: body
        });

        this._handleResponse(response);
    }

    /**
     * 写入文本文件
     * @param {string} filename - 文件名
     * @param {string} text - 文本内容
     * @param {string} encoding - 编码格式，默认为UTF-8
     * @returns {Promise<void>}
     */
    static async writeText(filename, text, encoding = 'UTF-8') {
        const encoder = new TextEncoder();
        const data = encoder.encode(text);
        return await this.write(filename, data);
    }

    /**
     * 写入JSON文件
     * @param {string} filename - 文件名
     * @param {object} data - JSON对象
     * @param {number} indent - 缩进空格数
     * @returns {Promise<void>}
     */
    static async writeJSON(filename, data, indent = 2) {
        const text = JSON.stringify(data, null, indent);
        return await this.writeText(filename, text);
    }

    /**
     * 删除文件
     * @param {string} filename - 文件名
     * @returns {Promise<boolean>} - 是否删除成功
     */
    static async unlink(filename) {
        const url = this._buildUrl(filename);
        const response = await fetch(url, {
            method: 'DELETE'
        });

        if (response.status === 204) {
            return true; // 成功删除
        } else if (response.status === 404) {
            return false; // 文件不存在
        } else {
            throw new Error(`删除文件失败: ${response.status} ${response.statusText}`);
        }
    }

    /**
     * 获取文件信息
     * @param {string} filename - 文件名
     * @returns {Promise<Object>} - 文件信息对象
     */
    static async info(filename) {
        const url = this._buildUrl(filename);
        const response = await fetch(url, {
            method: 'HEAD'
        });

        this._handleResponse(response);

        const headers = response.headers;
        return {
            size: parseInt(headers.get('X-File-Size') || '0'),
            creationTime: new Date(headers.get('X-File-Creation')),
            lastModifiedTime: new Date(headers.get('X-File-Last-Modified')),
            lastAccessTime: new Date(headers.get('X-File-Last-Access'))
        };
    }

    /**
     * 检查文件是否存在
     * @param {string} filename - 文件名
     * @returns {Promise<boolean>}
     */
    static async exists(filename) {
        const url = this._buildUrl(filename);
        try {
            const response = await fetch(url, {
                method: 'HEAD'
            });
            return response.ok;
        } catch {
            return false;
        }
    }

    // /**
    //  * 复制文件
    //  * @param {string} source - 源文件名
    //  * @param {string} destination - 目标文件名
    //  * @returns {Promise<void>}
    //  */
    // static async copy(source, destination) {
    //     const data = await this.read(source);
    //     await this.write(destination, data);
    // }
    //
    // /**
    //  * 移动/重命名文件
    //  * @param {string} source - 源文件名
    //  * @param {string} destination - 目标文件名
    //  * @returns {Promise<void>}
    //  */
    // static async move(source, destination) {
    //     await this.copy(source, destination);
    //     await this.unlink(source);
    // }
}