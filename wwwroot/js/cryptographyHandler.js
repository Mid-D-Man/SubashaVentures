// File: wwwroot/js/cryptographyHandler.js - Enhanced with AES validation and key management
window.cryptographyHandler = {
    // Production-ready AES constants - validated 32-byte key and 16-byte IV
    PRODUCTION_AES_KEY: "+58K1jECYmF6GpPovgv3kmUMljv/EvY3G1NPwWqRCj8=",
    PRODUCTION_AES_IV: "3emqU/f2fW6KG4rqanUG+Q==",

    // Helper: Convert an ArrayBuffer to a Base64 string.
    arrayBufferToBase64: function (buffer) {
        let binary = "";
        const bytes = new Uint8Array(buffer);
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    },

    // Helper: Convert a Base64 string to an ArrayBuffer.
    base64ToArrayBuffer: function (base64) {
        const binaryString = window.atob(base64);
        const len = binaryString.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    },

    // Helper: Convert ArrayBuffer to hexadecimal string.
    arrayBufferToHex: function (buffer) {
        let hexCodes = [];
        const view = new DataView(buffer);
        for (let i = 0; i < view.byteLength; i++) {
            hexCodes.push(view.getUint8(i).toString(16).padStart(2, '0'));
        }
        return hexCodes.join('');
    },

    // AES Parameter Validation
    validateBase64Key: function(base64Key, expectedBytes = 32) {
        try {
            const decoded = atob(base64Key);
            const actualBytes = decoded.length;

            return {
                valid: actualBytes === expectedBytes,
                actualBytes,
                expectedBytes,
                actualBits: actualBytes * 8,
                expectedBits: expectedBytes * 8
            };
        } catch (error) {
            return {
                valid: false,
                error: error.message,
                actualBytes: 0,
                expectedBytes
            };
        }
    },

    validateBase64IV: function(base64IV, expectedBytes = 16) {
        try {
            const decoded = atob(base64IV);
            const actualBytes = decoded.length;

            return {
                valid: actualBytes === expectedBytes,
                actualBytes,
                expectedBytes
            };
        } catch (error) {
            return {
                valid: false,
                error: error.message,
                actualBytes: 0,
                expectedBytes
            };
        }
    },

    // Enhanced AES key generation with validation
    generateAesKey: async function (bitLength = 256) {
        // Validate bit length
        if (![128, 192, 256].includes(bitLength)) {
            throw new Error("Invalid bit length. Must be 128, 192, or 256.");
        }

        const key = await crypto.subtle.generateKey(
            {
                name: "AES-CBC",
                length: bitLength
            },
            true, // extractable
            ["encrypt", "decrypt"]
        );

        const rawKey = await crypto.subtle.exportKey("raw", key);
        const base64Key = this.arrayBufferToBase64(rawKey);

        // Validate the generated key
        const validation = this.validateBase64Key(base64Key, bitLength / 8);
        if (!validation.valid) {
            throw new Error(`Generated key validation failed: Expected ${validation.expectedBytes} bytes, got ${validation.actualBytes}`);
        }

        return base64Key;
    },

    // Enhanced IV generation with validation
    generateIv: function () {
        const iv = crypto.getRandomValues(new Uint8Array(16));
        const base64IV = this.arrayBufferToBase64(iv);

        // Validate the generated IV
        const validation = this.validateBase64IV(base64IV);
        if (!validation.valid) {
            throw new Error(`Generated IV validation failed: Expected ${validation.expectedBytes} bytes, got ${validation.actualBytes}`);
        }

        return base64IV;
    },

    // Secure parameter resolution with fallback to production defaults
    resolveAesParameters: function(keyString, ivString) {
        let resolvedKey = keyString;
        let resolvedIV = ivString;
        let warnings = [];

        // Validate and resolve key
        if (!keyString) {
            resolvedKey = this.PRODUCTION_AES_KEY;
            warnings.push("No key provided, using production default");
        } else {
            const keyValidation = this.validateBase64Key(keyString);
            if (!keyValidation.valid) {
                resolvedKey = this.PRODUCTION_AES_KEY;
                warnings.push(`Invalid key provided (${keyValidation.error || 'wrong size'}), using production default`);
            }
        }

        // Validate and resolve IV
        if (!ivString) {
            resolvedIV = this.PRODUCTION_AES_IV;
            warnings.push("No IV provided, using production default");
        } else {
            const ivValidation = this.validateBase64IV(ivString);
            if (!ivValidation.valid) {
                resolvedIV = this.PRODUCTION_AES_IV;
                warnings.push(`Invalid IV provided (${ivValidation.error || 'wrong size'}), using production default`);
            }
        }

        return {
            key: resolvedKey,
            iv: resolvedIV,
            warnings,
            usingDefaults: warnings.length > 0
        };
    },

    // Enhanced encryption with parameter validation and fallback
    encryptData: async function (data, keyString, ivString) {
        const params = this.resolveAesParameters(keyString, ivString);

        if (params.warnings.length > 0) {
            console.warn("AES Encryption Parameter Warnings:", params.warnings);
        }

        const encoder = new TextEncoder();
        const dataBuffer = encoder.encode(data);
        const keyBuffer = this.base64ToArrayBuffer(params.key);
        const ivBuffer = this.base64ToArrayBuffer(params.iv);

        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "AES-CBC" },
            false,
            ["encrypt"]
        );

        try {
            const encryptedBuffer = await crypto.subtle.encrypt(
                { name: "AES-CBC", iv: ivBuffer },
                cryptoKey,
                dataBuffer
            );
            return this.arrayBufferToBase64(encryptedBuffer);
        } catch (error) {
            console.error("Encryption failed:", error);
            throw new Error(`AES encryption failed: ${error.message}`);
        }
    },

    // Enhanced decryption with parameter validation and fallback
    decryptData: async function (base64Data, keyString, ivString) {
        const params = this.resolveAesParameters(keyString, ivString);

        if (params.warnings.length > 0) {
            console.warn("AES Decryption Parameter Warnings:", params.warnings);
        }

        const keyBuffer = this.base64ToArrayBuffer(params.key);
        const ivBuffer = this.base64ToArrayBuffer(params.iv);

        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "AES-CBC" },
            false,
            ["decrypt"]
        );

        const encryptedBuffer = this.base64ToArrayBuffer(base64Data);
        try {
            const decryptedBuffer = await crypto.subtle.decrypt(
                { name: "AES-CBC", iv: ivBuffer },
                cryptoKey,
                encryptedBuffer
            );
            const decoder = new TextDecoder();
            return decoder.decode(decryptedBuffer);
        } catch (error) {
            console.error("Decryption failed:", error);
            throw new Error(`AES decryption failed: ${error.message}`);
        }
    },

    // Comprehensive AES validation suite
    runAesValidationSuite: function(key, iv) {
        const testKey = key || this.PRODUCTION_AES_KEY;
        const testIV = iv || this.PRODUCTION_AES_IV;

        console.log("=== AES Cryptography Validation Suite ===");

        // Key validation
        const keyResult = this.validateBase64Key(testKey);
        console.log(`Key Validation: ${keyResult.valid ? '✓ PASS' : '❌ FAIL'}`);
        if (!keyResult.valid) {
            console.log(`  Expected: ${keyResult.expectedBytes} bytes (${keyResult.expectedBits} bits)`);
            console.log(`  Actual: ${keyResult.actualBytes} bytes`);
            if (keyResult.error) console.log(`  Error: ${keyResult.error}`);
        }

        // IV validation
        const ivResult = this.validateBase64IV(testIV);
        console.log(`IV Validation: ${ivResult.valid ? '✓ PASS' : '❌ FAIL'}`);
        if (!ivResult.valid) {
            console.log(`  Expected: ${ivResult.expectedBytes} bytes`);
            console.log(`  Actual: ${ivResult.actualBytes} bytes`);
            if (ivResult.error) console.log(`  Error: ${ivResult.error}`);
        }

        return {
            keyValid: keyResult.valid,
            ivValid: ivResult.valid,
            overallValid: keyResult.valid && ivResult.valid,
            keyResult,
            ivResult
        };
    },

    // Integration test with parameter resolution
    testAesIntegration: async function(testKey, testIV) {
        const testData = "AES-256-CBC Integration Test - Enhanced Handler";

        try {
            console.log("=== AES Integration Test ===");
            console.log(`Test Data: "${testData}"`);

            // Test encryption with parameter resolution
            const encrypted = await this.encryptData(testData, testKey, testIV);
            console.log("✓ Encryption successful");
            console.log(`Encrypted (Base64): ${encrypted.substring(0, 32)}...`);

            // Test decryption with parameter resolution
            const decrypted = await this.decryptData(encrypted, testKey, testIV);
            const success = decrypted === testData;
            console.log(`✓ Decryption ${success ? 'successful' : 'failed'}: ${success ? 'DATA_MATCH' : 'DATA_MISMATCH'}`);

            if (!success) {
                console.log(`Expected: "${testData}"`);
                console.log(`Actual: "${decrypted}"`);
            }

            return success;
        } catch (error) {
            console.error("❌ Integration test failed:", error.message);
            return false;
        }
    },

    // Generate new secure AES parameters
    generateSecureAesParameters: async function() {
        try {
            console.log("=== Generating New Secure AES Parameters ===");

            const newKey = await this.generateAesKey(256);
            const newIV = this.generateIv();

            // Validate generated parameters
            const validation = this.runAesValidationSuite(newKey, newIV);

            if (validation.overallValid) {
                console.log("✓ Generated parameters validated successfully");
                return {
                    key: newKey,
                    iv: newIV,
                    valid: true
                };
            } else {
                throw new Error("Generated parameters failed validation");
            }
        } catch (error) {
            console.error("❌ Parameter generation failed:", error.message);
            return {
                key: this.PRODUCTION_AES_KEY,
                iv: this.PRODUCTION_AES_IV,
                valid: false,
                error: error.message
            };
        }
    },

    // Generate C# backend constants
    generateCSharpConstants: function(key, iv) {
        const useKey = key || this.PRODUCTION_AES_KEY;
        const useIV = iv || this.PRODUCTION_AES_IV;

        console.log("=== C# Backend Constants ===");
        console.log(`internal static readonly string AES_KEY = "${useKey}";`);
        console.log(`internal static readonly string AES_IV = "${useIV}";`);
        console.log("");
        console.log("// Validation in C#:");
        console.log("// Convert.FromBase64String(AES_KEY).Length should equal 32");
        console.log("// Convert.FromBase64String(AES_IV).Length should equal 16");
        console.log("");
        console.log("// Usage example:");
        console.log("// using (var aes = Aes.Create())");
        console.log("// {");
        console.log("//     aes.Key = Convert.FromBase64String(AES_KEY);");
        console.log("//     aes.IV = Convert.FromBase64String(AES_IV);");
        console.log("//     aes.Mode = CipherMode.CBC;");
        console.log("//     aes.Padding = PaddingMode.PKCS7;");
        console.log("// }");
    },

    // Existing methods continue below...

    // Hashes data using the specified algorithm (default is SHA-256)
    hashData: async function (data, algorithm = "SHA-256") {
        const encoder = new TextEncoder();
        const dataBuffer = encoder.encode(data);
        try {
            const hashBuffer = await crypto.subtle.digest(algorithm, dataBuffer);
            return this.arrayBufferToBase64(hashBuffer);
        } catch (error) {
            console.error("Hashing failed:", error);
            throw error;
        }
    },

    // Computes an HMAC signature for the given data
    signData: async function (data, keyString, algorithm = "SHA-256") {
        const encoder = new TextEncoder();
        const keyBuffer = encoder.encode(keyString);
        const dataBuffer = encoder.encode(data);

        const cryptoKey = await crypto.subtle.importKey(
            "raw",
            keyBuffer,
            { name: "HMAC", hash: { name: algorithm } },
            false,
            ["sign"]
        );

        const signatureBuffer = await crypto.subtle.sign("HMAC", cryptoKey, dataBuffer);
        const hexSignature = this.arrayBufferToHex(signatureBuffer);
        return hexSignature.substring(0, 16);
    },

    // Verifies an HMAC signature for the given data
    verifyHmac: async function (data, providedSignature, keyString, algorithm = "SHA-256") {
        const computedSignature = await this.signData(data, keyString, algorithm);
        return computedSignature === providedSignature;
    },

    // MD5 implementation for Gravatar URLs
    hashMD5: function(input) {
        function md5cycle(x, k) {
            var a = x[0], b = x[1], c = x[2], d = x[3];
            a = ff(a, b, c, d, k[0], 7, -680876936);
            d = ff(d, a, b, c, k[1], 12, -389564586);
            c = ff(c, d, a, b, k[2], 17, 606105819);
            b = ff(b, c, d, a, k[3], 22, -1044525330);
            a = ff(a, b, c, d, k[4], 7, -176418897);
            d = ff(d, a, b, c, k[5], 12, 1200080426);
            c = ff(c, d, a, b, k[6], 17, -1473231341);
            b = ff(b, c, d, a, k[7], 22, -45705983);
            a = ff(a, b, c, d, k[8], 7, 1770035416);
            d = ff(d, a, b, c, k[9], 12, -1958414417);
            c = ff(c, d, a, b, k[10], 17, -42063);
            b = ff(b, c, d, a, k[11], 22, -1990404162);
            a = ff(a, b, c, d, k[12], 7, 1804603682);
            d = ff(d, a, b, c, k[13], 12, -40341101);
            c = ff(c, d, a, b, k[14], 17, -1502002290);
            b = ff(b, c, d, a, k[15], 22, 1236535329);
            a = gg(a, b, c, d, k[1], 5, -165796510);
            d = gg(d, a, b, c, k[6], 9, -1069501632);
            c = gg(c, d, a, b, k[11], 14, 643717713);
            b = gg(b, c, d, a, k[0], 20, -373897302);
            a = gg(a, b, c, d, k[5], 5, -701558691);
            d = gg(d, a, b, c, k[10], 9, 38016083);
            c = gg(c, d, a, b, k[15], 14, -660478335);
            b = gg(b, c, d, a, k[4], 20, -405537848);
            a = gg(a, b, c, d, k[9], 5, 568446438);
            d = gg(d, a, b, c, k[14], 9, -1019803690);
            c = gg(c, d, a, b, k[3], 14, -187363961);
            b = gg(b, c, d, a, k[8], 20, 1163531501);
            a = gg(a, b, c, d, k[13], 5, -1444681467);
            d = gg(d, a, b, c, k[2], 9, -51403784);
            c = gg(c, d, a, b, k[7], 14, 1735328473);
            b = gg(b, c, d, a, k[12], 20, -1926607734);
            a = hh(a, b, c, d, k[5], 4, -378558);
            d = hh(d, a, b, c, k[8], 11, -2022574463);
            c = hh(c, d, a, b, k[11], 16, 1839030562);
            b = hh(b, c, d, a, k[14], 23, -35309556);
            a = hh(a, b, c, d, k[1], 4, -1530992060);
            d = hh(d, a, b, c, k[4], 11, 1272893353);
            c = hh(c, d, a, b, k[7], 16, -155497632);
            b = hh(b, c, d, a, k[10], 23, -1094730640);
            a = hh(a, b, c, d, k[13], 4, 681279174);
            d = hh(d, a, b, c, k[0], 11, -358537222);
            c = hh(c, d, a, b, k[3], 16, -722521979);
            b = hh(b, c, d, a, k[6], 23, 76029189);
            a = hh(a, b, c, d, k[9], 4, -640364487);
            d = hh(d, a, b, c, k[12], 11, -421815835);
            c = hh(c, d, a, b, k[15], 16, 530742520);
            b = hh(b, c, d, a, k[2], 23, -995338651);
            a = ii(a, b, c, d, k[0], 6, -198630844);
            d = ii(d, a, b, c, k[7], 10, 1126891415);
            c = ii(c, d, a, b, k[14], 15, -1416354905);
            b = ii(b, c, d, a, k[5], 21, -57434055);
            a = ii(a, b, c, d, k[12], 6, 1700485571);
            d = ii(d, a, b, c, k[3], 10, -1894986606);
            c = ii(c, d, a, b, k[10], 15, -1051523);
            b = ii(b, c, d, a, k[1], 21, -2054922799);
            a = ii(a, b, c, d, k[8], 6, 1873313359);
            d = ii(d, a, b, c, k[15], 10, -30611744);
            c = ii(c, d, a, b, k[6], 15, -1560198380);
            b = ii(b, c, d, a, k[13], 21, 1309151649);
            a = ii(a, b, c, d, k[4], 6, -145523070);
            d = ii(d, a, b, c, k[11], 10, -1120210379);
            c = ii(c, d, a, b, k[2], 15, 718787259);
            b = ii(b, c, d, a, k[9], 21, -343485551);
            x[0] = add32(a, x[0]);
            x[1] = add32(b, x[1]);
            x[2] = add32(c, x[2]);
            x[3] = add32(d, x[3]);
        }

        function cmn(q, a, b, x, s, t) {
            a = add32(add32(a, q), add32(x, t));
            return add32((a << s) | (a >>> (32 - s)), b);
        }
        function ff(a, b, c, d, x, s, t) { return cmn((b & c) | ((~b) & d), a, b, x, s, t); }
        function gg(a, b, c, d, x, s, t) { return cmn((b & d) | (c & (~d)), a, b, x, s, t); }
        function hh(a, b, c, d, x, s, t) { return cmn(b ^ c ^ d, a, b, x, s, t); }
        function ii(a, b, c, d, x, s, t) { return cmn(c ^ (b | (~d)), a, b, x, s, t); }

        function md51(s) {
            var n = s.length, state = [1732584193, -271733879, -1732584194, 271733878], i;
            for (i = 64; i <= s.length; i += 64) {
                md5cycle(state, md5blk(s.substring(i - 64, i)));
            }
            s = s.substring(i - 64);
            var tail = [0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0];
            for (i = 0; i < s.length; i++)
                tail[i >> 2] |= s.charCodeAt(i) << ((i % 4) << 3);
            tail[i >> 2] |= 0x80 << ((i % 4) << 3);
            if (i > 55) {
                md5cycle(state, tail);
                for (i = 0; i < 16; i++) tail[i] = 0;
            }
            tail[14] = n * 8;
            md5cycle(state, tail);
            return state;
        }

        function md5blk(s) {
            var md5blks = [], i;
            for (i = 0; i < 64; i += 4) {
                md5blks[i >> 2] = s.charCodeAt(i) + (s.charCodeAt(i + 1) << 8) + (s.charCodeAt(i + 2) << 16) + (s.charCodeAt(i + 3) << 24);
            }
            return md5blks;
        }

        function rhex(n) {
            var hex_chr = '0123456789abcdef'.split('');
            var s = '', j = 0;
            for (var i = 0; i < 4; i++)
                s += hex_chr[(n >> (j + 4)) & 0x0F] + hex_chr[(n >> j) & 0x0F], j += 8;
            return s;
        }

        function hex(x) {
            for (var i = 0; i < x.length; i++) x[i] = rhex(x[i]);
            return x.join('');
        }

        function add32(a, b) { return (a + b) & 0xFFFFFFFF; }

        if (input === '') return 'd41d8cd98f00b204e9800998ecf8427e';
        return hex(md51(input));
    },

    // SHA-256 as hex for development/testing
    hashSHA256AsHex: async function(input) {
        const encoder = new TextEncoder();
        const data = encoder.encode(input);
        const hashBuffer = await crypto.subtle.digest('SHA-256', data);
        const hashArray = Array.from(new Uint8Array(hashBuffer));
        return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    }
};

// Helper function for PKCE OAuth flow
window.generateCodeChallenge = async function(codeVerifier) {
    const encoder = new TextEncoder();
    const data = encoder.encode(codeVerifier);
    const hash = await crypto.subtle.digest('SHA-256', data);
    return base64UrlEncode(hash);
};

function base64UrlEncode(buffer) {
    var base64 = btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)));
    return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// Comprehensive validation and testing suite
async function runFullCryptoValidationSuite() {
    console.log("=== Comprehensive Cryptography Handler Validation ===");

    // Test with production parameters
    const productionValidation = window.cryptographyHandler.runAesValidationSuite();

    if (productionValidation.overallValid) {
        console.log("✓ Production parameters validated");

        // Run integration test
        const integrationSuccess = await window.cryptographyHandler.testAesIntegration();

        if (integrationSuccess) {
            console.log("✓ All tests passed with production parameters");
            window.cryptographyHandler.generateCSharpConstants();
        }
    } else {
        console.log("❌ Production parameters failed validation");
    }

    // Test parameter resolution with invalid inputs
    console.log("\n=== Testing Parameter Resolution ===");
    await window.cryptographyHandler.testAesIntegration("invalid_key", "invalid_iv");
    await window.cryptographyHandler.testAesIntegration(null, null);

    // Test new parameter generation
    console.log("\n=== Testing New Parameter Generation ===");
    const newParams = await window.cryptographyHandler.generateSecureAesParameters();
    if (newParams.valid) {
        await window.cryptographyHandler.testAesIntegration(newParams.key, newParams.iv);
        window.cryptographyHandler.generateCSharpConstants(newParams.key, newParams.iv);
    }

    console.log("\n=== Validation Suite Complete ===");
}

// Auto-execute comprehensive validation
runFullCryptoValidationSuite();