// Enhanced theme switcher for SubashaVentures
console.log("Enhanced themeSwitcher.js loaded");

window.themeSwitcher = {
    toggleTheme: function () {
        const body = document.querySelector('body');
        const isDarkMode = body.classList.contains('dark-mode');

        // Add transition class for smooth theme change
        body.classList.add('theme-transitioning');

        if (isDarkMode) {
            body.classList.remove('dark-mode');
            localStorage.setItem('theme', 'light');
            this.showThemeNotification('☀️', 'Light Mode');
        } else {
            body.classList.add('dark-mode');
            localStorage.setItem('theme', 'dark');
            this.showThemeNotification('🌙', 'Dark Mode');
        }

        // Remove transition class after animation
        setTimeout(() => {
            body.classList.remove('theme-transitioning');
        }, 300);

        // Trigger custom event for other components
        window.dispatchEvent(new CustomEvent('themeChanged', {
            detail: { theme: isDarkMode ? 'light' : 'dark' }
        }));
    },

    initTheme: function () {
        const savedTheme = localStorage.getItem('theme');
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        const body = document.querySelector('body');

        if (savedTheme === 'dark' || (!savedTheme && prefersDark)) {
            body.classList.add('dark-mode');
            localStorage.setItem('theme', 'dark');
        } else {
            body.classList.remove('dark-mode');
            localStorage.setItem('theme', 'light');
        }
    },

    showThemeNotification: function (icon, text) {
        // Remove existing notification if any
        const existing = document.querySelector('.theme-notification');
        if (existing) {
            existing.remove();
        }

        // Create notification element
        const notification = document.createElement('div');
        notification.className = 'theme-notification';
        notification.innerHTML = `
            <span class="theme-notification-icon">${icon}</span>
            <span class="theme-notification-text">${text}</span>
        `;

        // Add to DOM
        document.body.appendChild(notification);

        // Trigger show animation
        requestAnimationFrame(() => {
            notification.classList.add('show');
        });

        // Auto-hide after 2 seconds
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.remove();
                }
            }, 300);
        }, 2000);
    },

    // Listen for system theme changes
    watchSystemTheme: function () {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        mediaQuery.addListener((e) => {
            const savedTheme = localStorage.getItem('theme');
            if (!savedTheme) {
                // Only auto-switch if user hasn't manually set a preference
                const body = document.querySelector('body');
                if (e.matches) {
                    body.classList.add('dark-mode');
                } else {
                    body.classList.remove('dark-mode');
                }
            }
        });
    }
};

// Initialize theme on DOM ready
document.addEventListener('DOMContentLoaded', () => {
    window.themeSwitcher.initTheme();
    window.themeSwitcher.watchSystemTheme();
});

// Add CSS for theme transitions and notifications
const themeStyles = document.createElement('style');
themeStyles.textContent = `
    .theme-transitioning * {
        transition: background-color 0.3s ease, color 0.3s ease, border-color 0.3s ease !important;
    }

    .theme-notification {
        position: fixed;
        top: 20px;
        right: 20px;
        background: var(--bg-surface, rgba(255, 255, 255, 0.95));
        backdrop-filter: blur(10px);
        -webkit-backdrop-filter: blur(10px);
        border: 1px solid var(--border-light, rgba(150, 153, 74, 0.2));
        border-radius: 12px;
        padding: 1rem 1.5rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
        z-index: 10000;
        transform: translateX(100%);
        opacity: 0;
        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
        font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
        font-size: 0.9rem;
        font-weight: 500;
        color: var(--text-primary, #333);
    }

    .theme-notification.show {
        transform: translateX(0);
        opacity: 1;
    }

    .theme-notification-icon {
        font-size: 1.2rem;
        filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.1));
    }

    .theme-notification-text {
        white-space: nowrap;
    }

    .dark-mode .theme-notification {
        background: var(--bg-surface, rgba(33, 37, 41, 0.95));
        border-color: rgba(184, 187, 106, 0.2);
        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
        color: var(--text-primary, #f8f9fa);
    }

    @media (max-width: 768px) {
        .theme-notification {
            top: 15px;
            right: 15px;
            left: 15px;
            transform: translateY(-100%);
        }

        .theme-notification.show {
            transform: translateY(0);
        }
    }

    @media (prefers-reduced-motion: reduce) {
        .theme-transitioning *,
        .theme-notification {
            transition: none !important;
        }
    }
`;
document.head.appendChild(themeStyles);