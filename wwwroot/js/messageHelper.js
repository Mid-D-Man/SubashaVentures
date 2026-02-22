// wwwroot/js/messageHelper.js
// Queries Firebase Firestore for the user's total unread message count.
// Called by NotificationService via IJSRuntime.

window.messageHelper = (function () {

    /**
     * Returns the total unread conversation count for a user.
     * Path: messages/{userId}/conversations — field: unread_count
     */
    async function getUserUnreadCount(userId) {
        if (!userId) return 0;

        try {
            const db = firebase.firestore();
            const snap = await db
                .collection('messages')
                .doc(userId)
                .collection('conversations')
                .where('unread_count', '>', 0)
                .get();

            let total = 0;
            snap.forEach(doc => {
                const data = doc.data();
                total += (data.unread_count || 0);
            });

            return total;
        } catch (e) {
            // Firebase may not be ready on first load — silent fail is correct
            console.warn('[messageHelper] getUserUnreadCount:', e.message);
            return 0;
        }
    }

    return { getUserUnreadCount };

})();
