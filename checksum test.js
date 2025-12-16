function validateChecksum(hexStr) {
    hexStr = hexStr.trim().replace(/^0x/i, '').toUpperCase();
    
    if (!hexStr || hexStr.length < 4 || hexStr.length % 2 !== 0) {
        return { valid: false, reason: 'Invalid length or empty' };
    }

    const bytes = [];
    for (let i = 0; i < hexStr.length; i += 2) {
        const byteStr = hexStr.slice(i, i + 2);
        if (!/^[0-9A-F]{2}$/.test(byteStr)) {
            return { valid: false, reason: `Invalid hex at position ${i}: "${byteStr}"` };
        }
        bytes.push(parseInt(byteStr, 16));
    }

    if (bytes.length < 2) {
        return { valid: false, reason: 'At least 2 bytes required' };
    }

    const dataBytes = bytes.slice(0, -1);
    const storedChecksum = bytes[bytes.length - 1];

    // ✅ 关键修正：每一步都 mod 256（用 & 0xFF）
    let sum = 0;
    for (const b of dataBytes) {
        sum = (sum + b) & 0xFF; // 始终保持 sum ∈ [0, 255]
    }

    if (sum === storedChecksum) {
        return {
            valid: true,
            dataLength: dataBytes.length,
            checksum: storedChecksum
        };
    } else {
        return {
            valid: false,
            reason: `Checksum mismatch: expected ${sum.toString(16).toUpperCase().padStart(2, '0')}, got ${storedChecksum.toString(16).toUpperCase().padStart(2, '0')}`,
            expected: sum,
            actual: storedChecksum,
            dataLength: dataBytes.length
        };
    }
}

function addChecksum(hexData) {
    // hexData: like "640000000000C03F" (no checksum)
    const clean = hexData.trim().replace(/^0x/i, '').toUpperCase();
    if (clean.length % 2 !== 0) throw new Error('Hex data must have even length');
    
    let sum = 0;
    for (let i = 0; i < clean.length; i += 2) {
        const b = parseInt(clean.slice(i, i + 2), 16);
        sum = (sum + b) & 0xFF;
    }
    return clean + sum.toString(16).toUpperCase().padStart(2, '0');
}

// Example:
console.log(addChecksum("640000000000C03F")); // → "640000000000C03F63"