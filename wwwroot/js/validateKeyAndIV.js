// Key/IV Validation for AirCode Offline Credentials - Updated with MID_HelperFunctions
// Run  in browser console to validate  constants

const TEST_KEY = "VGhpcyBpcyBhIHRlc3Qga2V5IGZvciBBaXJDb2RlIHRlc3Rpbmc=";
const TEST_IV = "UmFuZG9tSVZmb3JUZXN0";

function validateKeyIV() {
    midHelperFunctions.debugMessage("=== AirCode Key/IV Validation Started ===", DebugClass.INFO);

    try {
        // Validate TEST_KEY
        const keyBuffer = atob(TEST_KEY);
        const keyBytes = new Uint8Array(keyBuffer.length);
        for (let i = 0; i < keyBuffer.length; i++) {
            keyBytes[i] = keyBuffer.charCodeAt(i);
        }

        midHelperFunctions.debugMessage("TEST_KEY decoded successfully", DebugClass.INFO, {
            lengthBytes: keyBytes.length,
            lengthBits: keyBytes.length * 8,
            validForAES: [16, 24, 32].includes(keyBytes.length)
        });

        if (keyBytes.length !== 32) {
            midHelperFunctions.debugMessage("TEST_KEY should be 32 bytes (256 bits) for AES-256", DebugClass.WARNING, {
                currentLength: keyBytes.length,
                expectedLength: 32
            });
        }

    } catch (error) {
        midHelperFunctions.debugMessage("TEST_KEY validation failed", DebugClass.ERROR, {
            error: error.message,
            testKey: TEST_KEY.substring(0, 20) + "..." // Only log partial key for security
        });
        return false;
    }

    try {
        // Validate TEST_IV
        const ivBuffer = atob(TEST_IV);
        const ivBytes = new Uint8Array(ivBuffer.length);
        for (let i = 0; i < ivBuffer.length; i++) {
            ivBytes[i] = ivBuffer.charCodeAt(i);
        }

        midHelperFunctions.debugMessage("TEST_IV decoded successfully", DebugClass.INFO, {
            lengthBytes: ivBytes.length,
            validForAES_CBC: ivBytes.length === 16
        });

        if (ivBytes.length !== 16) {
            midHelperFunctions.debugMessage("TEST_IV must be exactly 16 bytes for AES-CBC", DebugClass.ERROR, {
                currentLength: ivBytes.length,
                expectedLength: 16
            });
            return false;
        }

    } catch (error) {
        midHelperFunctions.debugMessage("TEST_IV validation failed", DebugClass.ERROR, {
            error: error.message,
            testIV: TEST_IV.substring(0, 10) + "..." // Only log partial IV for security
        });
        return false;
    }

    midHelperFunctions.debugMessage("Key/IV validation completed successfully", DebugClass.INFO);
    return true;
}

// Generate correct test constants
function generateCorrectTestConstants() {
    midHelperFunctions.debugMessage("=== Generating Correct Test Constants ===", DebugClass.INFO);

    try {
        // Generate 32-byte key (256-bit)
        const keyBytes = new Uint8Array(32);

        // Use crypto.getRandomValues if available, fallback to Math.random
        if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
            crypto.getRandomValues(keyBytes);
            midHelperFunctions.debugMessage("Using cryptographically secure random generation", DebugClass.INFO);
        } else {
            // Fallback for older browsers
            for (let i = 0; i < keyBytes.length; i++) {
                keyBytes[i] = Math.floor(Math.random() * 256);
            }
            midHelperFunctions.debugMessage("Using Math.random fallback (less secure)", DebugClass.WARNING);
        }

        const keyBase64 = btoa(String.fromCharCode(...keyBytes));

        // Generate 16-byte IV
        const ivBytes = new Uint8Array(16);
        if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
            crypto.getRandomValues(ivBytes);
        } else {
            for (let i = 0; i < ivBytes.length; i++) {
                ivBytes[i] = Math.floor(Math.random() * 256);
            }
        }
        const ivBase64 = btoa(String.fromCharCode(...ivBytes));

        midHelperFunctions.debugMessage("New constants generated", DebugClass.INFO, {
            keyLength: keyBase64.length,
            ivLength: ivBase64.length,
            keyPreview: keyBase64.substring(0, 10) + "...", // Only log preview for security
            ivPreview: ivBase64.substring(0, 10) + "..."
        });

        // Only show full values in development mode
        if (midHelperFunctions.isDebugMode()) {
            midHelperFunctions.debugMessage("New TEST_KEY (32 bytes)", DebugClass.INFO, { key: keyBase64 });
            midHelperFunctions.debugMessage("New TEST_IV (16 bytes)", DebugClass.INFO, { iv: ivBase64 });
        } else {
            midHelperFunctions.debugMessage("Full constants not displayed in production mode", DebugClass.INFO);
        }

        return { keyBase64, ivBase64 };
    } catch (error) {
        midHelperFunctions.debugMessage("Failed to generate test constants", DebugClass.ERROR, {
            error: error.message
        });
        return null;
    }
}

// Test encryption/decryption with current constants
async function testEncryptionFlow() {
    midHelperFunctions.debugMessage("=== Testing Encryption Flow ===", DebugClass.INFO);

    const testData = "Hello, AirCode!";

    return await midHelperFunctions.safeExecute(async () => {
        // Check if cryptographyHandler is available
        if (!window.cryptographyHandler) {
            midHelperFunctions.debugMessage("cryptographyHandler not available - cannot test encryption", DebugClass.WARNING);
            return false;
        }

        // Test encryption with performance measurement
        const encrypted = await midHelperFunctions.measurePerformance(
            () => window.cryptographyHandler.encryptData(testData, TEST_KEY, TEST_IV),
            'Encryption'
        );

        if (!encrypted) {
            midHelperFunctions.debugMessage("Encryption returned null/empty result", DebugClass.ERROR);
            return false;
        }

        midHelperFunctions.debugMessage("Encryption successful", DebugClass.INFO, {
            originalLength: testData.length,
            encryptedLength: encrypted.length
        });

        // Test decryption with performance measurement
        const decrypted = await midHelperFunctions.measurePerformance(
            () => window.cryptographyHandler.decryptData(encrypted, TEST_KEY, TEST_IV),
            'Decryption'
        );

        const isMatch = decrypted === testData;
        midHelperFunctions.debugMessage(`Decryption ${isMatch ? 'successful' : 'failed'}`,
            isMatch ? DebugClass.INFO : DebugClass.ERROR, {
                originalData: testData,
                decryptedData: decrypted,
                matches: isMatch
            });

        return isMatch;
    }, 'Encryption Flow Test');
}

// Enhanced validation with comprehensive error handling
async function runFullValidation() {
    midHelperFunctions.debugMessage("Starting full validation suite", DebugClass.INFO, {
        environment: midHelperFunctions.getEnvironment(),
        debugMode: midHelperFunctions.isDebugMode(),
        timestamp: new Date().toISOString()
    });

    const startTime = performance.now();

    try {
        // Step 1: Validate current constants
        const isValid = validateKeyIV();

        // Step 2: Generate new constants if needed
        if (!isValid) {
            midHelperFunctions.debugMessage("Current constants invalid, generating new ones", DebugClass.WARNING);
            const newConstants = generateCorrectTestConstants();

            if (newConstants && midHelperFunctions.isDebugMode()) {
                midHelperFunctions.debugMessage("C# Constants Update Required", DebugClass.INFO, {
                    instructions: "Update your C# constants with the following values",
                    testKey: `internal static string TEST_KEY = "${newConstants.keyBase64}";`,
                    testIV: `internal static string TEST_IV = "${newConstants.ivBase64}";`
                });
            }
        }

        // Step 3: Test encryption flow
        const encryptionWorking = await testEncryptionFlow();

        // Step 4: Final summary
        const endTime = performance.now();
        const duration = endTime - startTime;

        const summary = {
            constantsValid: isValid,
            encryptionWorking: encryptionWorking || false,
            totalDuration: Math.round(duration),
            environment: midHelperFunctions.getEnvironment(),
            timestamp: new Date().toISOString()
        };

        midHelperFunctions.debugMessage("Validation suite completed", DebugClass.INFO, summary);

        // Show summary using our helper instead of console
        if (midHelperFunctions.isDebugMode()) {
            midHelperFunctions.debugMessage("VALIDATION SUMMARY", DebugClass.INFO, {
                constantsValid: summary.constantsValid,
                encryptionWorking: summary.encryptionWorking,
                totalDurationMs: summary.totalDuration,
                environment: summary.environment,
                completedAt: summary.timestamp
            });
        }

        return summary;

    } catch (error) {
        const endTime = performance.now();
        const duration = endTime - startTime;

        midHelperFunctions.debugMessage("Validation suite failed", DebugClass.EXCEPTION, {
            error: error.message,
            stack: error.stack,
            duration: Math.round(duration)
        });

        throw error;
    }
}

// Auto-execute validation with error boundary
(async function() {
    try {
        // Wait a bit for other scripts to load
        await new Promise(resolve => setTimeout(resolve, 100));

        midHelperFunctions.debugMessage("Auto-executing validation suite", DebugClass.INFO);
        await runFullValidation();

    } catch (error) {
        midHelperFunctions.debugMessage("Auto-validation failed", DebugClass.EXCEPTION, {
            error: error.message
        });

        // Still allow manual execution
        midHelperFunctions.debugMessage("Automatic validation failed, but functions are available for manual execution:");
        midHelperFunctions.debugMessage("- runFullValidation()");
        midHelperFunctions.debugMessage("- validateKeyIV()");
        midHelperFunctions.debugMessage("- testEncryptionFlow()");
    }
})();

// Export functions for manual execution
window.cryptoValidation = {
    runFullValidation,
    validateKeyIV,
    testEncryptionFlow,
    generateCorrectTestConstants
};