// wwwroot/js/pwaManager.js
// SubashaVentures PWA Install Manager
// Only Chromium-based browsers fire beforeinstallprompt.
// We capture it, hide it, and show our own button.

(function () {
    'use strict';

    let _deferredPrompt = null;       // The captured beforeinstallprompt event
    let _blazorRef = null;            // DotNetObjectReference for Blazor callbacks
    let _isInitialized = false;

    // ── Helpers ────────────────────────────────────────────────────────────

    function isInstalled() {
        return (
            window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true ||
            document.referrer.startsWith('android-app://')
        );
    }

    function canInstall() {
        return _deferredPrompt !== null && !isInstalled();
    }

    // ── Blazor notifications ───────────────────────────────────────────────

    function notifyBlazor(method, arg) {
        if (!_blazorRef) return;
        try {
            if (arg !== undefined) {
                _blazorRef.invokeMethodAsync(method, arg);
            } else {
                _blazorRef.invokeMethodAsync(method);
            }
        } catch (err) {
            console.warn(`[PWA] Failed to notify Blazor (${method}):`, err);
            if (err.message && err.message.includes('disposed')) {
                _blazorRef = null;
            }
        }
    }

    // ── Core install logic ─────────────────────────────────────────────────

    window.addEventListener('beforeinstallprompt', (e) => {
        // Prevent the mini-infobar from appearing automatically
        e.preventDefault();
        _deferredPrompt = e;
        console.log('[PWA] Install prompt captured');

        // Tell Blazor the install button should be shown
        notifyBlazor('OnInstallAvailable', true);

        // Also fire a custom event in case any other code is listening
        window.dispatchEvent(new CustomEvent('pwa-installable', { detail: true }));
    });

    window.addEventListener('appinstalled', () => {
        _deferredPrompt = null;
        console.log('[PWA] App installed');
        notifyBlazor('OnAppInstalled');
        window.dispatchEvent(new CustomEvent('pwa-installed'));
    });

    // ── Public API (window.SubashaPWA) ─────────────────────────────────────

    window.SubashaPWA = {

        /** Called by Blazor to register its DotNetObjectReference */
        register(dotNetRef) {
            _blazorRef = dotNetRef;
            console.log('[PWA] Blazor reference registered');

            // If the prompt was already captured before Blazor mounted, notify now
            if (canInstall()) {
                notifyBlazor('OnInstallAvailable', true);
            }
        },

        /** Called by Blazor when install button is clicked */
        async promptInstall() {
            if (!_deferredPrompt) {
                console.log('[PWA] No install prompt available');
                return false;
            }
            try {
                await _deferredPrompt.prompt();
                const choice = await _deferredPrompt.userChoice;
                console.log(`[PWA] User choice: ${choice.outcome}`);
                _deferredPrompt = null;
                return choice.outcome === 'accepted';
            } catch (err) {
                console.error('[PWA] Install prompt failed:', err);
                return false;
            }
        },

        isInstalled,
        canInstall,

        /** Unregister Blazor reference on component dispose */
        unregister() {
            _blazorRef = null;
        }
    };

    // ── Service Worker registration ────────────────────────────────────────
    // Note: In development Blazor uses service-worker.js (passthrough).
    // In production it uses service-worker.published.js via the build pipeline.
    // We let the default Blazor registration in index.html handle this.
    // This file only deals with the install prompt logic.

    console.log(`[PWA] Manager loaded. Already installed: ${isInstalled()}`);

})();
