// wwwroot/js/gpuPerformance.js
// GPU Performance Enhancement Module
window.enableGPUAcceleration = function(elementSelector) {
    try {
        let element;

        // Handle different selector types
        if (typeof elementSelector === 'string') {
            // ID selector
            if (elementSelector.startsWith('#')) {
                element = document.querySelector(elementSelector);
            }
            // Class or other selector
            else if (elementSelector.startsWith('.') || elementSelector.includes('[')) {
                element = document.querySelector(elementSelector);
            }
            // Default to ID lookup
            else {
                element = document.getElementById(elementSelector);
            }
        }
        // Direct element reference
        else if (elementSelector && elementSelector.nodeType === Node.ELEMENT_NODE) {
            element = elementSelector;
        }

        // Validate element exists and has style property
        if (!element) {
            console.warn('GPU Acceleration: Element not found:', elementSelector);
            return false;
        }

        if (!element.style) {
            console.warn('GPU Acceleration: Element has no style property:', elementSelector);
            return false;
        }

        // Apply GPU acceleration styles
        element.style.transform = element.style.transform || 'translateZ(0)';
        element.style.willChange = 'transform';
        element.style.backfaceVisibility = 'hidden';
        element.style.perspective = '1000px';

        return true;
    } catch (error) {
        console.error('GPU Acceleration failed:', error);
        return false;
    }
};

// Batch GPU acceleration for multiple elements
window.enableBatchGPUAcceleration = function(selectors) {
    if (!Array.isArray(selectors)) {
        return window.enableGPUAcceleration(selectors);
    }

    const results = selectors.map(selector => ({
        selector: selector,
        success: window.enableGPUAcceleration(selector)
    }));

    return results;
};

// Disable GPU acceleration
window.disableGPUAcceleration = function(elementSelector) {
    try {
        const element = typeof elementSelector === 'string'
            ? document.querySelector(elementSelector) || document.getElementById(elementSelector)
            : elementSelector;

        if (element && element.style) {
            element.style.transform = '';
            element.style.willChange = '';
            element.style.backfaceVisibility = '';
            element.style.perspective = '';
            return true;
        }
        return false;
    } catch (error) {
        console.error('GPU Acceleration disable failed:', error);
        return false;
    }
};

// Performance monitoring
window.getGPUAccelerationStatus = function(elementSelector) {
    try {
        const element = typeof elementSelector === 'string'
            ? document.querySelector(elementSelector) || document.getElementById(elementSelector)
            : elementSelector;

        if (!element || !element.style) {
            return { enabled: false, reason: 'Element not found or no style property' };
        }

        const hasTransform = element.style.transform && element.style.transform.includes('translateZ');
        const hasWillChange = element.style.willChange === 'transform';

        return {
            enabled: hasTransform && hasWillChange,
            transform: element.style.transform,
            willChange: element.style.willChange,
            backfaceVisibility: element.style.backfaceVisibility,
            perspective: element.style.perspective
        };
    } catch (error) {
        return { enabled: false, error: error.message };
    }
};
