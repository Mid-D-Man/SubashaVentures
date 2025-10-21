// Loading screen progress tracking
(function() {
    'use strict';
    
    const percentageText = document.getElementById('loading-percentage');
    let lastPercentage = 0;
    
    // Monitor Blazor's CSS variable for loading percentage
    const checkProgress = setInterval(() => {
        const percentage = getComputedStyle(document.documentElement)
            .getPropertyValue('--blazor-load-percentage')
            .trim()
            .replace('%', '');
        
        if (percentage) {
            const value = parseInt(percentage) || 0;
            
            // Only update if percentage changed
            if (value !== lastPercentage) {
                lastPercentage = value;
                percentageText.textContent = value + '%';
            }
            
            // Remove loading screen when complete
            if (value >= 100) {
                setTimeout(() => {
                    removeLoadingScreen();
                    clearInterval(checkProgress);
                }, 300);
            }
        }
    }, 100);
    
    // Function to remove loading screen with fade effect
    function removeLoadingScreen() {
        const loadingScreen = document.getElementById('loading-screen');
        if (loadingScreen) {
            loadingScreen.style.transition = 'opacity 0.5s ease';
            loadingScreen.style.opacity = '0';
            setTimeout(() => {
                loadingScreen.remove();
            }, 500);
        }
    }
    
    // Fallback: Remove loading screen after 30 seconds
    setTimeout(() => {
        removeLoadingScreen();
        clearInterval(checkProgress);
    }, 30000);
    
    // Handle visibility change (tab switching)
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible' && lastPercentage >= 100) {
            removeLoadingScreen();
            clearInterval(checkProgress);
        }
    });
})();
