// wwwroot/service-worker.published.js
// SubashaVentures PWA Service Worker
// Purpose: Asset caching for performance + PWA installability
// NOTE: No offline mode. If network fails, requests fail naturally.

self.importScripts('./service-worker-assets.js');

const APP_VERSION = self.assetsManifest.version;
const CACHE_NAME = `subasha-cache-v${APP_VERSION}`;
const CACHE_NAME_PREFIX = 'subasha-cache-v';

// Asset types to cache for performance (not offline)
const CACHEABLE_ASSETS = [
    /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/,
    /\.js$/, /\.json$/, /\.css$/,
    /\.woff$/, /\.woff2$/,
    /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/,
    /\.blat$/, /\.dat$/
];

const EXCLUDE_FROM_CACHE = [
    /^service-worker\.js$/,
    /\/api\//,
    /\/_blazor\//,
];

// ─── Install ────────────────────────────────────────────────────────────────
self.addEventListener('install', event => {
    console.log(`[SW] Installing v${APP_VERSION}`);
    event.waitUntil(installCache());
    // Take control immediately — no need to wait for old SW to die
    self.skipWaiting();
});

async function installCache() {
    const cache = await caches.open(CACHE_NAME);

    const assets = self.assetsManifest.assets
        .filter(a => CACHEABLE_ASSETS.some(p => p.test(a.url)))
        .filter(a => !EXCLUDE_FROM_CACHE.some(p => p.test(a.url)));

    const requests = assets.map(a =>
        new Request(a.url, { integrity: a.hash, cache: 'no-cache' })
    );

    // Cache in batches — avoids memory spikes on large Blazor WASM bundles
    const BATCH = 10;
    let cached = 0;
    for (let i = 0; i < requests.length; i += BATCH) {
        const batch = requests.slice(i, i + BATCH);
        const results = await Promise.allSettled(
            batch.map(async req => {
                try {
                    const res = await fetch(req.clone(), {
                        cache: 'no-cache',
                        signal: AbortSignal.timeout(15000)
                    });
                    if (res.ok) {
                        await cache.put(req, res);
                        cached++;
                    }
                } catch {
                    console.warn(`[SW] Failed to cache: ${req.url}`);
                }
            })
        );
    }
    console.log(`[SW] Cached ${cached}/${requests.length} assets`);
}

// ─── Activate ───────────────────────────────────────────────────────────────
self.addEventListener('activate', event => {
    console.log(`[SW] Activating v${APP_VERSION}`);
    event.waitUntil(activate());
    self.clients.claim();
});

async function activate() {
    // Delete old versioned caches
    const keys = await caches.keys();
    await Promise.all(
        keys
            .filter(k => k.startsWith(CACHE_NAME_PREFIX) && k !== CACHE_NAME)
            .map(k => {
                console.log(`[SW] Deleting old cache: ${k}`);
                return caches.delete(k);
            })
    );
    console.log('[SW] Activation complete');
}

// ─── Fetch ──────────────────────────────────────────────────────────────────
self.addEventListener('fetch', event => {
    const req = event.request;

    // Only handle GET requests from same origin
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    // Skip API and Blazor signalR calls — always go to network
    if (EXCLUDE_FROM_CACHE.some(p => p.test(url.pathname))) return;

    event.respondWith(handleFetch(req));
});

async function handleFetch(req) {
    const url = new URL(req.url);

    // For navigation requests (page loads), serve index.html from cache if available
    // but always try network first so fresh HTML is served
    if (req.mode === 'navigate') {
        return networkFirst(req, './index.html');
    }

    // For static assets (JS/CSS/WASM/fonts/images) — cache first, then network
    if (CACHEABLE_ASSETS.some(p => p.test(url.pathname))) {
        return cacheFirst(req);
    }

    // Everything else — network only (no offline fallback)
    return fetch(req);
}

// Cache-first: serve from cache, update in background
async function cacheFirst(req) {
    const cached = await caches.match(req);
    if (cached) {
        // Optionally update cache in background (stale-while-revalidate)
        return cached;
    }
    // Not cached — fetch and cache it
    try {
        const res = await fetch(req);
        if (res.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(req, res.clone()).catch(() => {});
        }
        return res;
    } catch {
        // No offline fallback — let it fail naturally
        return new Response(null, { status: 503, statusText: 'Offline' });
    }
}

// Network-first: try network, fall back to cache (for HTML only)
async function networkFirst(req, fallbackUrl) {
    try {
        const res = await fetch(req, {
            cache: 'no-cache',
            signal: AbortSignal.timeout(8000)
        });
        if (res.ok) {
            // Cache the fresh HTML for next time
            const cache = await caches.open(CACHE_NAME);
            cache.put(req, res.clone()).catch(() => {});
        }
        return res;
    } catch {
        // Offline — serve cached HTML so the app shell loads
        const cached = await caches.match(req)
            || await caches.match(fallbackUrl);
        if (cached) return cached;
        // No cache at all — fail naturally
        return new Response(null, { status: 503, statusText: 'Offline' });
    }
}

// ─── Messages ───────────────────────────────────────────────────────────────
self.addEventListener('message', event => {
    if (!event.data) return;

    if (event.data.type === 'SKIP_WAITING') {
        console.log('[SW] Skip waiting triggered');
        self.skipWaiting();
    }
});
