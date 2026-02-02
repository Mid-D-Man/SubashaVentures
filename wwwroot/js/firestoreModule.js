// wwwroot/js/firestoreModule.js 

window.firestoreModule = (function () {
    let db = null;
    let isInitialized = false;
    let isOffline = false;
    let manuallyDisconnected = false;
    let initializationPromise = null;
    let firebaseApp = null;

    function waitForFirebase() {
        return new Promise((resolve, reject) => {
            if (typeof firebase !== 'undefined' && firebase.firestore) {
                resolve();
                return;
            }

            let attempts = 0;
            const maxAttempts = 50;
            const checkInterval = setInterval(() => {
                attempts++;
                if (typeof firebase !== 'undefined' && firebase.firestore) {
                    clearInterval(checkInterval);
                    resolve();
                } else if (attempts >= maxAttempts) {
                    clearInterval(checkInterval);
                    reject(new Error('Firebase SDK failed to load within timeout'));
                }
            }, 100);
        });
    }

    //#region ==================== INITIALIZATION ====================

    async function initializeFirestore() {
        // Return existing promise if initialization is in progress
        if (initializationPromise) {
            console.log("⏳ Firestore initialization already in progress, waiting...");
            return initializationPromise;
        }

        //  If already initialized, return immediately
        if (isInitialized && db) {
            console.log("✓ Firestore already initialized, reusing existing instance");
            return true;
        }

        initializationPromise = (async () => {
            try {
                console.log("🔄 Starting Firestore initialization...");
                await waitForFirebase();

                // Double-check after waiting for Firebase
                if (isInitialized && db) {
                    console.log("✓ Firestore was initialized while waiting");
                    return true;
                }

                //  Get existing Firestore instance if it exists
                try {
                    db = firebase.firestore();
                    console.log("✓ Retrieved existing Firestore instance");
                } catch (error) {
                    console.error("❌ Error getting Firestore instance:", error);
                    throw error;
                }

                //  Only set settings if this is truly the first initialization
                // Check if settings have already been applied by checking if we can get a collection
                try {
                    // Try a simple operation to see if Firestore is ready
                    const testRef = db.collection('_test_connection');
                    console.log("✓ Firestore instance is ready");
                } catch (settingsError) {
                    // If we get an error, it might be because settings weren't applied
                    console.log("⚙️ Applying Firestore settings...");
                    try {
                        db.settings({
                            ignoreUndefinedProperties: true,
                            timestampsInSnapshots: true
                        });
                        console.log("✓ Firestore settings applied");
                    } catch (settingsApplyError) {
                        // Settings might have already been applied, which is fine
                        console.log("ℹ️ Firestore settings already configured:", settingsApplyError.message);
                    }
                }

                // Connection monitoring
                try {
                    firebase.database().ref(".info/connected").on("value", (snapshot) => {
                        if (!manuallyDisconnected) {
                            isOffline = !snapshot.val();
                            console.log("🔌 Connection state:", isOffline ? "Offline" : "Online");
                        }
                    });
                } catch (monitorError) {
                    console.warn("⚠️ Could not set up connection monitoring:", monitorError.message);
                }

                isInitialized = true;
                console.log("✅ Firestore initialized successfully");
                return true;
            } catch (error) {
                console.error("❌ Error initializing Firestore:", error);
                initializationPromise = null;
                isInitialized = false;
                return false;
            }
        })();

        return initializationPromise;
    }

    async function setConnectionState(connect) {
        try {
            if (!isInitialized) {
                console.log("⚠️ Initializing Firestore before setting connection state...");
                await initializeFirestore();
            }

            manuallyDisconnected = !connect;

            if (connect) {
                await firebase.firestore().enableNetwork();
                isOffline = !navigator.onLine;
                console.log("✓ Firebase connection manually enabled");
            } else {
                await firebase.firestore().disableNetwork();
                isOffline = true;
                console.log("✓ Firebase connection manually disabled");
            }

            return true;
        } catch (error) {
            console.error("❌ Error setting connection state:", error);
            return false;
        }
    }

    //#endregion

    //#region ==================== DOCUMENT OPERATIONS ====================

    async function getDocument(collection, id) {
        try {
            console.log(`📖 Getting document: ${collection}/${id}`);

            //  Always ensure initialization
            if (!isInitialized || !db) {
                console.log("⚠️ Firestore not initialized, initializing now...");
                const initialized = await initializeFirestore();
                if (!initialized) {
                    console.error("❌ Failed to initialize Firestore");
                    throw new Error('Firestore not initialized');
                }
            }

            const docRef = db.collection(collection).doc(id);
            const doc = await docRef.get();

            if (doc.exists) {
                const data = doc.data();
                if (data && typeof data === 'object') {
                    data.id = doc.id;
                }
                console.log(`✓ Document found: ${collection}/${id}`);
                return JSON.stringify(data);
            } else {
                console.log(`⚠️ Document not found: ${collection}/${id}`);
                return null;
            }
        } catch (error) {
            console.error(`❌ Error getting document ${collection}/${id}:`, error);
            throw error; // Re-throw to let caller handle it
        }
    }

    async function addDocument(collection, jsonData, customId = null) {
        try {
            console.log(`➕ Adding document to ${collection}${customId ? ` with ID ${customId}` : ''}`);

            if (!isInitialized || !db) {
                console.log("⚠️ Firestore not initialized, initializing now...");
                await initializeFirestore();
            }

            let data = JSON.parse(jsonData);
            data = JSON.parse(JSON.stringify(data));

            let docRef;
            if (customId) {
                docRef = db.collection(collection).doc(customId);
                await docRef.set(data);
                console.log(`✓ Document created: ${collection}/${customId}`);
                return customId;
            } else {
                docRef = await db.collection(collection).add(data);
                console.log(`✓ Document created: ${collection}/${docRef.id}`);
                return docRef.id;
            }
        } catch (error) {
            console.error(`❌ Error adding document to ${collection}:`, error);
            if (isOffline) storeOfflineOperation({ collection, data: jsonData, operation: 'add', timestamp: Date.now() });
            return null;
        }
    }

    async function updateDocument(collection, id, jsonData) {
        try {
            console.log(`✏️ Updating document: ${collection}/${id}`);

            if (!isInitialized || !db) {
                console.log("⚠️ Firestore not initialized, initializing now...");
                await initializeFirestore();
            }

            let data = JSON.parse(jsonData);
            data = removeUndefinedConservative(data);

            await db.collection(collection).doc(id).update(data);
            console.log(`✓ Document updated: ${collection}/${id}`);
            return true;
        } catch (error) {
            console.error(`❌ Error updating document ${collection}/${id}:`, error);
            return false;
        }
    }

    async function deleteDocument(collection, id) {
        try {
            console.log(`🗑️ Deleting document: ${collection}/${id}`);

            if (!isInitialized || !db) {
                console.log("⚠️ Firestore not initialized, initializing now...");
                await initializeFirestore();
            }

            await db.collection(collection).doc(id).delete();
            console.log(`✓ Document deleted: ${collection}/${id}`);
            return true;
        } catch (error) {
            console.error(`❌ Error deleting document ${collection}/${id}:`, error);
            if (isOffline) storeOfflineOperation({ collection, id, operation: 'delete', timestamp: Date.now() });
            return false;
        }
    }

    //#endregion

    //#region ==================== FIELD OPERATIONS ====================

    async function addOrUpdateField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = value;

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`✓ Field ${fieldName} updated in ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`❌ Error updating field ${fieldName}:`, error);
            return false;
        }
    }

    async function updateFields(collection, docId, jsonFields) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            let fields = JSON.parse(jsonFields);
            fields = removeUndefinedConservative(fields);

            await db.collection(collection).doc(docId).update(fields);
            console.log(`✓ Multiple fields updated in ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`❌ Error updating fields in ${collection}/${docId}:`, error);
            return false;
        }
    }

    async function removeField(collection, docId, fieldName) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.delete();

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`✓ Field ${fieldName} removed from ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`❌ Error removing field ${fieldName}:`, error);
            return false;
        }
    }

    async function removeFields(collection, docId, fieldNames) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            const fieldsArray = JSON.parse(fieldNames);
            const updateData = {};

            fieldsArray.forEach(fieldName => {
                updateData[fieldName] = firebase.firestore.FieldValue.delete();
            });

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`✓ Fields ${fieldsArray.join(', ')} removed from ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`❌ Error removing fields:`, error);
            return false;
        }
    }

    async function getField(collection, docId, fieldName) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            const doc = await db.collection(collection).doc(docId).get();

            if (doc.exists) {
                const data = doc.data();
                const fieldValue = data[fieldName];
                return fieldValue !== undefined ? JSON.stringify(fieldValue) : null;
            }
            return null;
        } catch (error) {
            console.error(`❌ Error getting field ${fieldName}:`, error);
            return null;
        }
    }

    //#endregion

    //#region ==================== ARRAY FIELD OPERATIONS ====================

    async function addToArrayField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.arrayUnion(value);

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`✓ Item added to array field ${fieldName}`);
            return true;
        } catch (error) {
            console.error(`❌ Error adding to array field ${fieldName}:`, error);
            return false;
        }
    }

    async function removeFromArrayField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.arrayRemove(value);

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`✓ Item removed from array field ${fieldName}`);
            return true;
        } catch (error) {
            console.error(`❌ Error removing from array field ${fieldName}:`, error);
            return false;
        }
    }

    //#endregion

    //#region ==================== COLLECTION OPERATIONS ====================

    async function getCollection(collection) {
        try {
            console.log(`📚 Getting collection: ${collection}`);

            if (!isInitialized || !db) {
                console.log("⚠️ Firestore not initialized, initializing now...");
                await initializeFirestore();
            }

            const querySnapshot = await db.collection(collection).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            console.log(`✓ Retrieved ${data.length} documents from ${collection}`);
            return JSON.stringify(data);
        } catch (error) {
            console.error(`❌ Error getting collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    async function queryCollection(collection, field, jsonValue) {
        try {
            console.log(`🔍 Querying collection ${collection} where ${field} == ${jsonValue}`);

            if (!isInitialized || !db) {
                console.log("⚠️ Firestore not initialized, initializing now...");
                await initializeFirestore();
            }

            let value = JSON.parse(jsonValue);
            const querySnapshot = await db.collection(collection).where(field, "==", value).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            console.log(`✓ Query returned ${data.length} documents`);
            return JSON.stringify(data);
        } catch (error) {
            console.error(`❌ Error querying collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    async function addBatch(collection, jsonItems) {
        try {
            if (!isInitialized || !db) await initializeFirestore();

            let items = JSON.parse(jsonItems);
            items = JSON.parse(JSON.stringify(items));

            const batch = db.batch();

            items.forEach((item) => {
                const docId = item.id || db.collection(collection).doc().id;
                const docRef = db.collection(collection).doc(docId);
                const itemCopy = {...item};

                if ('id' in itemCopy) {
                    delete itemCopy.id;
                }

                batch.set(docRef, itemCopy);
            });

            await batch.commit();
            console.log(`✓ Batch added ${items.length} documents to ${collection}`);
            return true;
        } catch (error) {
            console.error(`❌ Error adding batch to ${collection}:`, error);
            if (isOffline) storeOfflineOperation({ collection, data: jsonItems, operation: 'batch', timestamp: Date.now() });
            return false;
        }
    }

    //#endregion

    //#region ==================== UTILITY FUNCTIONS ====================

    function removeUndefinedConservative(obj) {
        if (obj === null || typeof obj !== 'object') return obj;

        if (Array.isArray(obj)) {
            return obj.map(removeUndefinedConservative);
        }

        const cleaned = {};
        for (const [key, value] of Object.entries(obj)) {
            if (value !== undefined) {
                if (typeof value === 'object' && value !== null) {
                    const cleanedValue = removeUndefinedConservative(value);
                    if (Array.isArray(cleanedValue) || Object.keys(cleanedValue).length > 0) {
                        cleaned[key] = cleanedValue;
                    }
                } else {
                    cleaned[key] = value;
                }
            }
        }
        return cleaned;
    }

    //#endregion

    //#region ==================== OFFLINE SUPPORT ====================

    function storeOfflineOperation(operation) {
        try {
            const storageKey = 'firestore_offline_operations';
            const existingOps = JSON.parse(localStorage.getItem(storageKey) || '[]');
            existingOps.push(operation);
            localStorage.setItem(storageKey, JSON.stringify(existingOps));
            console.log('💾 Operation stored for offline use:', operation);
        } catch (error) {
            console.error('❌ Error storing offline operation:', error);
        }
    }

    async function processPendingOperations() {
        if (!navigator.onLine || !isInitialized || manuallyDisconnected) return;

        const storageKey = 'firestore_offline_operations';
        try {
            const pendingOps = JSON.parse(localStorage.getItem(storageKey) || '[]');
            if (pendingOps.length === 0) return;

            console.log(`⚙️ Processing ${pendingOps.length} pending operations`);
            pendingOps.sort((a, b) => a.timestamp - b.timestamp);

            const successfulOps = [];

            for (const op of pendingOps) {
                try {
                    let success = false;

                    switch (op.operation) {
                        case 'add':
                            const addResult = await addDocument(op.collection, op.data, op.id);
                            success = !!addResult;
                            break;
                        case 'update':
                            success = await updateDocument(op.collection, op.id, op.data);
                            break;
                        case 'delete':
                            success = await deleteDocument(op.collection, op.id);
                            break;
                        case 'batch':
                            success = await addBatch(op.collection, op.data);
                            break;
                    }

                    if (success) {
                        successfulOps.push(op);
                    }
                } catch (error) {
                    console.error('❌ Error processing pending operation:', error, op);
                }
            }

            const remainingOps = pendingOps.filter(op =>
                !successfulOps.some(sop =>
                    sop.timestamp === op.timestamp &&
                    sop.operation === op.operation
                )
            );

            localStorage.setItem(storageKey, JSON.stringify(remainingOps));
            console.log(`✓ Processed ${successfulOps.length} operations, ${remainingOps.length} remaining`);

        } catch (error) {
            console.error('❌ Error processing pending operations:', error);
        }
    }

    async function isConnected() {
        try {
            if (manuallyDisconnected) return false;

            if (!isInitialized) {
                const initResult = await initializeFirestore();
                if (!initResult) return false;
            }

            return new Promise((resolve) => {
                const connectedRef = firebase.database().ref(".info/connected");
                connectedRef.once("value", (snap) => {
                    const connected = snap.val() === true;
                    isOffline = !connected;
                    resolve(connected);
                });
            });
        } catch (error) {
            console.error("❌ Error checking connection:", error);
            return false;
        }
    }

    function getManualConnectionState() {
        return !manuallyDisconnected;
    }

    window.addEventListener('online', () => {
        if (!manuallyDisconnected) {
            console.log('🌐 Back online, processing pending operations');
            processPendingOperations();
        }
    });

    //#endregion

    // Public API
    return {
        // Initialization
        initializeFirestore,
        setConnectionState,
        getManualConnectionState,
        isConnected,
        processPendingOperations,

        // Document operations
        getDocument,
        addDocument,
        updateDocument,
        deleteDocument,

        // Field operations
        addOrUpdateField,
        updateFields,
        removeField,
        removeFields,
        getField,

        // Array field operations
        addToArrayField,
        removeFromArrayField,

        // Collection operations
        getCollection,
        queryCollection,
        addBatch
    };
})();