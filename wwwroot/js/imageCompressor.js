// wwwroot/js/imageCompressor.js - FIXED BLOB URL LOADING
// Browser-native image compression using Canvas API

window.imageCompressor = {
    /**
     * Compress an image from base64 string using Canvas API
     * @param {string} base64 - Base64 encoded image
     * @param {number} quality - Quality 0-100 (default: 80)
     * @param {number} maxWidth - Max width in pixels (default: 2000)
     * @param {number} maxHeight - Max height in pixels (default: 2000)
     * @param {string} outputFormat - Output MIME type (default: image/jpeg)
     * @returns {Promise<Object>} Compression result
     */
    async compressImage(base64, quality = 80, maxWidth = 2000, maxHeight = 2000, outputFormat = 'image/jpeg') {
        try {
            console.log('Starting image compression...');
            
            // FIXED: Better base64 to blob conversion
            const blob = this._base64ToBlob(base64);
            const originalSize = blob.size;
            
            console.log('Original blob size:', originalSize, 'bytes');
            
            // FIXED: Load image with proper error handling
            const img = await this._loadImageSafe(blob);
            
            console.log('Image loaded successfully:', img.width, 'x', img.height);
            
            // Calculate new dimensions
            const dimensions = this._calculateDimensions(
                img.width,
                img.height,
                maxWidth,
                maxHeight
            );
            
            console.log('Target dimensions:', dimensions.width, 'x', dimensions.height);
            
            // Create canvas and draw resized image
            const canvas = document.createElement('canvas');
            canvas.width = dimensions.width;
            canvas.height = dimensions.height;
            
            const ctx = canvas.getContext('2d');
            
            // Use better image smoothing
            ctx.imageSmoothingEnabled = true;
            ctx.imageSmoothingQuality = 'high';
            
            // Draw image
            ctx.drawImage(img, 0, 0, dimensions.width, dimensions.height);
            
            // Convert canvas to blob with quality
            const compressedBlob = await this._canvasToBlob(
                canvas, 
                outputFormat, 
                quality / 100
            );
            
            console.log('Compressed blob size:', compressedBlob.size, 'bytes');
            
            // Convert blob back to base64
            const compressedBase64 = await this._blobToBase64(compressedBlob);
            
            const result = {
                success: true,
                base64Data: compressedBase64,
                compressedSize: compressedBlob.size,
                originalSize: originalSize,
                compressionRatio: Math.max(0, (originalSize - compressedBlob.size) / originalSize),
                dimensions: {
                    width: dimensions.width,
                    height: dimensions.height,
                    original: {
                        width: img.width,
                        height: img.height
                    }
                },
                format: outputFormat,
                errorMessage: null
            };
            
            console.log('Compression successful:', result);
            return result;
        }
        catch (error) {
            console.error('Image compression error:', error);
            return {
                success: false,
                errorMessage: error.message || 'Unknown compression error',
                base64Data: null,
                compressedSize: 0,
                originalSize: 0,
                compressionRatio: 0
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
            const blob = this._base64ToBlob(base64);
            const img = await this._loadImageSafe(blob);
            
            return {
                width: img.width,
                height: img.height,
                naturalWidth: img.naturalWidth,
                naturalHeight: img.naturalHeight
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
     * @param {number} quality - Quality 0-100 (default: 85)
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
            const blob = this._base64ToBlob(base64);
            const size = blob.size;
            
            if (size > maxSizeBytes) {
                return {
                    isValid: false,
                    errorMessage: `File size ${this._formatBytes(size)} exceeds limit of ${this._formatBytes(maxSizeBytes)}`,
                    fileSize: size
                };
            }
            
            // Try to load image to verify format
            try {
                const img = await this._loadImageSafe(blob);
                
                return {
                    isValid: true,
                    fileSize: size,
                    format: blob.type,
                    width: img.width,
                    height: img.height,
                    errorMessage: null
                };
            }
            catch (imgError) {
                return {
                    isValid: false,
                    errorMessage: 'Invalid or corrupted image file',
                    fileSize: size
                };
            }
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
     * Convert file input to base64
     * @param {File} file - File object from input
     * @returns {Promise<string>} Base64 string (without data URL prefix)
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
    },

    // ========== PRIVATE HELPER METHODS ==========

    /**
     * FIXED: Convert base64 string to Blob with better error handling
     * @private
     */
    _base64ToBlob(base64) {
        try {
            // Handle both with and without data URL prefix
            let mimeType = 'image/jpeg';
            let base64Data = base64;
            
            if (base64.includes(',')) {
                const parts = base64.split(',');
                const mimeMatch = parts[0].match(/:(.*?);/);
                if (mimeMatch) {
                    mimeType = mimeMatch[1];
                }
                base64Data = parts[1];
            }
            
            // Decode base64
            const binaryString = atob(base64Data);
            const len = binaryString.length;
            const bytes = new Uint8Array(len);
            
            for (let i = 0; i < len; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }
            
            return new Blob([bytes], { type: mimeType });
        }
        catch (error) {
            console.error('Error converting base64 to blob:', error);
            throw new Error('Failed to convert base64 to blob: ' + error.message);
        }
    },

    /**
     * Convert Blob to base64 string (without prefix)
     * @private
     */
    async _blobToBase64(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                // Remove data URL prefix
                const result = reader.result.split(',')[1];
                resolve(result);
            };
            reader.onerror = reject;
            reader.readAsDataURL(blob);
        });
    },

    /**
     * Load image from blob with better error handling and timeout
     * @private
     */
    _loadImageSafe(blob) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            const objectUrl = URL.createObjectURL(blob);
            
            // Set timeout for loading
            const timeout = setTimeout(() => {
                URL.revokeObjectURL(objectUrl);
                reject(new Error('Image load timeout'));
            }, 30000); // 30 second timeout
            
            img.onload = () => {
                clearTimeout(timeout);
                URL.revokeObjectURL(objectUrl);
                
                // Validate image loaded properly
                if (img.width === 0 || img.height === 0) {
                    reject(new Error('Invalid image dimensions'));
                    return;
                }
                
                resolve(img);
            };
            
            img.onerror = (error) => {
                clearTimeout(timeout);
                URL.revokeObjectURL(objectUrl);
                console.error('Image load error:', error);
                reject(new Error('Failed to load image - may be corrupted or unsupported format'));
            };
            
            // CRITICAL: Set crossOrigin before src
            img.crossOrigin = 'anonymous';
            img.src = objectUrl;
        });
    },

    /**
     * Convert canvas to blob with quality
     * @private
     */
    _canvasToBlob(canvas, mimeType, quality) {
        return new Promise((resolve, reject) => {
            // Ensure quality is between 0 and 1
            const normalizedQuality = Math.max(0, Math.min(1, quality));
            
            canvas.toBlob(
                (blob) => {
                    if (blob) {
                        resolve(blob);
                    } else {
                        reject(new Error('Failed to create blob from canvas'));
                    }
                },
                mimeType,
                normalizedQuality
            );
        });
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
    }
};

console.log('✓ Image Compressor (Canvas API - Fixed) loaded successfully');
