// Admin Navigation Menu JavaScript Module
let navElement = null;
let dotNetRef = null;
let proximityThreshold = 100; // pixels
let isDesktop = window.innerWidth > 768;
let rafId = null;

export function initializeNavMenu(element, dotNetReference) {
    navElement = element;
    dotNetRef = dotNetReference;

    // Check if desktop
    updateDeviceType();

    // Add resize listener
    window.addEventListener('resize', handleResize);

    // Add proximity detection for desktop
    if (isDesktop) {
        document.addEventListener('mousemove', handleMouseMove);
    }

    // Setup mobile toggle
    setupMobileToggle();

    console.log('AdminNavMenu JavaScript initialized');
}

function updateDeviceType() {
    isDesktop = window.innerWidth > 768;
}

function handleResize() {
    const wasDesktop = isDesktop;
    updateDeviceType();

    // Clean up/setup event listeners based on device type change
    if (wasDesktop !== isDesktop) {
        if (isDesktop) {
            document.addEventListener('mousemove', handleMouseMove);
        } else {
            document.removeEventListener('mousemove', handleMouseMove);
        }
    }
}

function handleMouseMove(event) {
    // Cancel any pending animation frame
    if (rafId) {
        cancelAnimationFrame(rafId);
    }

    // Use requestAnimationFrame for better performance
    rafId = requestAnimationFrame(() => {
        if (!navElement || !isDesktop) return;

        const rect = navElement.getBoundingClientRect();
        const mouseX = event.clientX;
        const mouseY = event.clientY;

        // Calculate distance from nav menu
        let distance = Infinity;

        // Check horizontal distance from left edge
        if (mouseX < rect.left) {
            distance = rect.left - mouseX;
        } else if (mouseX > rect.right) {
            distance = mouseX - rect.right;
        } else {
            distance = 0; // Mouse is within horizontal bounds
        }

        // Check if mouse is within vertical bounds
        const isWithinVerticalBounds = mouseY >= rect.top && mouseY <= rect.bottom;

        // Trigger expansion if within proximity and vertical bounds
        if (distance <= proximityThreshold && isWithinVerticalBounds) {
            if (!navElement.classList.contains('expanded') &&
                !navElement.classList.contains('mobile-open')) {
                // Mouse entered proximity zone
                triggerMouseEnter();
            }
        } else {
            if (navElement.classList.contains('expanded') &&
                !navElement.classList.contains('mobile-open')) {
                // Mouse left proximity zone
                triggerMouseLeave();
            }
        }
    });
}

function triggerMouseEnter() {
    // Trigger mouseenter event on nav element
    const event = new MouseEvent('mouseenter', {
        bubbles: true,
        cancelable: true,
        view: window
    });
    navElement.dispatchEvent(event);
}

function triggerMouseLeave() {
    // Trigger mouseleave event on nav element
    const event = new MouseEvent('mouseleave', {
        bubbles: true,
        cancelable: true,
        view: window
    });
    navElement.dispatchEvent(event);
}

function setupMobileToggle() {
    // Setup mobile menu toggle button (burger menu)
    const toggleBtn = document.querySelector('.mobile-menu-toggle');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', () => {
            if (dotNetRef && !isDesktop) {
                dotNetRef.invokeMethodAsync('OpenMobileNav');
            }
        });
    }

    // Setup keyboard navigation
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && navElement?.classList.contains('mobile-open')) {
            // Close mobile nav on escape
            const closeBtn = navElement.querySelector('.nav-close-btn');
            if (closeBtn) {
                closeBtn.click();
            }
        }
    });
}

export function dispose() {
    // Clean up event listeners
    window.removeEventListener('resize', handleResize);
    document.removeEventListener('mousemove', handleMouseMove);

    if (rafId) {
        cancelAnimationFrame(rafId);
    }

    navElement = null;
    dotNetRef = null;

    console.log('AdminNavMenu JavaScript disposed');
}