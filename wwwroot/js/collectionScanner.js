// wwwroot/js/collectionScanner.js
class CollectionScannerManager {
    constructor(videoId) {
        this.videoId = videoId;
        this.video = document.getElementById(videoId);
        this.qrScanner = null;
        this.isScanning = false;
        this.dotNetRef = null;
        this.cameras = [];
        this.currentCameraIndex = 0;
    }

    async init(dotNetObject) {
        this.dotNetRef = dotNetObject;

        if (!this.video) {
            console.error(`[CollectionScanner] Video element ${this.videoId} not found`);
            return false;
        }

        try {
            this.cameras = await QrScanner.listCameras(true);
            console.log(`[CollectionScanner] Found ${this.cameras.length} cameras:`, this.cameras);

            const envCameraIndex = this.cameras.findIndex(cam =>
                cam.label.toLowerCase().includes('back') ||
                cam.label.toLowerCase().includes('environment')
            );
            this.currentCameraIndex = envCameraIndex >= 0 ? envCameraIndex : 0;

            this.qrScanner = new QrScanner(
                this.video,
                result => this.handleScanResult(result),
                {
                    preferredCamera: this.cameras[this.currentCameraIndex]?.id || 'environment',
                    highlightScanRegion: false,
                    highlightCodeOutline: false,
                    returnDetailedScanResult: true,
                    maxScansPerSecond: 5,
                    calculateScanRegion: (video) => {
                        const scanSize = Math.min(video.videoWidth, video.videoHeight) * 0.8;
                        return {
                            x: Math.round((video.videoWidth - scanSize) / 2),
                            y: Math.round((video.videoHeight - scanSize) / 2),
                            width: Math.round(scanSize),
                            height: Math.round(scanSize),
                        };
                    }
                }
            );

            return true;
        } catch (error) {
            console.error('[CollectionScanner] Init failed:', error);
            return false;
        }
    }

    async start() {
        if (!this.qrScanner) return false;
        try {
            await this.qrScanner.start();
            this.isScanning = true;
            return true;
        } catch (error) {
            console.error('[CollectionScanner] Start failed:', error);
            this.isScanning = false;
            return false;
        }
    }

    stop() {
        if (this.qrScanner && this.isScanning) {
            try {
                this.qrScanner.stop();
                this.isScanning = false;
            } catch (error) {
                console.error('[CollectionScanner] Stop error:', error);
            }
        }
    }

    handleScanResult(result) {
        try {
            if (this.dotNetRef && result?.data) {
                this.dotNetRef.invokeMethodAsync('OnQrScanned', result.data);
            }
        } catch (error) {
            console.error('[CollectionScanner] Result handling error:', error);
        }
    }

    async switchCamera() {
        if (!this.qrScanner || this.cameras.length <= 1) return false;
        try {
            const wasScanning = this.isScanning;
            if (wasScanning) {
                this.qrScanner.stop();
                this.isScanning = false;
            }

            this.currentCameraIndex = (this.currentCameraIndex + 1) % this.cameras.length;
            const nextCamera = this.cameras[this.currentCameraIndex];
            await this.qrScanner.setCamera(nextCamera.id);

            if (wasScanning) {
                await this.qrScanner.start();
                this.isScanning = true;
            }

            return true;
        } catch (error) {
            console.error('[CollectionScanner] Camera switch failed:', error);
            return false;
        }
    }

    destroy() {
        try {
            this.stop();
            if (this.qrScanner) {
                this.qrScanner.destroy();
                this.qrScanner = null;
            }
        } catch (error) {
            console.error('[CollectionScanner] Destroy error:', error);
        }
    }
}

const _scannerInstances = new Map();

// Explicitly request camera permission first — this is what actually
// triggers the browser permission dialog. Without this call the browser
// may silently deny access or never show the prompt at all.
async function requestCameraPermission() {
    try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        // Permission granted — stop the temporary stream immediately,
        // the QrScanner will open its own stream when start() is called.
        stream.getTracks().forEach(track => track.stop());
        return true;
    } catch (error) {
        console.error('[CollectionScanner] Camera permission denied:', error);
        return false;
    }
}

window.collectionScanner = {
    async start(videoId, dotNetRef) {
        try {
            // Step 1: explicitly prompt for permission
            const permitted = await requestCameraPermission();
            if (!permitted) {
                console.error('[CollectionScanner] Camera permission not granted');
                return false;
            }

            // Step 2: destroy any stale instance
            const existing = _scannerInstances.get(videoId);
            if (existing) {
                existing.destroy();
                _scannerInstances.delete(videoId);
            }

            // Step 3: init and start
            const scanner = new CollectionScannerManager(videoId);
            const initSuccess = await scanner.init(dotNetRef);
            if (!initSuccess) return false;

            _scannerInstances.set(videoId, scanner);
            return await scanner.start();
        } catch (error) {
            console.error(`[CollectionScanner] start error for ${videoId}:`, error);
            return false;
        }
    },

    stop(videoId) {
        try {
            _scannerInstances.get(videoId)?.stop();
        } catch (error) {
            console.error(`[CollectionScanner] stop error for ${videoId}:`, error);
        }
    },

    async switchCamera(videoId) {
        try {
            return await _scannerInstances.get(videoId)?.switchCamera() ?? false;
        } catch (error) {
            console.error(`[CollectionScanner] switchCamera error for ${videoId}:`, error);
            return false;
        }
    },

    destroy(videoId) {
        const s = _scannerInstances.get(videoId);
        if (s) {
            s.destroy();
            _scannerInstances.delete(videoId);
        }
    },

    destroyAll() {
        _scannerInstances.forEach(s => s.destroy());
        _scannerInstances.clear();
    }
};

window.addEventListener('beforeunload', () => window.collectionScanner.destroyAll());
