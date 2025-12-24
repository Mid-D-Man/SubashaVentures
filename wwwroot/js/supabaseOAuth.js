// wwwroot/js/supabaseOAuth.js - COMPLETE AUTH SOLUTION
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

    // ==================== EMAIL/PASSWORD AUTH ====================

    // Sign up with email and password
    window.supabaseOAuth.signUp = async function(email, password, userData) {
        try {
            console.log('📝 Signing up:', email);
            
            const { data, error } = await supabase.auth.signUp({
                email: email,
                password: password,
                options: {
                    data: userData,
                    emailRedirectTo: getRedirectUrl()
                }
            });
            
            if (error) {
                console.error('❌ Sign up error:', error);
                return {
                    success: false,
                    error: error.message,
                    errorCode: error.status
                };
            }
            
            console.log('✅ Sign up successful');
            return {
                success: true,
                data: data,
                requiresVerification: true
            };
            
        } catch (error) {
            console.error('❌ Exception during sign up:', error);
            return {
                success: false,
                error: error.message
            };
        }
    };

    // Sign in with email and password
    window.supabaseOAuth.signIn = async function(email, password) {
        try {
            console.log('🔑 Signing in:', email);
            
            const { data, error } = await supabase.auth.signInWithPassword({
                email: email,
                password: password
            });
            
            if (error) {
                console.error('❌ Sign in error:', error);
                return {
                    success: false,
                    error: error.message,
                    errorCode: error.status
                };
            }
            
            console.log('✅ Sign in successful');
            
            // Dispatch event for Blazor
            window.dispatchEvent(new CustomEvent('supabase-auth-success', {
                detail: { session: data.session }
            }));
            
            return {
                success: true,
                session: data.session,
                user: data.user
            };
            
        } catch (error) {
            console.error('❌ Exception during sign in:', error);
            return {
                success: false,
                error: error.message
            };
        }
    };

    // ==================== OAUTH ====================

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

    // ==================== SESSION MANAGEMENT ====================

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

    // Get current user
    window.supabaseOAuth.getUser = async function() {
        try {
            const { data: { user }, error } = await supabase.auth.getUser();
            
            if (error) {
                console.error('❌ Error getting user:', error);
                return null;
            }
            
            return user;
        } catch (error) {
            console.error('❌ Error in getUser:', error);
            return null;
        }
    };

    // Check if authenticated
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
                return false;
            }
            
            console.log('✅ Signed out successfully');
            
            // Dispatch event for Blazor
            window.dispatchEvent(new CustomEvent('supabase-auth-change', {
                detail: { event: 'SIGNED_OUT', session: null }
            }));
            
            return true;
        } catch (error) {
            console.error('❌ Error in signOut:', error);
            return false;
        }
    };

    // ==================== OAUTH CALLBACK HANDLING ====================

    window.addEventListener('load', async function() {
        console.log('🔍 Checking for OAuth callback...');
        
        const hash = window.location.hash;
        if (hash && hash.includes('access_token')) {
            console.log('✅ OAuth callback detected');
            
            try {
                // Wait for Supabase to process the callback
                await new Promise(resolve => setTimeout(resolve, 1500));
                
                const { data: { session }, error } = await supabase.auth.getSession();
                
                if (error) {
                    console.error('❌ Error getting session after OAuth:', error);
                    return;
                }
                
                if (session) {
                    console.log('✅ OAuth session established:', session.user.email);
                    
                    // Clean URL
                    const cleanUrl = window.location.pathname + window.location.search;
                    window.history.replaceState({}, document.title, cleanUrl);
                    
                    // Dispatch event for Blazor
                    window.dispatchEvent(new CustomEvent('supabase-auth-success', {
                        detail: { session: session }
                    }));
                } else {
                    console.warn('⚠️ No session found after OAuth callback');
                }
            } catch (error) {
                console.error('❌ Error processing OAuth callback:', error);
            }
        }
    });

    // ==================== AUTH STATE LISTENER ====================

    supabase.auth.onAuthStateChange((event, session) => {
        console.log('🔔 Auth state changed:', event);
        
        window.dispatchEvent(new CustomEvent('supabase-auth-change', {
            detail: { event: event, session: session }
        }));
    });

    console.log('✅ Supabase OAuth module initialized');
    console.log('Redirect URL:', getRedirectUrl());

})();
