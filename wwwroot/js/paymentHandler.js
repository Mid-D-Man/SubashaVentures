// wwwroot/js/paymentHandler.js - Payment Gateway Integration
window.paymentHandler = {
    // ===================== PAYSTACK =====================
    
    /**
     * Initialize Paystack payment
     * @param {object} config - Payment configuration
     * @param {string} config.publicKey - Paystack public key
     * @param {string} config.email - Customer email
     * @param {number} config.amount - Amount in kobo (e.g., 50000 = ₦500)
     * @param {string} config.reference - Unique transaction reference
     * @param {string} config.currency - Currency code (NGN)
     * @param {object} config.metadata - Additional metadata
     * @returns {Promise<object>} Payment result
     */
    initializePaystack: async function(config) {
        return new Promise((resolve, reject) => {
            try {
                console.log('🔵 Initializing Paystack payment:', config);
                
                // Validate Paystack SDK is loaded
                if (typeof PaystackPop === 'undefined') {
                    reject({
                        success: false,
                        message: 'Paystack SDK not loaded. Please refresh the page.',
                        provider: 'paystack'
                    });
                    return;
                }

                const handler = PaystackPop.setup({
                    key: config.publicKey,
                    email: config.email,
                    amount: config.amount, // Amount in kobo
                    currency: config.currency || 'NGN',
                    ref: config.reference,
                    metadata: config.metadata || {},
                    
                    onSuccess: function(transaction) {
                        console.log('✅ Paystack payment successful:', transaction);
                        resolve({
                            success: true,
                            reference: transaction.reference,
                            message: transaction.message,
                            status: transaction.status,
                            transaction: transaction,
                            provider: 'paystack'
                        });
                    },
                    
                    onCancel: function() {
                        console.log('❌ Paystack payment cancelled');
                        reject({
                            success: false,
                            message: 'Payment was cancelled',
                            provider: 'paystack',
                            cancelled: true
                        });
                    },
                    
                    onError: function(error) {
                        console.error('❌ Paystack payment error:', error);
                        reject({
                            success: false,
                            message: error.message || 'Payment failed',
                            error: error,
                            provider: 'paystack'
                        });
                    }
                });

                // Open the payment modal
                handler.openIframe();
                
            } catch (error) {
                console.error('❌ Paystack initialization error:', error);
                reject({
                    success: false,
                    message: error.message || 'Failed to initialize payment',
                    error: error,
                    provider: 'paystack'
                });
            }
        });
    },

    // ===================== FLUTTERWAVE =====================
    
    /**
     * Initialize Flutterwave payment
     * @param {object} config - Payment configuration
     * @param {string} config.publicKey - Flutterwave public key
     * @param {string} config.email - Customer email
     * @param {string} config.name - Customer name
     * @param {string} config.phone - Customer phone number
     * @param {number} config.amount - Amount in main currency (e.g., 500 = ₦500)
     * @param {string} config.reference - Unique transaction reference
     * @param {string} config.currency - Currency code (NGN)
     * @param {object} config.metadata - Additional metadata
     * @returns {Promise<object>} Payment result
     */
    initializeFlutterwave: async function(config) {
        return new Promise((resolve, reject) => {
            try {
                console.log('🟢 Initializing Flutterwave payment:', config);
                
                // Validate Flutterwave SDK is loaded
                if (typeof FlutterwaveCheckout === 'undefined') {
                    reject({
                        success: false,
                        message: 'Flutterwave SDK not loaded. Please refresh the page.',
                        provider: 'flutterwave'
                    });
                    return;
                }

                FlutterwaveCheckout({
                    public_key: config.publicKey,
                    tx_ref: config.reference,
                    amount: config.amount, // Amount in main currency
                    currency: config.currency || 'NGN',
                    payment_options: 'card,ussd,banktransfer',
                    
                    customer: {
                        email: config.email,
                        name: config.name || config.email,
                        phone_number: config.phone || ''
                    },
                    
                    customizations: {
                        title: 'SubashaVentures',
                        description: 'Payment for order',
                        logo: 'https://yourwebsite.com/logo.png' // Update with your logo
                    },
                    
                    meta: config.metadata || {},
                    
                    callback: function(response) {
                        console.log('✅ Flutterwave payment callback:', response);
                        
                        if (response.status === 'successful' || response.status === 'completed') {
                            resolve({
                                success: true,
                                reference: response.tx_ref,
                                transactionId: response.transaction_id,
                                message: 'Payment successful',
                                status: response.status,
                                transaction: response,
                                provider: 'flutterwave'
                            });
                        } else {
                            reject({
                                success: false,
                                message: 'Payment failed or pending',
                                status: response.status,
                                transaction: response,
                                provider: 'flutterwave'
                            });
                        }
                    },
                    
                    onclose: function() {
                        console.log('❌ Flutterwave modal closed');
                        reject({
                            success: false,
                            message: 'Payment modal was closed',
                            provider: 'flutterwave',
                            cancelled: true
                        });
                    }
                });
                
            } catch (error) {
                console.error('❌ Flutterwave initialization error:', error);
                reject({
                    success: false,
                    message: error.message || 'Failed to initialize payment',
                    error: error,
                    provider: 'flutterwave'
                });
            }
        });
    },

    // ===================== UTILITY FUNCTIONS =====================
    
    /**
     * Generate unique payment reference
     * @returns {string} Unique reference
     */
    generateReference: function() {
        const timestamp = Date.now();
        const random = Math.floor(Math.random() * 1000000);
        return `SV-${timestamp}-${random}`;
    },

    /**
     * Convert amount to kobo (for Paystack)
     * @param {number} amount - Amount in Naira
     * @returns {number} Amount in kobo
     */
    toKobo: function(amount) {
        return Math.round(amount * 100);
    },

    /**
     * Convert amount from kobo to Naira
     * @param {number} kobo - Amount in kobo
     * @returns {number} Amount in Naira
     */
    fromKobo: function(kobo) {
        return kobo / 100;
    },

    /**
     * Format amount as currency
     * @param {number} amount - Amount to format
     * @param {string} currency - Currency code
     * @returns {string} Formatted amount
     */
    formatCurrency: function(amount, currency = 'NGN') {
        return new Intl.NumberFormat('en-NG', {
            style: 'currency',
            currency: currency,
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(amount);
    }
};

console.log('✅ Payment Handler initialized');
