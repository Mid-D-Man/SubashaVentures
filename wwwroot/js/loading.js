// loading.js - Optimized loading screen controller with smooth transitions

(function() {
    'use strict';

    let loadingProgress = 0;
    let loadingInterval = null;
    let isLoading = true;

    // Get loading elements
    function getLoadingElements() {
        return {
            screen: document.getElementById('loading-screen'),
            percentage: document.getElementById('loading-percentage')
        };
    }

    // Update percentage display
    function updatePercentage(value) {
        const elements = getLoadingElements();
        if (elements.percentage) {
            elements.percentage.textContent = Math.round(value) + '%';
        }
    }

    // Simulate loading progress (smoother progression)
    function simulateProgress() {
        if (loadingProgress < 90) {
            // Fast initial progress
            if (loadingProgress < 30) {
                loadingProgress += Math.random() * 3 + 2;
            }
            // Medium progress
            else if (loadingProgress < 60) {
                loadingProgress += Math.random() * 2 + 1;
            }
            // Slow down near the end
            else {
                loadingProgress += Math.random() * 1 + 0.5;
            }

            loadingProgress = Math.min(loadingProgress, 90);
            updatePercentage(loadingProgress);
        }
    }

    // Start progress simulation
    function startProgress() {
        if (loadingInterval) {
            clearInterval(loadingInterval);
        }

        loadingProgress = 0;
        updatePercentage(0);

        // Update every 100ms for smooth progress
        loadingInterval = setInterval(simulateProgress, 100);
    }

    // Complete loading and hide screen
    function completeLoading() {
        if (!isLoading) return;

        isLoading = false;

        // Clear interval
        if (loadingInterval) {
            clearInterval(loadingInterval);
            loadingInterval = null;
        }

        // Jump to 100%
        loadingProgress = 100;
        updatePercentage(100);

        // Hide loading screen after brief delay
        setTimeout(() => {
            const elements = getLoadingElements();
            if (elements.screen) {
                elements.screen.classList.add('hidden');

                // Remove from DOM after transition completes
                setTimeout(() => {
                    if (elements.screen && elements.screen.parentNode) {
                        elements.screen.parentNode.removeChild(elements.screen);
                    }
                }, 500); // Match CSS transition duration
            }
        }, 300); // Show 100% briefly
    }

    // Expose methods to window for Blazor to call
    window.loadingScreen = {
        start: startProgress,
        complete: completeLoading,
        updateProgress: function(value) {
            loadingProgress = Math.max(0, Math.min(100, value));
            updatePercentage(loadingProgress);
        }
    };

    // Auto-start on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startProgress);
    } else {
        startProgress();
    }

    // Fallback: Auto-complete after 10 seconds if Blazor doesn't call it
    setTimeout(() => {
        if (isLoading) {
            console.warn('Loading screen auto-completing after timeout');
            completeLoading();
        }
    }, 10000);

})();