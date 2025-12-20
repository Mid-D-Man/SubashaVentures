// wwwroot/js/supabaseOAuth.js - FIXED for Blazor WASM
(function() {
    'use strict';

    const SUPABASE_URL = 'https://wbwmovtewytjibxutssk.supabase.co';
    const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indid21vdnRld3l0amlieHV0c3NrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyODMzNDcsImV4cCI6MjA0OTg1OTM0N30.f3ZGDFYp-6h_GNMG7T1rCJI8v8Lv-BdwggNk9NiFpKg';

    // Initialize Supabase client
    const supabase = window.supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

    // Helper function to get base path
    function getBasePath() {
        const path = window.location.pathname;
        
        // For GitHub Pages subdirectory deployment
        if (path.includes('/SubashaVentures')) {
            return '/SubashaVentures/';
        }
        
        // For localhost or root deployment
        return '/';
    }

    // Helper function to get redirect URL
    function getRedirectUrl() {
        const origin = window.location.origin;
        const basePath = getBasePath();
        const redirectUrl = origin + basePath;
        
        console.log('🔗 OAuth Redirect URL:', redirectUrl);
        return redirectUrl;
    }

    // Create the supabaseOAuth namespace that C# expects
    window.supabaseOAuth = {
        getBasePath: getBasePath,
        getRedirectUrl: getRedirectUrl,
        supabaseClient: supabase
    };

    // Google OAuth Sign In
    window.supabaseOAuth.signInWithGoogle = async function() {
        try {
            console.log('🔵 Initiating Google OAuth sign in');
            
            const redirectUrl = getRedirectUrl();
            console.log('📍 Redirect URL:', redirectUrl);
            
            // Use signInWithOAuth from Supabase
            const { data, error } = await supabase.auth.signInWithOAuth({
                provider: 'google',
                options: {
                    redirectTo: redirectUrl,
                    skipBrowserRedirect: false
                }
            });
            
            if (error) {
                console.error('❌ Google OAuth error:', error);
                throw error;
            }
            
            console.log('✅ Google OAuth initiated successfully');
            console.log('OAuth URL:', data.url);
            
            // Return true to indicate success
            return true;
            
        } catch (error) {
            console.error('❌ Error during Google OAuth:', error);
            return false;
        }
    };

    // Get current session
    window.supabaseOAuth.getSession = async function() {
        try {
            const { data: { session }, error } = await supabase.auth.getSession();
            
            if (error) {
                console.error('❌ Error getting session:', error);
                return null;
            }
            
            return session;
        } catch (error) {
            console.error('❌ Error in getSession:', error);
            return null;
        }
    };

    // Sign out
    window.supabaseOAuth.signOut = async function() {
        try {
            console.log('🔴 Signing out...');
            const { error } = await supabase.auth.signOut();
            
            if (error) {
                console.error('❌ Error signing out:', error);
                throw error;
            }
            
            console.log('✅ Signed out successfully');
            return true;
        } catch (error) {
            console.error('❌ Error in signOut:', error);
            return false;
        }
    };

    // Check for OAuth callback on page load
    window.addEventListener('load', async function() {
        console.log('🔍 Checking for OAuth callback...');
        
        // Check if we have a hash fragment (OAuth callback)
        const hash = window.location.hash;
        if (hash && (hash.includes('access_token') || hash.includes('error'))) {
            console.log('✅ OAuth callback detected in URL');
            console.log('Hash fragment:', hash);
            
            try {
                // Supabase automatically handles the session
                const { data: { session }, error } = await supabase.auth.getSession();
                
                if (error) {
                    console.error('❌ Error getting session:', error);
                    return;
                }
                
                if (session) {
                    console.log('✅ Session established successfully');
                    console.log('User:', session.user.email);
                }
            } catch (error) {
                console.error('❌ Error processing OAuth callback:', error);
            }
        } else {
            console.log('ℹ️ No OAuth callback detected');
        }
    });

    // Initialize logging
    console.log('✅ Supabase OAuth module initialized');
    console.log('Base Path:', getBasePath());
    console.log('Redirect URL:', getRedirectUrl());

})();
