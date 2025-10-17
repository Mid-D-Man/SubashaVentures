// wwwroot/js/firestoreModule.js - E-Commerce Version for SubashaVentures

window.firestoreModule = (function () {
    let db = null;
    let isInitialized = false;
    let isOffline = false;
    let manuallyDisconnected = false;
    let initializationPromise = null;

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
        if (initializationPromise) {
            return initializationPromise;
        }

        initializationPromise = (async () => {
            try {
                await waitForFirebase();

                if (isInitialized) {
                    return true;
                }

                db = firebase.firestore();
                db.settings({
                    ignoreUndefinedProperties: true,
                    timestampsInSnapshots: true
                });

                // Connection monitoring
                firebase.database().ref(".info/connected").on("value", (snapshot) => {
                    if (!manuallyDisconnected) {
                        isOffline = !snapshot.val();
                        console.log("Connection state:", isOffline ? "Offline" : "Online");
                    }
                });

                isInitialized = true;
                console.log("Firestore initialized successfully");
                return true;
            } catch (error) {
                console.error("Error initializing Firestore:", error);
                initializationPromise = null;
                return false;
            }
        })();

        return initializationPromise;
    }

    async function setConnectionState(connect) {
        try {
            if (!isInitialized) await initializeFirestore();

            manuallyDisconnected = !connect;

            if (connect) {
                await firebase.firestore().enableNetwork();
                isOffline = !navigator.onLine;
                console.log("Firebase connection manually enabled");
            } else {
                await firebase.firestore().disableNetwork();
                isOffline = true;
                console.log("Firebase connection manually disabled");
            }

            return true;
        } catch (error) {
            console.error("Error setting connection state:", error);
            return false;
        }
    }

    //#endregion

    //#region ==================== DOCUMENT OPERATIONS ====================

    async function getDocument(collection, id) {
        try {
            const initialized = await initializeFirestore();
            if (!initialized) throw new Error('Firestore not initialized');

            const docRef = db.collection(collection).doc(id);
            const doc = await docRef.get();

            if (doc.exists) {
                const data = doc.data();
                if (data && typeof data === 'object') {
                    data.id = doc.id;
                }
                return JSON.stringify(data);
            } else {
                console.log(`Document not found: ${collection}/${id}`);
                return null;
            }
        } catch (error) {
            console.error(`Error getting document ${collection}/${id}:`, error);
            return null;
        }
    }

    async function addDocument(collection, jsonData, customId = null) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = JSON.parse(JSON.stringify(data));

            let docRef;
            if (customId) {
                docRef = db.collection(collection).doc(customId);
                await docRef.set(data);
                return customId;
            } else {
                docRef = await db.collection(collection).add(data);
                return docRef.id;
            }
        } catch (error) {
            console.error("Error adding document:", error);
            if (isOffline) storeOfflineOperation({ collection, data: jsonData, operation: 'add', timestamp: Date.now() });
            return null;
        }
    }

    async function updateDocument(collection, id, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = removeUndefinedConservative(data);

            await db.collection(collection).doc(id).update(data);
            console.log(`Document ${collection}/${id} updated successfully`);
            return true;
        } catch (error) {
            console.error(`Error updating document ${collection}/${id}:`, error);
            return false;
        }
    }

    async function deleteDocument(collection, id) {
        try {
            if (!isInitialized) await initializeFirestore();

            await db.collection(collection).doc(id).delete();
            return true;
        } catch (error) {
            console.error(`Error deleting document ${collection}/${id}:`, error);
            if (isOffline) storeOfflineOperation({ collection, id, operation: 'delete', timestamp: Date.now() });
            return false;
        }
    }

    //#endregion

    //#region ==================== FIELD OPERATIONS ====================

    async function addOrUpdateField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = value;

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Field ${fieldName} updated in ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error updating field ${fieldName}:`, error);
            return false;
        }
    }

    async function updateFields(collection, docId, jsonFields) {
        try {
            if (!isInitialized) await initializeFirestore();

            let fields = JSON.parse(jsonFields);
            fields = removeUndefinedConservative(fields);

            await db.collection(collection).doc(docId).update(fields);
            console.log(`Multiple fields updated in ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error updating fields in ${collection}/${docId}:`, error);
            return false;
        }
    }

    async function removeField(collection, docId, fieldName) {
        try {
            if (!isInitialized) await initializeFirestore();

            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.delete();

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Field ${fieldName} removed from ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error removing field ${fieldName}:`, error);
            return false;
        }
    }

    async function removeFields(collection, docId, fieldNames) {
        try {
            if (!isInitialized) await initializeFirestore();

            const fieldsArray = JSON.parse(fieldNames);
            const updateData = {};

            fieldsArray.forEach(fieldName => {
                updateData[fieldName] = firebase.firestore.FieldValue.delete();
            });

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Fields ${fieldsArray.join(', ')} removed from ${collection}/${docId}`);
            return true;
        } catch (error) {
            console.error(`Error removing fields:`, error);
            return false;
        }
    }

    async function getField(collection, docId, fieldName) {
        try {
            if (!isInitialized) await initializeFirestore();

            const doc = await db.collection(collection).doc(docId).get();

            if (doc.exists) {
                const data = doc.data();
                const fieldValue = data[fieldName];
                return fieldValue !== undefined ? JSON.stringify(fieldValue) : null;
            }
            return null;
        } catch (error) {
            console.error(`Error getting field ${fieldName}:`, error);
            return null;
        }
    }

    //#endregion

    //#region ==================== ARRAY FIELD OPERATIONS ====================

    async function addToArrayField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.arrayUnion(value);

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Item added to array field ${fieldName}`);
            return true;
        } catch (error) {
            console.error(`Error adding to array field ${fieldName}:`, error);
            return false;
        }
    }

    async function removeFromArrayField(collection, docId, fieldName, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            let value = JSON.parse(jsonValue);
            const updateData = {};
            updateData[fieldName] = firebase.firestore.FieldValue.arrayRemove(value);

            await db.collection(collection).doc(docId).update(updateData);
            console.log(`Item removed from array field ${fieldName}`);
            return true;
        } catch (error) {
            console.error(`Error removing from array field ${fieldName}:`, error);
            return false;
        }
    }

    //#endregion

    //#region ==================== COLLECTION OPERATIONS ====================

    async function getCollection(collection) {
        try {
            if (!isInitialized) await initializeFirestore();

            const querySnapshot = await db.collection(collection).get();
            const data = [];

            querySnapshot.forEach((doc) => {
                const item = doc.data();
                if (item && typeof item === 'object') {
                    item.id = doc.id;
                }
                data.push(item);
            });

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error getting collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    async function queryCollection(collection, field, jsonValue) {
        try {
            if (!isInitialized) await initializeFirestore();

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

            return JSON.stringify(data);
        } catch (error) {
            console.error(`Error querying collection ${collection}:`, error);
            return JSON.stringify([]);
        }
    }

    async function addBatch(collection, jsonItems) {
        try {
            if (!isInitialized) await initializeFirestore();

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
            return true;
        } catch (error) {
            console.error(`Error adding batch to ${collection}:`, error);
            if (isOffline) storeOfflineOperation({ collection, data: jsonItems, operation: 'batch', timestamp: Date.now() });
            return false;
        }
    }

    //#endregion

    //#region ==================== DISTRIBUTED DOCUMENT OPERATIONS (FOR PRODUCTS) ====================

    async function addToDistributedDocument(collection, documentId, key, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            const updateData = {};
            updateData[key] = data;

            const docRef = db.collection(collection).doc(documentId);
            const doc = await docRef.get();

            if (doc.exists) {
                await docRef.update(updateData);
            } else {
                await docRef.set(updateData);
            }

            console.log(`Data added to distributed document ${collection}/${documentId}[${key}]`);
            return key;
        } catch (error) {
            console.error(`Error adding to distributed document:`, error);
            return null;
        }
    }

    async function updateFieldInDistributedDocument(collection, documentId, key, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            const updateData = {};
            updateData[key] = data;

            await db.collection(collection).doc(documentId).update(updateData);
            console.log(`Field updated in distributed document ${collection}/${documentId}[${key}]`);
            return true;
        } catch (error) {
            console.error(`Error updating field in distributed document:`, error);
            return false;
        }
    }

    async function documentContainsKey(collection, documentId, key) {
        try {
            if (!isInitialized) await initializeFirestore();

            const doc = await db.collection(collection).doc(documentId).get();

            if (!doc.exists) {
                return false;
            }

            const data = doc.data();
            return data && data.hasOwnProperty(key);
        } catch (error) {
            console.error(`Error checking if document contains key:`, error);
            return false;
        }
    }

    async function documentExists(collection, documentId) {
        try {
            if (!isInitialized) await initializeFirestore();

            const doc = await db.collection(collection).doc(documentId).get();
            return doc.exists;
        } catch (error) {
            console.error(`Error checking document existence:`, error);
            return false;
        }
    }

    async function getDocumentSizeInfo(collection, documentId) {
        try {
            if (!isInitialized) await initializeFirestore();

            const doc = await db.collection(collection).doc(documentId).get();

            if (!doc.exists) {
                return null;
            }

            const data = doc.data();
            const estimatedSize = estimateDocumentSize(data);
            const fieldCount = Object.keys(data).length;

            return JSON.stringify({
                EstimatedSize: estimatedSize,
                FieldCount: fieldCount,
                Exists: true
            });
        } catch (error) {
            console.error(`Error getting document size info:`, error);
            return null;
        }
    }

    function estimateDocumentSize(data) {
        try {
            const jsonString = JSON.stringify(data);
            return new Blob([jsonString]).size;
        } catch (error) {
            console.error("Error estimating document size:", error);
            return 0;
        }
    }

    async function getDocumentsWithPrefix(collection, prefix) {
        try {
            if (!isInitialized) await initializeFirestore();

            const endPrefix = prefix.slice(0, -1) + String.fromCharCode(prefix.charCodeAt(prefix.length - 1) + 1);

            const querySnapshot = await db.collection(collection)
                .where(firebase.firestore.FieldPath.documentId(), '>=', prefix)
                .where(firebase.firestore.FieldPath.documentId(), '<', endPrefix)
                .get();

            const documents = [];
            querySnapshot.forEach((doc) => {
                documents.push({
                    id: doc.id,
                    data: doc.data()
                });
            });

            return JSON.stringify(documents);
        } catch (error) {
            console.error(`Error getting documents with prefix:`, error);
            return JSON.stringify([]);
        }
    }

    async function batchGetDocuments(collection, documentIds) {
        try {
            if (!isInitialized) await initializeFirestore();

            const docIds = JSON.parse(documentIds);
            const batch = [];

            docIds.forEach(id => {
                batch.push(db.collection(collection).doc(id).get());
            });

            const docs = await Promise.all(batch);
            const results = [];

            docs.forEach((doc, index) => {
                if (doc.exists) {
                    const data = doc.data();
                    data.id = doc.id;
                    results.push(data);
                } else {
                    results.push(null);
                }
            });

            return JSON.stringify(results);
        } catch (error) {
            console.error(`Error batch getting documents:`, error);
            return JSON.stringify([]);
        }
    }

    async function removeKeyFromDistributedDocument(collection, documentId, key) {
        try {
            if (!isInitialized) await initializeFirestore();

            const updateData = {};
            updateData[key] = firebase.firestore.FieldValue.delete();

            await db.collection(collection).doc(documentId).update(updateData);
            console.log(`Key ${key} removed from distributed document ${collection}/${documentId}`);
            return true;
        } catch (error) {
            console.error(`Error removing key from distributed document:`, error);
            return false;
        }
    }

    async function getDocumentFieldCount(collection, documentId) {
        try {
            if (!isInitialized) await initializeFirestore();

            const doc = await db.collection(collection).doc(documentId).get();

            if (!doc.exists) {
                return 0;
            }

            const data = doc.data();
            return Object.keys(data).length;
        } catch (error) {
            console.error(`Error getting document field count:`, error);
            return 0;
        }
    }

    async function mergeIntoDistributedDocument(collection, documentId, jsonData) {
        try {
            if (!isInitialized) await initializeFirestore();

            let data = JSON.parse(jsonData);
            data = removeUndefinedConservative(data);

            const docRef = db.collection(collection).doc(documentId);
            await docRef.set(data, { merge: true });

            console.log(`Data merged into distributed document ${collection}/${documentId}`);
            return true;
        } catch (error) {
            console.error(`Error merging into distributed document:`, error);
            return false;
        }
    }

    async function getMultipleFieldsFromDistributedDocument(collection, documentId, fieldNames) {
        try {
            if (!isInitialized) await initializeFirestore();

            const fields = JSON.parse(fieldNames);
            const doc = await db.collection(collection).doc(documentId).get();

            if (!doc.exists) {
                return JSON.stringify(null);
            }

            const data = doc.data();
            const result = {};

            fields.forEach(fieldName => {
                if (data.hasOwnProperty(fieldName)) {
                    result[fieldName] = data[fieldName];
                }
            });

            return JSON.stringify(result);
        } catch (error) {
            console.error(`Error getting multiple fields from distributed document:`, error);
            return JSON.stringify(null);
        }
    }

    async function searchDistributedDocuments(collection, baseDocumentId, searchKey, searchValue) {
        try {
            if (!isInitialized) await initializeFirestore();

            const results = [];
            let documentIndex = 1;

            await searchInDocument(collection, baseDocumentId, searchKey, searchValue, results);

            while (true) {
                const currentDocumentId = `${baseDocumentId}_${documentIndex}`;
                const doc = await db.collection(collection).doc(currentDocumentId).get();

                if (!doc.exists) {
                    break;
                }

                await searchInDocument(collection, currentDocumentId, searchKey, searchValue, results);
                documentIndex++;
            }

            return JSON.stringify(results);
        } catch (error) {
            console.error(`Error searching distributed documents:`, error);
            return JSON.stringify([]);
        }
    }

    async function searchInDocument(collection, documentId, searchKey, searchValue, results) {
        try {
            const doc = await db.collection(collection).doc(documentId).get();

            if (!doc.exists) {
                return;
            }

            const data = doc.data();

            Object.keys(data).forEach(key => {
                const fieldData = data[key];
                if (fieldData && typeof fieldData === 'object' && fieldData[searchKey] === searchValue) {
                    results.push({
                        documentId: documentId,
                        key: key,
                        data: fieldData
                    });
                }
            });
        } catch (error) {
            console.error(`Error searching in document ${documentId}:`, error);
        }
    }

    async function getDistributedDocumentStats(collection, baseDocumentId) {
        try {
            if (!isInitialized) await initializeFirestore();

            const stats = {
                totalDocuments: 0,
                totalFields: 0,
                estimatedTotalSize: 0,
                documents: []
            };

            await addDocumentStats(collection, baseDocumentId, stats);

            let documentIndex = 1;
            while (true) {
                const currentDocumentId = `${baseDocumentId}_${documentIndex}`;
                const doc = await db.collection(collection).doc(currentDocumentId).get();

                if (!doc.exists) {
                    break;
                }

                await addDocumentStats(collection, currentDocumentId, stats);
                documentIndex++;
            }

            return JSON.stringify(stats);
        } catch (error) {
            console.error(`Error getting distributed document stats:`, error);
            return JSON.stringify({ error: error.message });
        }
    }

    async function addDocumentStats(collection, documentId, stats) {
        try {
            const doc = await db.collection(collection).doc(documentId).get();

            if (doc.exists) {
                const data = doc.data();
                const fieldCount = Object.keys(data).length;
                const estimatedSize = estimateDocumentSize(data);

                stats.totalDocuments++;
                stats.totalFields += fieldCount;
                stats.estimatedTotalSize += estimatedSize;

                stats.documents.push({
                    id: documentId,
                    fieldCount: fieldCount,
                    estimatedSize: estimatedSize
                });
            }
        } catch (error) {
            console.error(`Error adding stats for document ${documentId}:`, error);
        }
    }

    //#endregion

    //#region ==================== E-COMMERCE SPECIFIC OPERATIONS ====================

    async function findProductByCode(productCode) {
        try {
            if (!isInitialized) await initializeFirestore();

            const collections = ['Products', 'Products_Featured', 'Products_Sale'];

            for (const collection of collections) {
                try {
                    const querySnapshot = await db.collection(collection)
                        .where("productCode", "==", productCode)
                        .get();

                    if (!querySnapshot.empty) {
                        const doc = querySnapshot.docs[0];
                        const data = doc.data();
                        data.id = doc.id;
                        data.collection = collection;
                        return JSON.stringify(data);
                    }
                } catch (error) {
                    console.error(`Error searching in ${collection}:`, error);
                }
            }

            console.log(`Product ${productCode} not found in any collection`);
            return null;
        } catch (error) {
            console.error("Error in findProductByCode:", error);
            return null;
        }
    }

    async function deleteProductByCode(productCode) {
        try {
            if (!isInitialized) await initializeFirestore();

            const collections = ['Products', 'Products_Featured', 'Products_Sale'];

            for (const collection of collections) {
                try {
                    const querySnapshot = await db.collection(collection)
                        .where("productCode", "==", productCode)
                        .get();

                    if (!querySnapshot.empty) {
                        const docToDelete = querySnapshot.docs[0];
                        await docToDelete.ref.delete();
                        console.log(`Successfully deleted product ${productCode} from ${collection}`);
                        return true;
                    }
                } catch (error) {
                    console.error(`Error searching in ${collection}:`, error);
                }
            }

            console.log(`Product ${productCode} not found in any collection`);
            return false;
        } catch (error) {
            console.error("Error in deleteProductByCode:", error);
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
            console.log('Operation stored for offline use:', operation);
        } catch (error) {
            console.error('Error storing offline operation:', error);
        }
    }

    async function processPendingOperations() {
        if (!navigator.onLine || !isInitialized || manuallyDisconnected) return;

        const storageKey = 'firestore_offline_operations';
        try {
            const pendingOps = JSON.parse(localStorage.getItem(storageKey) || '[]');
            if (pendingOps.length === 0) return;

            console.log(`Processing ${pendingOps.length} pending operations`);
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
                    console.error('Error processing pending operation:', error, op);
                }
            }

            const remainingOps = pendingOps.filter(op =>
                !successfulOps.some(sop =>
                    sop.timestamp === op.timestamp &&
                    sop.operation === op.operation
                )
            );

            localStorage.setItem(storageKey, JSON.stringify(remainingOps));
            console.log(`Processed ${successfulOps.length} operations, ${remainingOps.length} remaining`);

        } catch (error) {
            console.error('Error processing pending operations:', error);
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
                connectedRef.on("value", (snap) => {
                    const connected = snap.val() === true;
                    isOffline = !connected;
                    resolve(connected);
                });
            });
        } catch (error) {
            console.error("Error checking connection:", error);
            return false;
        }
    }

    function getManualConnectionState() {
        return !manuallyDisconnected;
    }

    window.addEventListener('online', () => {
        if (!manuallyDisconnected) {
            console.log('Back online, processing pending operations');
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
        addBatch,

        // Distributed document functions (for products)
        addToDistributedDocument,
        updateFieldInDistributedDocument,
        documentContainsKey,
        documentExists,
        getDocumentSizeInfo,
        getDocumentsWithPrefix,
        batchGetDocuments,
        removeKeyFromDistributedDocument,
        getDocumentFieldCount,
        mergeIntoDistributedDocument,
        getMultipleFieldsFromDistributedDocument,
        searchDistributedDocuments,
        getDistributedDocumentStats,

        // E-commerce specific
        findProductByCode,
        deleteProductByCode
    };
})();