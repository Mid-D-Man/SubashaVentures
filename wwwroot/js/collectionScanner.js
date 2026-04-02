// wwwroot/js/collectionScanner.js
// Nimiq qr-scanner adapted for SubashaVentures admin collection scanning.
// Requires qr-scanner.umd.min.js and qr-scanner-worker.min.js in wwwroot/js/

class CollectionScannerManager {
    constructor(videoId) {
        this.videoId       = videoId;
        this.video         = null;
        this.qrScanner     = null;
        this.isScanning    = false;
        this.dotNetRef     = null;
        this.cameras       = [];
        this.currentCamIdx = 0;
    }

    async init(dotNetObject) {
        this.dotNetRef = dotNetObject;
        this.video     = document.getElementById(this.videoId);

        if (!this.video) {
            console.error(`[CollectionScanner] Video element #${this.videoId} not found`);
            return false;
        }

        try {
            this.cameras = await QrScanner.listCameras(true);
            console.log(`[CollectionScanner] Found ${this.cameras.length} camera(s)`);

            const backIdx = this.cameras.findIndex(c =>
                /back|rear|environment/i.test(c.label)
            );
            this.currentCamIdx = backIdx >= 0 ? backIdx : 0;

            this.qrScanner = new QrScanner(
                this.video,
                result => this._onResult(result),
                {
                    preferredCamera:          this.cameras[this.currentCamIdx]?.id ?? 'environment',
                    highlightScanRegion:      true,
                    highlightCodeOutline:     true,
                    returnDetailedScanResult: true,
                    maxScansPerSecond:        5,
                    calculateScanRegion: (v) => {
                        const size = Math.min(v.videoWidth, v.videoHeight) * 0.75;
                        return {
                            x: Math.round((v.videoWidth  - size) / 2),
                            y: Math.round((v.videoHeight - size) / 2),
                            width:  Math.round(size),
                            height: Math.round(size),
                        };
                    },
                }
            );

            return true;
        } catch (err) {
            console.error('[CollectionScanner] Init error:', err);
            return false;
        }
    }

    async start() {
        if (!this.qrScanner) return false;
        try {
            await this.qrScanner.start();
            this.isScanning = true;
            return true;
        } catch (err) {
            console.error('[CollectionScanner] Start error:', err);
            this.isScanning = false;
            return false;
        }
    }

    stop() {
        if (this.qrScanner && this.isScanning) {
            try {
                this.qrScanner.stop();
            } catch (err) {
                console.error('[CollectionScanner] Stop error:', err);
            } finally {
                this.isScanning = false;
            }
        }
    }

    async switchCamera() {
        if (!this.qrScanner || this.cameras.length <= 1) return false;
        try {
            const wasScanning = this.isScanning;
            if (wasScanning) { this.qrScanner.stop(); this.isScanning = false; }

            this.currentCamIdx = (this.currentCamIdx + 1) % this.cameras.length;
            await this.qrScanner.setCamera(this.cameras[this.currentCamIdx].id);

            if (wasScanning) { await this.qrScanner.start(); this.isScanning = true; }
            return true;
        } catch (err) {
            console.error('[CollectionScanner] switchCamera error:', err);
            return false;
        }
    }

    destroy() {
        this.stop();
        if (this.qrScanner) {
            try { this.qrScanner.destroy(); } catch (_) {}
            this.qrScanner = null;
        }
    }

    _onResult(result) {
        if (!result?.data || !this.dotNetRef) return;
        try {
            this.dotNetRef.invokeMethodAsync('OnQrScanned', result.data);
        } catch (err) {
            console.error('[CollectionScanner] invokeMethodAsync error:', err);
        }
    }
}

// ── Global registry ──────────────────────────────────────────────────────────

const _scanners = new Map();

window.collectionScanner = {

    // Always destroy any existing scanner instance before creating a new one.
    // This is critical because Blazor re-renders create a NEW <video> element
    // with the same ID each time _scannerReady toggles. If we reused the old
    // CollectionScannerManager it would hold a reference to the now-detached
    // (old) video element and QrScanner.start() would silently fail.
    async start(videoId, dotNetRef) {
        const existing = _scanners.get(videoId);
        if (existing) {
            existing.destroy();
            _scanners.delete(videoId);
        }

        const scanner = new CollectionScannerManager(videoId);
        const ok = await scanner.init(dotNetRef);
        if (!ok) return false;

        _scanners.set(videoId, scanner);
        return scanner.start();
    },

    stop(videoId) {
        _scanners.get(videoId)?.stop();
    },

    async switchCamera(videoId) {
        return _scanners.get(videoId)?.switchCamera() ?? false;
    },

    destroy(videoId) {
        const s = _scanners.get(videoId);
        if (s) { s.destroy(); _scanners.delete(videoId); }
    },

    destroyAll() {
        _scanners.forEach(s => s.destroy());
        _scanners.clear();
    },
};

window.addEventListener('beforeunload', () => window.collectionScanner.destroyAll());
