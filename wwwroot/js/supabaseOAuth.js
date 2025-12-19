// wwwroot/js/supabaseOAuth.js
// Supabase OAuth helper for Blazor WASM

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
    getRedirectUrl: function() {
        const origin = window.location.origin;
        // Redirect back to root after OAuth
        return origin + '/';
    }
};
