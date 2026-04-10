// wwwroot/js/imageCompressor.js - WebP-first compression
// Browser-native image compression using Canvas API
// WebP is now the default output format (25-34% smaller than JPEG, 97% browser support)

const WEBP_SUPPORTED = (() => {
    try {
        const canvas = document.createElement('canvas');
        canvas.width = 1;
        canvas.height = 1;
        return canvas.toDataURL('image/webp').startsWith('data:image/webp');
    } catch {
        return false;
    }
})();

console.log(`[ImageCompressor] WebP support: ${WEBP_SUPPORTED}`);

window.imageCompressor = {

    /**
     * Compress an image from base64 string using Canvas API
     * WebP is used by default when supported (25-34% smaller than JPEG for ecommerce)
     *
     * @param {string} base64           - Base64 encoded image
     * @param {number} quality          - Quality 0-100 (default: 85)
     * @param {number} maxWidth         - Max width in pixels (default: 2000)
     * @param {number} maxHeight        - Max height in pixels (default: 2000)
     * @param {string} outputFormat     - MIME type, e.g. 'image/webp' or 'image/jpeg'.
     *                                    Pass 'auto' (default) to use WebP when supported,
     *                                    else fall back to JPEG.
     * @returns {Promise<Object>} Compression result
     */
    async compressImage(
        base64,
        quality      = 85,
        maxWidth     = 2000,
        maxHeight    = 2000,
        outputFormat = 'auto'
    ) {
        try {
            // Resolve format
            const format = this._resolveFormat(outputFormat);
            console.log(`[ImageCompressor] Compressing → ${format} @ quality ${quality}`);

            const blob        = this._base64ToBlob(base64);
            const originalSize = blob.size;

            const img        = await this._loadImageSafe(blob);
            const dimensions = this._calculateDimensions(img.width, img.height, maxWidth, maxHeight);

            const canvas     = document.createElement('canvas');
            canvas.width     = dimensions.width;
            canvas.height    = dimensions.height;

            const ctx = canvas.getContext('2d');
            ctx.imageSmoothingEnabled = true;
            ctx.imageSmoothingQuality = 'high';
            ctx.drawImage(img, 0, 0, dimensions.width, dimensions.height);

            const compressedBlob   = await this._canvasToBlob(canvas, format, quality / 100);
            const compressedBase64 = await this._blobToBase64(compressedBlob);

            const ratio = Math.max(0, (originalSize - compressedBlob.size) / originalSize);

            console.log(
                `[ImageCompressor] ✓ ${this._formatBytes(originalSize)} → ` +
                `${this._formatBytes(compressedBlob.size)} (${(ratio * 100).toFixed(1)}% saved) [${format}]`
            );

            return {
                success          : true,
                base64Data       : compressedBase64,
                compressedSize   : compressedBlob.size,
                originalSize     : originalSize,
                compressionRatio : ratio,
                dimensions       : {
                    width    : dimensions.width,
                    height   : dimensions.height,
                    original : { width: img.width, height: img.height }
                },
                format           : format,
                isWebP           : format === 'image/webp',
                errorMessage     : null
            };
        } catch (error) {
            console.error('[ImageCompressor] Error:', error);
            return {
                success          : false,
                errorMessage     : error.message || 'Unknown compression error',
                base64Data       : null,
                compressedSize   : 0,
                originalSize     : 0,
                compressionRatio : 0
            };
        }
    },

    /**
     * Convert an existing JPEG/PNG/GIF blob or base64 to WebP.
     * Falls back to JPEG when WebP is not supported.
     *
     * @param {string} base64  - Source image in any browser-renderable format
     * @param {number} quality - Quality 0-100
     * @returns {Promise<Object>} Conversion result
     */
    async convertToWebP(base64, quality = 85) {
        return this.compressImage(base64, quality, 4096, 4096, 'image/webp');
    },

    /**
     * Check whether the current browser can encode WebP via Canvas.
     */
    canEncodeWebP() {
        return WEBP_SUPPORTED;
    },

    /**
     * Get image dimensions without full processing.
     */
    async getImageDimensions(base64) {
        try {
            const blob = this._base64ToBlob(base64);
            const img  = await this._loadImageSafe(blob);
            return { width: img.width, height: img.height };
        } catch (error) {
            console.error('[ImageCompressor] getImageDimensions error:', error);
            return null;
        }
    },

    /**
     * Create a thumbnail (square crop, WebP by default).
     */
    async createThumbnail(base64, size = 300, quality = 85) {
        return this.compressImage(base64, quality, size, size, 'auto');
    },

    /**
     * Validate image and return metadata.
     */
    async validateImage(base64, maxSizeBytes = 50 * 1024 * 1024) {
        try {
            const blob = this._base64ToBlob(base64);
            const size = blob.size;

            if (size > maxSizeBytes) {
                return {
                    isValid      : false,
                    errorMessage : `File size ${this._formatBytes(size)} exceeds limit of ${this._formatBytes(maxSizeBytes)}`,
                    fileSize     : size
                };
            }

            const img = await this._loadImageSafe(blob);
            return {
                isValid      : true,
                fileSize     : size,
                format       : blob.type,
                width        : img.width,
                height       : img.height,
                errorMessage : null
            };
        } catch (error) {
            return { isValid: false, errorMessage: error.message, fileSize: 0 };
        }
    },

    /**
     * Convert a File object to base64 string (without data-URL prefix).
     */
    async fileToBase64(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload  = () => resolve(reader.result.split(',')[1]);
            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    },

    // ─── Private helpers ────────────────────────────────────────────────────

    _resolveFormat(requested) {
        if (requested === 'auto' || requested === 'image/webp') {
            return WEBP_SUPPORTED ? 'image/webp' : 'image/jpeg';
        }
        return requested || 'image/jpeg';
    },

    _base64ToBlob(base64) {
        let mimeType  = 'image/jpeg';
        let base64Data = base64;

        if (base64.includes(',')) {
            const parts    = base64.split(',');
            const mimeMatch = parts[0].match(/:(.*?);/);
            if (mimeMatch) mimeType = mimeMatch[1];
            base64Data = parts[1];
        }

        const binary = atob(base64Data);
        const bytes  = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return new Blob([bytes], { type: mimeType });
    },

    async _blobToBase64(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload  = () => resolve(reader.result.split(',')[1]);
            reader.onerror = reject;
            reader.readAsDataURL(blob);
        });
    },

    _loadImageSafe(blob) {
        return new Promise((resolve, reject) => {
            const img      = new Image();
            const objectUrl = URL.createObjectURL(blob);

            const timeout = setTimeout(() => {
                URL.revokeObjectURL(objectUrl);
                reject(new Error('Image load timeout (30s)'));
            }, 30_000);

            img.onload = () => {
                clearTimeout(timeout);
                URL.revokeObjectURL(objectUrl);
                if (img.width === 0 || img.height === 0) {
                    reject(new Error('Invalid image dimensions'));
                    return;
                }
                resolve(img);
            };

            img.onerror = (e) => {
                clearTimeout(timeout);
                URL.revokeObjectURL(objectUrl);
                reject(new Error('Failed to load image — may be corrupted or unsupported format'));
            };

            img.crossOrigin = 'anonymous';
            img.src = objectUrl;
        });
    },

    _canvasToBlob(canvas, mimeType, quality) {
        return new Promise((resolve, reject) => {
            canvas.toBlob(
                (blob) => blob ? resolve(blob) : reject(new Error('canvas.toBlob returned null')),
                mimeType,
                Math.max(0, Math.min(1, quality))
            );
        });
    },

    _calculateDimensions(width, height, maxWidth, maxHeight) {
        let w = width, h = height;
        if (w > maxWidth)  { h = Math.round(h * maxWidth / w);  w = maxWidth; }
        if (h > maxHeight) { w = Math.round(w * maxHeight / h); h = maxHeight; }
        return { width: w, height: h };
    },

    _formatBytes(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024, sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return (bytes / Math.pow(k, i)).toFixed(2) + ' ' + sizes[i];
    }
};

console.log('✅ ImageCompressor loaded — WebP-first mode:', WEBP_SUPPORTED ? 'ENABLED' : 'FALLBACK→JPEG');
