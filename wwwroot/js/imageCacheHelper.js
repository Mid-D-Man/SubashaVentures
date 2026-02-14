// wwwroot/js/imageCacheHelper.js - Fixed without exports
const CACHE_NAME = 'subasha-images-v1';
const CACHE_DURATION = 7 * 24 * 60 * 60 * 1000; // 7 days

window.imageCacheHelper = {
    async getCachedImage(imageUrl) {
        try {
            const cache = await caches.open(CACHE_NAME);
            const response = await cache.match(imageUrl);

            if (response) {
                const cachedTime = response.headers.get('x-cached-time');
                if (cachedTime) {
                    const age = Date.now() - parseInt(cachedTime);
                    if (age < CACHE_DURATION) {
                        console.log(`✓ Cache HIT: ${imageUrl.substring(0, 50)}...`);
                        return await response.blob();
                    }
                }
                await cache.delete(imageUrl);
                console.log(`⚠ Cache EXPIRED: ${imageUrl.substring(0, 50)}...`);
            }

            console.log(`✗ Cache MISS: ${imageUrl.substring(0, 50)}...`);
            return await this.fetchAndCacheImage(imageUrl, cache);
        } catch (error) {
            console.error('Cache error:', error);
            const response = await fetch(imageUrl);
            return await response.blob();
        }
    },

    async fetchAndCacheImage(imageUrl, cache) {
        try {
            const response = await fetch(imageUrl, {
                headers: { 'Cache-Control': 'max-age=604800' }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const responseClone = response.clone();
            const blob = await response.blob();

            const cachedResponse = new Response(blob, {
                status: responseClone.status,
                statusText: responseClone.statusText,
                headers: {
                    ...Object.fromEntries(responseClone.headers),
                    'x-cached-time': Date.now().toString()
                }
            });

            await cache.put(imageUrl, cachedResponse);
            console.log(`✓ Cached: ${imageUrl.substring(0, 50)}...`);

            return blob;
        } catch (error) {
            console.error(`Failed to fetch/cache ${imageUrl}:`, error);
            throw error;
        }
    },

    async preloadImages(imageUrls) {
        const cache = await caches.open(CACHE_NAME);
        const promises = imageUrls.map(url =>
            this.fetchAndCacheImage(url, cache).catch(err => {
                console.warn(`Failed to preload ${url}:`, err);
            })
        );

        await Promise.all(promises);
        console.log(`✓ Preloaded ${imageUrls.length} images`);
    },

    async clearImageCache() {
        try {
            await caches.delete(CACHE_NAME);
            console.log('✓ Image cache cleared');
            return true;
        } catch (error) {
            console.error('Failed to clear cache:', error);
            return false;
        }
    },

    async getCacheSize() {
        try {
            const cache = await caches.open(CACHE_NAME);
            const requests = await cache.keys();
            let totalSize = 0;

            for (const request of requests) {
                const response = await cache.match(request);
                if (response) {
                    const blob = await response.blob();
                    totalSize += blob.size;
                }
            }

            return {
                count: requests.length,
                size: totalSize,
                formatted: this.formatBytes(totalSize)
            };
        } catch (error) {
            console.error('Failed to get cache size:', error);
            return { count: 0, size: 0, formatted: '0 B' };
        }
    },

    async clearExpiredCache() {
        try {
            const cache = await caches.open(CACHE_NAME);
            const requests = await cache.keys();
            let cleared = 0;

            for (const request of requests) {
                const response = await cache.match(request);
                if (response) {
                    const cachedTime = response.headers.get('x-cached-time');
                    if (cachedTime) {
                        const age = Date.now() - parseInt(cachedTime);
                        if (age >= CACHE_DURATION) {
                            await cache.delete(request);
                            cleared++;
                        }
                    }
                }
            }

            console.log(`✓ Cleared ${cleared} expired cache entries`);
            return cleared;
        } catch (error) {
            console.error('Failed to clear expired cache:', error);
            return 0;
        }
    },

    formatBytes(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
    }
};

// Auto-cleanup on load
window.addEventListener('load', () => {
    window.imageCacheHelper.clearExpiredCache();
});

console.log('✅ Image Cache Helper loaded');