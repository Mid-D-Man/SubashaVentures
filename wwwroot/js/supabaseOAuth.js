// wwwroot/js/supabaseOAuth.js
// Supabase OAuth helper for Blazor WASM on GitHub Pages

window.supabaseOAuth = {
    // Get current window origin (protocol + hostname + port)
    getCurrentOrigin: function() {
        return window.location.origin;
    },
    
    // Get full current URL
    getCurrentUrl: function() {
        return window.location.href;
    },
    
    // Redirect to OAuth URL
    redirectTo: function(url) {
        window.location.href = url;
    },
    
    // Get redirect URL for OAuth callback
    // FIXED: Now includes /SubashaVentures/ base path for GitHub Pages
    getRedirectUrl: function() {
        const origin = window.location.origin;
        const basePath = '/SubashaVentures';
        
        // For localhost development, no base path
        if (origin.includes('localhost')) {
            return origin + '/';
        }
        
        // For GitHub Pages deployment
        return origin + basePath + '/';
    },
    
    // Get base path from current location
    getBasePath: function() {
        // Check if we're on GitHub Pages
        const path = window.location.pathname;
        if (path.startsWith('/SubashaVentures')) {
            return '/SubashaVentures';
        }
        return '';
    }
};

// Log the redirect URL for debugging
console.log('OAuth Redirect URL:', window.supabaseOAuth.getRedirectUrl());
