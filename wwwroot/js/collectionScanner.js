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
            console.error(`Video element ${this.videoId} not found`);
            return false;
        }

        try {
            this.cameras = await QrScanner.listCameras(true);
            console.log(`Found ${this.cameras.length} cameras:`, this.cameras);

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
            console.error('CollectionScanner init failed:', error);
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
            console.error('CollectionScanner start failed:', error);
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
                console.error('CollectionScanner stop error:', error);
            }
        }
    }

    handleScanResult(result) {
        try {
            if (this.dotNetRef && result?.data) {
                this.dotNetRef.invokeMethodAsync('OnQrScanned', result.data);
            }
        } catch (error) {
            console.error('CollectionScanner result handling error:', error);
        }
    }

    async switchCamera() {
        if (!this.qrScanner || this.cameras.length <= 1) {
            console.log('Camera switch not possible:', this.cameras.length);
            return false;
        }
        try {
            const wasScanning = this.isScanning;
            if (wasScanning) {
                this.qrScanner.stop();
                this.isScanning = false;
            }

            this.currentCameraIndex = (this.currentCameraIndex + 1) % this.cameras.length;
            const nextCamera = this.cameras[this.currentCameraIndex];
            console.log(`Switching to camera: ${nextCamera.label}`);
            await this.qrScanner.setCamera(nextCamera.id);

            if (wasScanning) {
                await this.qrScanner.start();
                this.isScanning = true;
            }
            return true;
        } catch (error) {
            console.error('CollectionScanner camera switch failed:', error);
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
            console.error('CollectionScanner destroy error:', error);
        }
    }
}

const _scannerInstances = new Map();

window.collectionScanner = {
    async startScanWithId(videoId, dotNetObject) {
        try {
            let scanner = _scannerInstances.get(videoId);

            if (!scanner) {
                scanner = new CollectionScannerManager(videoId);
                const initSuccess = await scanner.init(dotNetObject);
                if (!initSuccess) {
                    console.error(`Failed to initialize collection scanner for ${videoId}`);
                    return false;
                }
                _scannerInstances.set(videoId, scanner);
            } else {
                scanner.dotNetRef = dotNetObject;
            }

            return await scanner.start();
        } catch (error) {
            console.error(`CollectionScanner startScanWithId error for ${videoId}:`, error);
            return false;
        }
    },

    stopScanWithId(videoId) {
        try {
            _scannerInstances.get(videoId)?.stop();
        } catch (error) {
            console.error(`CollectionScanner stopScanWithId error for ${videoId}:`, error);
        }
    },

    async switchCameraWithId(videoId) {
        try {
            return await _scannerInstances.get(videoId)?.switchCamera() ?? false;
        } catch (error) {
            console.error(`CollectionScanner switchCameraWithId error for ${videoId}:`, error);
            return false;
        }
    },

    cleanup() {
        try {
            _scannerInstances.forEach((scanner, videoId) => {
                try {
                    scanner.destroy();
                } catch (error) {
                    console.error(`CollectionScanner cleanup error for ${videoId}:`, error);
                }
            });
            _scannerInstances.clear();
        } catch (error) {
            console.error('CollectionScanner global cleanup error:', error);
        }
    }
};

window.addEventListener('beforeunload', () => window.collectionScanner.cleanup());
