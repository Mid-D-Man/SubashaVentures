// wwwroot/js/imageCompressor.js
// Image compression utility using Sharp.js
// Requires: <script src="https://cdn.jsdelivr.net/npm/sharp@0.32.0/dist/sharp.js"></script>

window.imageCompressor = {
    /**
     * Compress an image from base64 string
     * @param {string} base64 - Base64 encoded image
     * @param {number} quality - Quality 1-100 (default: 80)
     * @param {number} maxWidth - Max width in pixels (default: 2000)
     * @param {number} maxHeight - Max height in pixels (default: 2000)
     * @param {string} outputFormat - Output MIME type (default: image/jpeg)
     * @returns {Promise<Object>} Compression result
     */
    async compressImage(base64, quality = 80, maxWidth = 2000, maxHeight = 2000, outputFormat = 'image/jpeg') {
        try {
            if (!window.sharp) {
                console.warn('Sharp.js not loaded, returning original');
                return {
                    success: true,
                    base64Data: base64,
                    compressedSize: 0
                };
            }

            // Convert base64 to buffer
            const buffer = Buffer.from(base64, 'base64');

            // Create sharp instance
            let image = sharp(buffer);

            // Get metadata
            const metadata = await image.metadata();

            // Calculate new dimensions maintaining aspect ratio
            const newDimensions = this._calculateDimensions(
                metadata.width,
                metadata.height,
                maxWidth,
                maxHeight
            );

            // Resize if needed
            if (newDimensions.width !== metadata.width || newDimensions.height !== metadata.height) {
                image = image.resize(newDimensions.width, newDimensions.height, {
                    fit: 'inside',
                    withoutEnlargement: true,
                    position: 'center'
                });
            }

            let compressed;

            // Process based on output format
            if (outputFormat === 'image/webp') {
                compressed = await image
                    .webp({
                        quality: Math.max(1, Math.min(100, quality)),
                        alphaQuality: quality,
                        effort: 6
                    })
                    .toBuffer();
            }
            else if (outputFormat === 'image/png') {
                // PNG with adaptive filtering
                compressed = await image
                    .png({
                        quality: Math.max(1, Math.min(100, quality)),
                        effort: 10,
                        adaptiveFiltering: true,
                        compressionLevel: 9
                    })
                    .toBuffer();
            }
            else if (outputFormat === 'image/gif') {
                // GIF - limited compression options
                compressed = await image
                    .gif()
                    .toBuffer();
            }
            else {
                // Default to JPEG
                compressed = await image
                    .jpeg({
                        quality: Math.max(1, Math.min(100, quality)),
                        mozjpeg: true,
                        progressive: true
                    })
                    .toBuffer();
            }

            // Convert back to base64
            const compressedBase64 = compressed.toString('base64');
            const originalSize = buffer.length;
            const compressedSize = compressed.length;
            const ratio = (originalSize - compressedSize) / originalSize;

            return {
                success: true,
                base64Data: compressedBase64,
                compressedSize: compressedSize,
                originalSize: originalSize,
                compressionRatio: Math.max(0, ratio),
                dimensions: {
                    width: newDimensions.width,
                    height: newDimensions.height,
                    original: {
                        width: metadata.width,
                        height: metadata.height
                    }
                },
                format: outputFormat,
                errorMessage: null
            };
        }
        catch (error) {
            console.error('Image compression error:', error);
            return {
                success: false,
                errorMessage: error.message || 'Unknown compression error',
                base64Data: null,
                compressedSize: 0
            };
        }
    },

    /**
     * Get image dimensions without full processing
     * @param {string} base64 - Base64 encoded image
     * @returns {Promise<Object|null>} Image dimensions or null
     */
    async getImageDimensions(base64) {
        try {
            if (!window.sharp) {
                return null;
            }

            const buffer = Buffer.from(base64, 'base64');
            const metadata = await sharp(buffer).metadata();

            return {
                width: metadata.width,
                height: metadata.height,
                format: metadata.format,
                space: metadata.space,
                channels: metadata.channels,
                depth: metadata.depth,
                density: metadata.density,
                hasAlpha: metadata.hasAlpha,
                orientation: metadata.orientation,
                pages: metadata.pages,
                pageHeight: metadata.pageHeight,
                loop: metadata.loop,
                delay: metadata.delay,
                pagDelay: metadata.pagDelay,
                hasProfile: metadata.hasProfile,
                exif: metadata.exif,
                icc: metadata.icc
            };
        }
        catch (error) {
            console.error('Error getting image dimensions:', error);
            return null;
        }
    },

    /**
     * Create thumbnail from base64 image
     * @param {string} base64 - Base64 encoded image
     * @param {number} size - Thumbnail size (default: 300)
     * @param {number} quality - Quality 1-100 (default: 85)
     * @returns {Promise<Object>} Thumbnail compression result
     */
    async createThumbnail(base64, size = 300, quality = 85) {
        return this.compressImage(base64, quality, size, size, 'image/jpeg');
    },

    /**
     * Validate image and get info
     * @param {string} base64 - Base64 encoded image
     * @param {number} maxSizeBytes - Max file size in bytes (default: 50MB)
     * @returns {Promise<Object>} Validation result
     */
    async validateImage(base64, maxSizeBytes = 50 * 1024 * 1024) {
        try {
            const buffer = Buffer.from(base64, 'base64');
            const size = buffer.length;

            if (size > maxSizeBytes) {
                return {
                    isValid: false,
                    errorMessage: `File size ${this._formatBytes(size)} exceeds limit of ${this._formatBytes(maxSizeBytes)}`,
                    fileSize: size
                };
            }

            if (!window.sharp) {
                return {
                    isValid: true,
                    fileSize: size,
                    errorMessage: null
                };
            }

            const metadata = await sharp(buffer).metadata();
            const supportedFormats = ['jpeg', 'jpg', 'png', 'webp', 'gif', 'avif', 'tiff', 'svg'];

            if (!supportedFormats.includes(metadata.format?.toLowerCase())) {
                return {
                    isValid: false,
                    errorMessage: `Unsupported format: ${metadata.format}`,
                    fileSize: size,
                    format: metadata.format
                };
            }

            return {
                isValid: true,
                fileSize: size,
                format: metadata.format,
                width: metadata.width,
                height: metadata.height,
                hasAlpha: metadata.hasAlpha,
                errorMessage: null
            };
        }
        catch (error) {
            console.error('Error validating image:', error);
            return {
                isValid: false,
                errorMessage: error.message,
                fileSize: 0
            };
        }
    },

    /**
     * Calculate dimensions maintaining aspect ratio
     * @private
     */
    _calculateDimensions(width, height, maxWidth, maxHeight) {
        let newWidth = width;
        let newHeight = height;

        // Constrain to maxWidth
        if (newWidth > maxWidth) {
            newHeight = Math.round((newHeight * maxWidth) / newWidth);
            newWidth = maxWidth;
        }

        // Constrain to maxHeight
        if (newHeight > maxHeight) {
            newWidth = Math.round((newWidth * maxHeight) / newHeight);
            newHeight = maxHeight;
        }

        return { width: newWidth, height: newHeight };
    },

    /**
     * Format bytes to human readable format
     * @private
     */
    _formatBytes(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
    },

    /**
     * Convert file input to base64
     * @param {File} file - File object from input
     * @returns {Promise<string>} Base64 string
     */
    async fileToBase64(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                // Extract base64 part (after data:image/...;base64,)
                const result = reader.result.split(',')[1];
                resolve(result);
            };
            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }
};

console.log('Image Compressor module loaded');