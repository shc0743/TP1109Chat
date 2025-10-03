export default class ViewBuffer {
    /**
     * 
     * @param {DataView} view 
     */
    constructor(view) {
        this.view = view;
    }

    _seek = 0;
    seek(pos = 0) {
        this._seek = pos;
    }

    readUint8() {
        const value = this.view.getUint8(this._seek);
        this._seek += 1;
        return value;
    }
    readUint16(littleEndian = false) {
        const value = this.view.getUint16(this._seek, littleEndian);
        this._seek += 2;
        return value;
    }
    readUint32(littleEndian = false) {
        const value = this.view.getUint32(this._seek, littleEndian);
        this._seek += 4;
        return value;
    }
    readUint64(littleEndian = false) {
        const value = this.view.getBigUint64(this._seek, littleEndian);
        this._seek += 8;
        return value;
    }
    readBytes(length) {
        const bytes = new Uint8Array(this.view.buffer, this._seek, length);
        this._seek += length;
        return bytes;
    }

    writeUint8(value) {
        this.view.setUint8(this._seek, value);
        this._seek += 1;
    }
    writeUint16(value, littleEndian = false) {
        this.view.setUint16(this._seek, value, littleEndian);
        this._seek += 2;
    }
    writeUint32(value, littleEndian = false) {
        this.view.setUint32(this._seek, value, littleEndian);
        this._seek += 4;
    }
    writeUint64(value, littleEndian = false) {
        this.view.setBigUint64(this._seek, value, littleEndian);
        this._seek += 8;
    }
    writeBytes(bytes) {
        this.view.set(bytes, this._seek);
        this._seek += bytes.length;
    }
}
