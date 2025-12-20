// wwwroot/js/supabaseOAuth.js
// Supabase OAuth helper for Blazor WASM with GitHub Pages support

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
    // Automatically detects localhost vs GitHub Pages
    getRedirectUrl: function() {
        const origin = window.location.origin;
        const pathname = window.location.pathname;
        
        // Extract base path from current location
        // For GitHub Pages: /SubashaVentures/
        // For localhost: /
        let basePath = '/';
        
        // Check if we're NOT on localhost
        if (!origin.includes('localhost') && !origin.includes('127.0.0.1')) {
            // Extract the first path segment (repository name)
            const match = pathname.match(/^\/([^\/]+)\//);
            if (match && match[1]) {
                basePath = '/' + match[1] + '/';
            }
        }
        
        const redirectUrl = origin + basePath;
        console.log('OAuth Redirect URL:', redirectUrl);
        
        return redirectUrl;
    },
    
    // Get base path from current location
    getBasePath: function() {
        const pathname = window.location.pathname;
        
        // Check if we're on localhost
        if (window.location.origin.includes('localhost') || 
            window.location.origin.includes('127.0.0.1')) {
            return '/';
        }
        
        // Extract first path segment for GitHub Pages
        const match = pathname.match(/^\/([^\/]+)\//);
        if (match && match[1]) {
            return '/' + match[1];
        }
        
        return '';
    }
};

// Log configuration on load
console.log('Supabase OAuth initialized');
console.log('Base Path:', window.supabaseOAuth.getBasePath());
console.log('Redirect URL:', window.supabaseOAuth.getRedirectUrl());
