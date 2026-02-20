// wwwroot/service-worker.published.js
// SubashaVentures Service Worker
// Purpose: Cache static assets for PERFORMANCE only.
// Offline mode: DISABLED. No network = browser error page. Full stop.

self.importScripts('./service-worker-assets.js');

const APP_VERSION = self.assetsManifest.version;
const CACHE_NAME = `subasha-cache-v${APP_VERSION}`;
const CACHE_NAME_PREFIX = 'subasha-cache-v';

// Only cache these asset types — purely for faster repeat loads
const CACHEABLE_EXTENSIONS = [
    /\.dll$/, /\.pdb$/, /\.wasm$/,
    /\.js$/, /\.css$/,
    /\.woff$/, /\.woff2$/,
    /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/,
    /\.blat$/, /\.dat$/,
];

const NEVER_CACHE = [
    /^service-worker\.js$/,
    /\/api\//,
    /\/_blazor\//,
    /\.html$/,   // ← Never cache HTML. Ever.
    /\.json$/,   // ← Never cache JSON (includes index config, manifests, appsettings)
];

// ─── Install ────────────────────────────────────────────────────────────────
self.addEventListener('install', event => {
    console.log(`[SW] Installing v${APP_VERSION}`);
    event.waitUntil(precacheAssets());
    self.skipWaiting();
});

async function precacheAssets() {
    const cache = await caches.open(CACHE_NAME);

    const assets = self.assetsManifest.assets
        .filter(a => CACHEABLE_EXTENSIONS.some(p => p.test(a.url)))
        .filter(a => !NEVER_CACHE.some(p => p.test(a.url)));

    const requests = assets.map(a =>
        new Request(a.url, { integrity: a.hash, cache: 'no-cache' })
    );

    const BATCH_SIZE = 10;
    let cached = 0;

    for (let i = 0; i < requests.length; i += BATCH_SIZE) {
        const batch = requests.slice(i, i + BATCH_SIZE);
        await Promise.allSettled(
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
                    console.warn(`[SW] Skipped (network error): ${req.url}`);
                }
            })
        );
    }

    console.log(`[SW] Precached ${cached}/${requests.length} static assets`);
}

// ─── Activate ───────────────────────────────────────────────────────────────
self.addEventListener('activate', event => {
    console.log(`[SW] Activating v${APP_VERSION}`);
    event.waitUntil(deleteOldCaches());
    self.clients.claim();
});

async function deleteOldCaches() {
    const keys = await caches.keys();
    await Promise.all(
        keys
            .filter(k => k.startsWith(CACHE_NAME_PREFIX) && k !== CACHE_NAME)
            .map(k => {
                console.log(`[SW] Deleting old cache: ${k}`);
                return caches.delete(k);
            })
    );
    console.log('[SW] Old caches cleared');
}

// ─── Fetch ──────────────────────────────────────────────────────────────────
self.addEventListener('fetch', event => {
    const req = event.request;
    const url = new URL(req.url);

    // Only intercept GET requests from this origin
    if (req.method !== 'GET') return;
    if (url.origin !== self.location.origin) return;

    // Skip Blazor internals and API calls entirely — go straight to network
    if (NEVER_CACHE.some(p => p.test(url.pathname))) return;

    // Navigation requests (typing URL, clicking links, refreshing page):
    // ALWAYS require network. No fallback. No cached HTML.
    // If offline → browser shows its own "No internet" error. That's correct.
    if (req.mode === 'navigate') return;

    // Static assets (WASM, DLLs, JS, CSS, fonts, images):
    // Serve from cache for performance. If not cached, fetch from network.
    // If network also fails → 503, no silent fallback.
    event.respondWith(serveAssetFromCache(req));
});

async function serveAssetFromCache(req) {
    // 1. Check cache first
    const cached = await caches.match(req);
    if (cached) {
        return cached;
    }

    // 2. Not in cache — fetch from network and cache the result
    try {
        const res = await fetch(req);
        if (res.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(req, res.clone()).catch(() => {});
        }
        return res;
    } catch {
        // Network failed and no cache — return a clean 503
        // No offline page, no app shell, nothing.
        return new Response(null, {
            status: 503,
            statusText: 'Service Unavailable — No network connection'
        });
    }
}

// ─── Messages ───────────────────────────────────────────────────────────────
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
