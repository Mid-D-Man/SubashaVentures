// loading.js - Fixed with proper Blazor lifecycle integration

(function() {
    'use strict';

    let loadingProgress = 0;
    let loadingInterval = null;
    let isLoading = true;
    let hasCompleted = false; // Prevent multiple completions

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

    // Simulate loading progress
    function simulateProgress() {
        if (loadingProgress < 85) { // Stop at 85% until Blazor completes
            if (loadingProgress < 30) {
                loadingProgress += Math.random() * 3 + 2;
            }
            else if (loadingProgress < 60) {
                loadingProgress += Math.random() * 2 + 1;
            }
            else {
                loadingProgress += Math.random() * 0.5 + 0.3;
            }

            loadingProgress = Math.min(loadingProgress, 85);
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
        loadingInterval = setInterval(simulateProgress, 100);
    }

    // Complete loading and hide screen
    function completeLoading() {
        if (!isLoading || hasCompleted) return;

        hasCompleted = true;
        isLoading = false;

        console.log('✅ Loading screen completing...');

        // Clear interval
        if (loadingInterval) {
            clearInterval(loadingInterval);
            loadingInterval = null;
        }

        // Jump to 100%
        loadingProgress = 100;
        updatePercentage(100);

        // Hide loading screen
        setTimeout(() => {
            const elements = getLoadingElements();
            if (elements.screen) {
                elements.screen.classList.add('hidden');

                // Remove from DOM after transition
                setTimeout(() => {
                    if (elements.screen && elements.screen.parentNode) {
                        elements.screen.parentNode.removeChild(elements.screen);
                        console.log('✅ Loading screen removed');
                    }
                }, 500);
            }
        }, 200);
    }

    // Expose methods to window for Blazor
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

    // Extended fallback timeout - give Blazor more time
    setTimeout(() => {
        if (isLoading && !hasCompleted) {
            console.warn('⚠️ Loading screen auto-completing after extended timeout');
            completeLoading();
        }
    }, 15000); // 15 seconds instead of 10

})();
