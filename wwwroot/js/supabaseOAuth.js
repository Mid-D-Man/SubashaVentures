// wwwroot/js/supabaseOAuth.js - UPDATED WITH RETURN URL SUPPORT
(function() {
    'use strict';

    const SUPABASE_URL = 'https://wbwmovtewytjibxutssk.supabase.co';
    const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Indid21vdnRld3l0amlieHV0c3NrIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyODMzNDcsImV4cCI6MjA0OTg1OTM0N30.f3ZGDFYp-6h_GNMG7T1rCJI8v8Lv-BdwggNk9NiFpKg';

    // ✅ CONFIRMED: Supabase uses localStorage to persist sessions automatically
    // Initialize Supabase client with auto session restoration
    const supabase = window.supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
        auth: {
            autoRefreshToken: true,
            persistSession: true,
            detectSessionInUrl: true, // ✅ Critical for OAuth callback
            flowType: 'implicit' // ✅ For client-side OAuth (Blazor WASM)
        }
    });

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

    // ✅ NEW: Store return URL before OAuth redirect
    function storeReturnUrl(returnUrl) {
        if (returnUrl) {
            try {
                localStorage.setItem('oauth_return_url', returnUrl);
                console.log('💾 Stored return URL:', returnUrl);
            } catch (error) {
                console.error('❌ Failed to store return URL:', error);
            }
        }
    }

    // ✅ NEW: Retrieve and clear return URL after OAuth
    function getAndClearReturnUrl() {
        try {
            const returnUrl = localStorage.getItem('oauth_return_url');
            if (returnUrl) {
                localStorage.removeItem('oauth_return_url');
                console.log('📤 Retrieved return URL:', returnUrl);
                return returnUrl;
            }
        } catch (error) {
            console.error('❌ Failed to retrieve return URL:', error);
        }
        return null;
    }

    // Create the supabaseOAuth namespace that C# expects
    window.supabaseOAuth = {
        getBasePath: getBasePath,
        getRedirectUrl: getRedirectUrl,
        supabaseClient: supabase,
        storeReturnUrl: storeReturnUrl,
        getAndClearReturnUrl: getAndClearReturnUrl
    };

    // ✅ UPDATED: Google OAuth Sign In with return URL support
    window.supabaseOAuth.signInWithGoogle = async function(returnUrl) {
        try {
            console.log('🔵 Initiating Google OAuth sign in');
            
            // Store return URL before redirect
            if (returnUrl) {
                storeReturnUrl(returnUrl);
            }
            
            const redirectUrl = getRedirectUrl();
            console.log('📍 Redirect URL:', redirectUrl);
            
            // ✅ CONFIRMED: Use signInWithOAuth from Supabase JS SDK
            const { data, error } = await supabase.auth.signInWithOAuth({
                provider: 'google',
                options: {
                    redirectTo: redirectUrl,
                    skipBrowserRedirect: false,
                    // ✅ NEW: Add query params for better session handling
                    queryParams: {
                        access_type: 'offline',
                        prompt: 'consent'
                    }
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

    // ✅ NEW: Get current session (useful for checking auth state)
    window.supabaseOAuth.getSession = async function() {
        try {
            const { data: { session }, error } = await supabase.auth.getSession();
            
            if (error) {
                console.error('❌ Error getting session:', error);
                return null;
            }
            
            if (session) {
                console.log('✅ Session found:', session.user.email);
            }
            
            return session;
        } catch (error) {
            console.error('❌ Error in getSession:', error);
            return null;
        }
    };

    // ✅ NEW: Check if user is authenticated
    window.supabaseOAuth.isAuthenticated = async function() {
        try {
            const session = await window.supabaseOAuth.getSession();
            return session !== null;
        } catch (error) {
            console.error('❌ Error checking authentication:', error);
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
                throw error;
            }
            
            // Clear any stored return URLs
            try {
                localStorage.removeItem('oauth_return_url');
            } catch (e) {
                console.warn('⚠️ Failed to clear return URL:', e);
            }
            
            console.log('✅ Signed out successfully');
            return true;
        } catch (error) {
            console.error('❌ Error in signOut:', error);
            return false;
        }
    };

    // ✅ CRITICAL: Check for OAuth callback on page load
    window.addEventListener('load', async function() {
        console.log('🔍 Checking for OAuth callback...');
        
        // ✅ CONFIRMED: Supabase returns tokens in hash fragment (#)
        const hash = window.location.hash;
        if (hash && (hash.includes('access_token') || hash.includes('error'))) {
            console.log('✅ OAuth callback detected in URL hash');
            console.log('Hash fragment:', hash);
            
            try {
                // ✅ CRITICAL: Give Supabase SDK time to process the callback
                // The SDK automatically handles the hash and stores the session
                await new Promise(resolve => setTimeout(resolve, 1000));
                
                // Check if session was established
                const { data: { session }, error } = await supabase.auth.getSession();
                
                if (error) {
                    console.error('❌ Error getting session after callback:', error);
                    return;
                }
                
                if (session) {
                    console.log('✅ Session established successfully');
                    console.log('User:', session.user.email);
                    
                    // Retrieve return URL if stored
                    const returnUrl = getAndClearReturnUrl();
                    
                    // ✅ IMPORTANT: Clean URL (remove hash) and redirect
                    // This prevents the hash from interfering with Blazor routing
                    const cleanUrl = window.location.pathname + window.location.search;
                    
                    // Store destination for Blazor to use
                    if (returnUrl) {
                        sessionStorage.setItem('post_oauth_redirect', returnUrl);
                        console.log('🎯 Will redirect to:', returnUrl);
                    } else {
                        sessionStorage.setItem('post_oauth_redirect', '/');
                        console.log('🎯 Will redirect to: home');
                    }
                    
                    // Replace URL without hash (this triggers Blazor to re-render)
                    window.history.replaceState({}, document.title, cleanUrl);
                    
                    // Dispatch custom event for Blazor to catch
                    window.dispatchEvent(new CustomEvent('supabase-auth-success', {
                        detail: { session: session }
                    }));
                } else {
                    console.warn('⚠️ OAuth callback processed but no session found');
                }
            } catch (error) {
                console.error('❌ Error processing OAuth callback:', error);
            }
        } else {
            console.log('ℹ️ No OAuth callback detected');
            
            // Check for existing session on normal page load
            try {
                const session = await window.supabaseOAuth.getSession();
                if (session) {
                    console.log('✅ Existing session found:', session.user.email);
                }
            } catch (error) {
                console.error('❌ Error checking existing session:', error);
            }
        }
    });

    // ✅ NEW: Listen for auth state changes
    supabase.auth.onAuthStateChange((event, session) => {
        console.log('🔔 Auth state changed:', event);
        
        if (event === 'SIGNED_IN') {
            console.log('✅ User signed in:', session?.user?.email);
        } else if (event === 'SIGNED_OUT') {
            console.log('🔴 User signed out');
        } else if (event === 'TOKEN_REFRESHED') {
            console.log('🔄 Token refreshed');
        }
        
        // Dispatch event for Blazor to listen to
        window.dispatchEvent(new CustomEvent('supabase-auth-change', {
            detail: { event: event, session: session }
        }));
    });

    // Initialize logging
    console.log('✅ Supabase OAuth module initialized');
    console.log('Base Path:', getBasePath());
    console.log('Redirect URL:', getRedirectUrl());
    console.log('Supabase URL:', SUPABASE_URL);

})();
