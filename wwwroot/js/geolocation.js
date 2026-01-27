// wwwroot/js/geolocation.js - IP-based geolocation (no API key needed)
window.geolocationHelper = {
    // Get user's location from IP address (simple, no permissions needed)
    getLocationFromIP: async function () {
        try {
            console.log('🌍 Getting location from IP address...');
            
            const response = await fetch('https://ipapi.co/json/');
            if (!response.ok) {
                throw new Error('IP geolocation failed');
            }

            const data = await response.json();
            
            console.log('✅ IP geolocation successful:', data);
            
            return {
                addressLine1: data.city ? `${data.city} Area` : '',
                city: data.city || '',
                state: data.region || '',
                postalCode: data.postal || '',
                country: data.country_name || 'Nigeria',
                countryCode: data.country_code || 'NG',
                latitude: data.latitude || null,
                longitude: data.longitude || null,
                formattedAddress: `${data.city || ''}, ${data.region || ''}, ${data.country_name || 'Nigeria'}`.trim().replace(/^,\s*|,\s*$/g, '')
            };
        } catch (error) {
            console.error('❌ IP geolocation error:', error);
            return null;
        }
    },

    // Optional: Get precise location using browser GPS (requires user permission)
    getCurrentPosition: function () {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject('Geolocation is not supported by this browser');
                return;
            }

            console.log('📍 Requesting GPS location...');

            navigator.geolocation.getCurrentPosition(
                position => {
                    console.log('✅ GPS location obtained');
                    resolve({
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy
                    });
                },
                error => {
                    let errorMessage = 'Failed to get location';
                    switch (error.code) {
                        case error.PERMISSION_DENIED:
                            errorMessage = 'Location permission denied';
                            break;
                        case error.POSITION_UNAVAILABLE:
                            errorMessage = 'Location information unavailable';
                            break;
                        case error.TIMEOUT:
                            errorMessage = 'Location request timed out';
                            break;
                    }
                    console.warn('⚠️ GPS location failed:', errorMessage);
                    reject(errorMessage);
                },
                {
                    enableHighAccuracy: true,
                    timeout: 10000,
                    maximumAge: 0
                }
            );
        });
    }
};
