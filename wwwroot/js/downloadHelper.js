// wwwroot/js/downloadHelper.js
// Helper function for downloading files from Blazor

/**
 * Download file from base64 data
 * @param {string} filename - Name of the file to download
 * @param {string} base64Data - Base64 encoded file data
 * @param {string} contentType - MIME type of the file
 */
window.downloadFile = function(filename, base64Data, contentType) {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: contentType });
        
        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        
        // Trigger download
        document.body.appendChild(link);
        link.click();
        
        // Cleanup
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        
        console.log('✓ File downloaded:', filename);
        return true;
    } catch (error) {
        console.error('✗ File download failed:', error);
        return false;
    }
};

/**
 * Download text content as file
 * @param {string} filename - Name of the file to download
 * @param {string} textContent - Text content to download
 * @param {string} contentType - MIME type (default: text/plain)
 */
window.downloadTextFile = function(filename, textContent, contentType = 'text/plain') {
    try {
        const blob = new Blob([textContent], { type: contentType });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        
        document.body.appendChild(link);
        link.click();
        
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        
        console.log('✓ Text file downloaded:', filename);
        return true;
    } catch (error) {
        console.error('✗ Text file download failed:', error);
        return false;
    }
};
