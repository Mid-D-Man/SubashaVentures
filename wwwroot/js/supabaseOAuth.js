// Initialize Supabase client
const SUPABASE_URL = 'https://wbwmovtewytjibxutssk.supabase.co';
const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indid21vdnRld3l0amlieHV0c3NrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyODMzNDcsImV4cCI6MjA0OTg1OTM0N30.f3ZGDFYp-6h_GNMG7T1rCJI8v8Lv-BdwggNk9NiFpKg';

const supabase = window.supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

// Helper function to get base path
function getBasePath() {
    const path = window.location.pathname;
    
    // For GitHub Pages subdirectory deployment
    if (path.includes('/SubashaVentures')) {
        const repoMatch = path.match(/^\/([^\/]+)/);
        return repoMatch ? `/${repoMatch[1]}/` : '/';
    }
    
    // For localhost or root deployment
    return '/';
}

// Helper function to get redirect URL
function getRedirectUrl() {
    const origin = window.location.origin;
    const basePath = getBasePath();
    const redirectUrl = origin + basePath;
    
    console.log('OAuth Redirect URL:', redirectUrl);
    return redirectUrl;
}

// Initialize on page load
console.log('Supabase OAuth initialized');
console.log('Base Path:', getBasePath());
console.log('Redirect URL:', getRedirectUrl());

// Google OAuth Sign In
window.supabaseGoogleSignIn = async function() {
    try {
        console.log('Initiating Google OAuth sign in');
        
        const redirectUrl = getRedirectUrl();
        console.log('Redirect URL:', redirectUrl);
        
        // Use signInWithOAuth with proper configuration
        const { data, error } = await supabase.auth.signInWithOAuth({
            provider: 'google',
            options: {
                redirectTo: redirectUrl,
                // Skip nonce check for now (can add later for extra security)
                skipBrowserRedirect: false
            }
        });
        
        if (error) {
            console.error('❌ Google OAuth error:', error);
            throw error;
        }
        
        console.log('✓ Google OAuth initiated successfully');
        console.log('OAuth data:', data);
        
        // The redirect will happen automatically
        // No need to return anything as the page will redirect
        
    } catch (error) {
        console.error('❌ Error during Google OAuth:', error);
        throw error;
    }
};

// Google OAuth Sign Up (same as sign in for OAuth)
window.supabaseGoogleSignUp = async function() {
    console.log('Initiating Google OAuth sign up (Blazor WASM)');
    return await window.supabaseGoogleSignIn();
};

// Check for OAuth callback on page load
window.addEventListener('load', async function() {
    console.log('Checking for OAuth callback...');
    
    // Check if we have a hash fragment (OAuth callback)
    const hash = window.location.hash;
    if (hash && (hash.includes('access_token') || hash.includes('error'))) {
        console.log('✓ OAuth callback detected in URL');
        console.log('Hash fragment:', hash);
        
        try {
            // Supabase automatically handles the session
            const { data: { session }, error } = await supabase.auth.getSession();
            
            if (error) {
                console.error('❌ Error getting session:', error);
                return;
            }
            
            if (session) {
                console.log('✓ Session established successfully');
                console.log('User:', session.user.email);
                
                // Clean up the URL by removing the hash
                const cleanUrl = window.location.origin + window.location.pathname;
                window.history.replaceState({}, document.title, cleanUrl);
                
                // Notify Blazor that auth is complete
                if (window.DotNet) {
                    console.log('Notifying Blazor of successful authentication');
                }
            }
        } catch (error) {
            console.error('❌ Error processing OAuth callback:', error);
        }
    } else {
        console.log('No OAuth callback detected');
    }
});

// Get current session
window.supabaseGetSession = async function() {
    try {
        const { data: { session }, error } = await supabase.auth.getSession();
        
        if (error) {
            console.error('Error getting session:', error);
            return null;
        }
        
        return session;
    } catch (error) {
        console.error('Error in supabaseGetSession:', error);
        return null;
    }
};

// Sign out
window.supabaseSignOut = async function() {
    try {
        console.log('Signing out...');
        const { error } = await supabase.auth.signOut();
        
        if (error) {
            console.error('Error signing out:', error);
            throw error;
        }
        
        console.log('✓ Signed out successfully');
    } catch (error) {
        console.error('Error in supabaseSignOut:', error);
        throw error;
    }
};

// Export for debugging
window.supabaseDebug = {
    getBasePath,
    getRedirectUrl,
    client: supabase
};
