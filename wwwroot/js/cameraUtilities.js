// wwwroot/js/cameraUtilities.js
// Minimal camera utilities - only keep what's needed beyond QR scanner

(function() {
    'use strict';

    // Basic camera utilities for non-QR scanner use cases
    window.CameraUtilities = {

        // Basic camera stream (your QR scanner handles the advanced stuff)
        getBasicCameraStream: async function() {
            try {
                return await navigator.mediaDevices.getUserMedia({
                    video: {
                        facingMode: 'environment'
                    }
                });
            } catch (error) {
                console.error('Basic camera access failed:', error);
                throw error;
            }
        },

        // Camera permission check
        checkCameraPermission: async function() {
            try {
                const result = await navigator.permissions.query({ name: 'camera' });
                return result.state;
            } catch (error) {
                return 'unknown';
            }
        },

        // Clean up any media stream
        stopMediaStream: function(stream) {
            if (stream) {
                stream.getTracks().forEach(track => track.stop());
            }
        }
    };

    console.log('Minimal camera utilities loaded - QR scanner handles the heavy lifting');

})();
