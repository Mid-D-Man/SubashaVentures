// wwwroot/js/firebaseSetup.js
console.log("Firebase setup script loaded");

// Firebase initialization function that accepts config from C#
window.initializeFirebaseFromConfig = function(config) {
    try {
        console.log("Initializing Firebase with config from appsettings...");
        
        // Validate config
        if (!config || !config.apiKey || !config.projectId) {
            throw new Error("Invalid Firebase configuration");
        }

        // Check if Firebase is already initialized
        if (typeof firebase !== 'undefined' && firebase.apps && firebase.apps.length > 0) {
            console.log("Firebase already initialized");
            return true;
        }

        // Wait for Firebase SDK to load
        const waitForFirebase = new Promise((resolve, reject) => {
            let attempts = 0;
            const maxAttempts = 50;
            
            const checkFirebase = setInterval(() => {
                attempts++;
                
                if (typeof firebase !== 'undefined') {
                    clearInterval(checkFirebase);
                    resolve();
                } else if (attempts >= maxAttempts) {
                    clearInterval(checkFirebase);
                    reject(new Error('Firebase SDK failed to load'));
                }
            }, 100);
        });

        return waitForFirebase.then(() => {
            // Initialize Firebase
            const app = firebase.initializeApp(config);
            
            // Initialize Firestore
            const db = firebase.firestore();
            db.settings({
                ignoreUndefinedProperties: true,
                timestampsInSnapshots: true,
                cacheSizeBytes: firebase.firestore.CACHE_SIZE_UNLIMITED
            });

            // Enable persistence for offline support
            db.enablePersistence({ synchronizeTabs: true })
                .then(() => {
                    console.log("Firestore persistence enabled");
                })
                .catch((err) => {
                    if (err.code === 'failed-precondition') {
                        console.warn("Multiple tabs open, persistence enabled in first tab only");
                    } else if (err.code === 'unimplemented') {
                        console.warn("Browser doesn't support persistence");
                    } else {
                        console.error("Persistence error:", err);
                    }
                });

            // Initialize Analytics if measurementId provided
            if (config.measurementId) {
                try {
                    firebase.analytics();
                    console.log("Firebase Analytics initialized");
                } catch (e) {
                    console.warn("Analytics initialization failed:", e);
                }
            }

            console.log("Firebase initialized successfully");
            return true;
        });

    } catch (error) {
        console.error("Error initializing Firebase:", error);
        throw error;
    }
};

// Check if Firebase is initialized
window.isFirebaseInitialized = function() {
    return typeof firebase !== 'undefined' && 
           firebase.apps && 
           firebase.apps.length > 0;
};

// Get Firebase app instance
window.getFirebaseApp = function() {
    if (window.isFirebaseInitialized()) {
        return firebase.app();
    }
    return null;
};

// Get Firestore instance
window.getFirestore = function() {
    if (window.isFirebaseInitialized()) {
        return firebase.firestore();
    }
    return null;
};
