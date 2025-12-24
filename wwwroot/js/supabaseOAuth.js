// wwwroot/js/supabaseOAuth.js - SIMPLIFIED
(function() {
    'use strict';

    const SUPABASE_URL = 'https://wbwmovtewytjibxutssk.supabase.co';
    const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indid21vdnRld3l0amlieHV0c3NrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyODMzNDcsImV4cCI6MjA0OTg1OTM0N30.f3ZGDFYp-6h_GNMG7T1rCJI8v8Lv-BdwggNk9NiFpKg';

    // Initialize Supabase client
    const supabase = window.supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
        auth: {
            autoRefreshToken: true,
            persistSession: true,
            detectSessionInUrl: true,
            flowType: 'implicit'
        }
    });

    function getBasePath() {
        const path = window.location.pathname;
        if (path.includes('/SubashaVentures')) {
            return '/SubashaVentures/';
        }
        return '/';
    }

    function getRedirectUrl() {
        const origin = window.location.origin;
        const basePath = getBasePath();
        return origin + basePath;
    }

    // Create the namespace
    window.supabaseOAuth = {
        getBasePath: getBasePath,
        getRedirectUrl: getRedirectUrl,
        supabaseClient: supabase
    };

    // Google OAuth Sign In
    window.supabaseOAuth.signInWithGoogle = async function() {
        try {
            console.log('🔵 Initiating Google OAuth');
            
            const { data, error } = await supabase.auth.signInWithOAuth({
                provider: 'google',
                options: {
                    redirectTo: getRedirectUrl(),
                    queryParams: {
                        access_type: 'offline',
                        prompt: 'consent'
                    }
                }
            });
            
            if (error) {
                console.error('❌ Google OAuth error:', error);
                return false;
            }
            
            console.log('✅ Google OAuth initiated');
            return true;
            
        } catch (error) {
            console.error('❌ Error during Google OAuth:', error);
            return false;
        }
    };

    // Sign out
    window.supabaseOAuth.signOut = async function() {
        try {
            console.log('🔴 Signing out...');
            const { error } = await supabase.auth.signOut();
            
            if (error) {
                console.error('❌ Error signing out:', error);
                return false;
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
        
        const hash = window.location.hash;
        if (hash && (hash.includes('access_token') || hash.includes('error'))) {
            console.log('✅ OAuth callback detected');
            
            try {
                // Wait for Supabase to process the callback
                await new Promise(resolve => setTimeout(resolve, 1000));
                
                const { data: { session }, error } = await supabase.auth.getSession();
                
                if (error) {
                    console.error('❌ Error getting session:', error);
                    return;
                }
                
                if (session) {
                    console.log('✅ Session established:', session.user.email);
                    
                    // Clean URL
                    const cleanUrl = window.location.pathname + window.location.search;
                    window.history.replaceState({}, document.title, cleanUrl);
                    
                    // Dispatch event for Blazor
                    window.dispatchEvent(new CustomEvent('supabase-auth-success', {
                        detail: { session: session }
                    }));
                } else {
                    console.warn('⚠️ No session found after callback');
                }
            } catch (error) {
                console.error('❌ Error processing OAuth callback:', error);
            }
        }
    });

    // Listen for auth state changes
    supabase.auth.onAuthStateChange((event, session) => {
        console.log('🔔 Auth state changed:', event);
        
        window.dispatchEvent(new CustomEvent('supabase-auth-change', {
            detail: { event: event, session: session }
        }));
    });

    console.log('✅ Supabase OAuth initialized');
    console.log('Redirect URL:', getRedirectUrl());

})();
