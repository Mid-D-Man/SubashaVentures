// Enhanced wwwroot/js/connectivityServices.js
// Advanced connectivity checking with network quality detection

console.log("Enhanced connectivityServices.js loaded");

window.connectivityChecker = {
    dotNetReference: null,
    isOnline: navigator.onLine,
    checkIntervalId: null,
    networkQuality: 'unknown',
    lastOnlineTime: null,
    connectionHistory: [],
    
    // Enhanced online status getter with network quality
    getOnlineStatus: function() {
        const status = {
            isOnline: this.isOnline,
            networkQuality: this.networkQuality,
            lastOnlineTime: this.lastOnlineTime,
            connectionStability: this.getConnectionStability()
        };
        console.log("connectivityChecker.getOnlineStatus called:", status);
        return status;
    },

    // Simple boolean getter for backward compatibility
    getSimpleOnlineStatus: function() {
        return this.isOnline;
    },

    init: function (dotNetRef) {
        console.log("connectivityChecker.init called with dotNetRef:", dotNetRef);
        this.dotNetReference = dotNetRef;
        this.lastOnlineTime = this.isOnline ? Date.now() : null;

        // Add event listeners for online/offline events
        window.addEventListener('online', this.handleOnlineEvent.bind(this));
        window.addEventListener('offline', this.handleOfflineEvent.bind(this));

        // Setup periodic check every 15 seconds (more frequent for better UX)
        this.checkIntervalId = setInterval(this.checkConnectivity.bind(this), 15000);

        // Setup network quality monitoring
        this.setupNetworkQualityMonitoring();

        // Initial connectivity check
        this.checkConnectivity();

        return true;
    },

    setupNetworkQualityMonitoring: function() {
        // Use Connection API if available
        if ('connection' in navigator) {
            const connection = navigator.connection;
            
            connection.addEventListener('change', () => {
                this.updateNetworkQuality();
            });
            
            this.updateNetworkQuality();
        }

        // Fallback: Simple ping test
        setInterval(() => {
            if (this.isOnline) {
                this.performNetworkQualityTest();
            }
        }, 60000); // Check every minute
    },

    updateNetworkQuality: function() {
        if ('connection' in navigator) {
            const connection = navigator.connection;
            const effectiveType = connection.effectiveType;
            
            // Map connection types to quality levels
            const qualityMap = {
                'slow-2g': 'poor',
                '2g': 'poor',
                '3g': 'fair',
                '4g': 'good'
            };
            
            this.networkQuality = qualityMap[effectiveType] || 'unknown';
            console.log('Network quality updated:', this.networkQuality);
        }
    },

    performNetworkQualityTest: function() {
        const startTime = Date.now();
        
        // Test with a small resource
        fetch(window.location.origin + '/favicon.ico?t=' + Date.now(), {
            cache: 'no-cache',
            method: 'GET'
        })
        .then(response => {
            const endTime = Date.now();
            const responseTime = endTime - startTime;
            
            // Classify network quality based on response time
            if (responseTime < 500) {
                this.networkQuality = 'excellent';
            } else if (responseTime < 1000) {
                this.networkQuality = 'good';
            } else if (responseTime < 2000) {
                this.networkQuality = 'fair';
            } else {
                this.networkQuality = 'poor';
            }
            
            console.log('Network quality test completed:', this.networkQuality, 'Response time:', responseTime + 'ms');
        })
        .catch(error => {
            console.log('Network quality test failed:', error);
            this.networkQuality = 'poor';
        });
    },

    getConnectionStability: function() {
        if (this.connectionHistory.length < 2) return 'unknown';
        
        const recentHistory = this.connectionHistory.slice(-10); // Last 10 checks
        const changes = recentHistory.filter((status, index) => 
            index > 0 && status !== recentHistory[index - 1]
        ).length;
        
        if (changes === 0) return 'stable';
        if (changes <= 2) return 'mostly-stable';
        if (changes <= 5) return 'unstable';
        return 'very-unstable';
    },

    handleOnlineEvent: function () {
        console.log("handleOnlineEvent called.");
        if (!this.isOnline) {
            this.isOnline = true;
            this.lastOnlineTime = Date.now();
            this.connectionHistory.push(true);
            this.updateNetworkQuality();
            this.notifyDotNet(true);
            
            // Trigger any pending sync operations
            this.triggerBackgroundSync();
        }
    },

    handleOfflineEvent: function () {
        console.log("handleOfflineEvent called.");
        if (this.isOnline) {
            this.isOnline = false;
            this.networkQuality = 'offline';
            this.connectionHistory.push(false);
            this.notifyDotNet(false);
        }
    },

    checkConnectivity: function () {
        const currentStatus = navigator.onLine;
        
        // Add to connection history
        this.connectionHistory.push(currentStatus);
        
        // Keep history to last 50 entries
        if (this.connectionHistory.length > 50) {
            this.connectionHistory.shift();
        }
        
        if (this.isOnline !== currentStatus) {
            console.log("checkConnectivity: status changed from", this.isOnline, "to", currentStatus);
            this.isOnline = currentStatus;
            
            if (currentStatus) {
                this.lastOnlineTime = Date.now();
                this.updateNetworkQuality();
            } else {
                this.networkQuality = 'offline';
            }
            
            this.notifyDotNet(currentStatus);
        } else if (currentStatus) {
            // Still online, check if we need to update quality
            this.performNetworkQualityTest();
        }
    },

    notifyDotNet: function (isOnline) {
        console.log("notifyDotNet called with isOnline:", isOnline);
        if (this.dotNetReference) {
            const statusInfo = {
                isOnline: isOnline,
                networkQuality: this.networkQuality,
                lastOnlineTime: this.lastOnlineTime,
                connectionStability: this.getConnectionStability()
            };
            
            this.dotNetReference.invokeMethodAsync('OnConnectivityChanged', statusInfo)
                .then(() => { console.log("DotNet notified successfully with status:", statusInfo); })
                .catch(err => { console.error("Error notifying DotNet:", err); });
        } else {
            console.warn("dotNetReference is not set, cannot notify DotNet.");
        }
    },

    triggerBackgroundSync: function() {
        // Trigger any pending background sync operations
        if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
            navigator.serviceWorker.controller.postMessage({
                type: 'CONNECTIVITY_RESTORED'
            });
        }
        
        // Dispatch custom event for app components
        window.dispatchEvent(new CustomEvent('connectivity-restored', {
            detail: { 
                isOnline: this.isOnline,
                networkQuality: this.networkQuality 
            }
        }));
    },

    // Method to manually trigger a connectivity check
    forceCheck: function() {
        console.log("Force checking connectivity...");
        this.checkConnectivity();
        if (this.isOnline) {
            this.performNetworkQualityTest();
        }
    },

    // Get detailed connectivity report
    getConnectivityReport: function() {
        return {
            isOnline: this.isOnline,
            networkQuality: this.networkQuality,
            lastOnlineTime: this.lastOnlineTime,
            connectionStability: this.getConnectionStability(),
            connectionHistory: this.connectionHistory.slice(-20), // Last 20 checks
            hasConnection: 'connection' in navigator,
            connectionInfo: 'connection' in navigator ? {
                effectiveType: navigator.connection.effectiveType,
                downlink: navigator.connection.downlink,
                rtt: navigator.connection.rtt,
                saveData: navigator.connection.saveData
            } : null
        };
    },

    dispose: function () {
        console.log("Disposing connectivityChecker...");
        window.removeEventListener('online', this.handleOnlineEvent);
        window.removeEventListener('offline', this.handleOfflineEvent);

        if (this.checkIntervalId) {
            clearInterval(this.checkIntervalId);
            this.checkIntervalId = null;
        }

        this.dotNetReference = null;
        this.connectionHistory = [];
    }
};

// Enhanced global connectivity state management
window.getConnectivityState = function() {
    return {
        isOnline: window.connectivityChecker.isOnline,
        isOfflineMode: window.isOfflineMode || false,
        networkQuality: window.connectivityChecker.networkQuality,
        hasConnectivityChecker: !!window.connectivityChecker.dotNetReference
    };
};

// Global event dispatcher for connectivity changes
window.onConnectivityChange = function(callback) {
    window.addEventListener('connectivity-restored', callback);
    window.addEventListener('online', callback);
    window.addEventListener('offline', callback);
};

// Expose connectivity checker for debugging
window.debugConnectivity = function() {
    console.log('Connectivity Report:', window.connectivityChecker.getConnectivityReport());
};
