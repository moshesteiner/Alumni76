
// Global Function in a file like 'global.js'
function showCustomAlert(message) {
    // 1. Create the HTML structure for the modal
    const modalHtml = `
        <div id="custom-alert-overlay" style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0, 0, 0, 0.5); z-index: 9999;">
            <div style="position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); background: white; padding: 20px; border-radius: 8px; box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);">
                <h4 style="margin-top: 0; color: #0056b3;">מערכת מעקב מחוון</h4>
                <p>${message}</p>
                <button onclick="document.getElementById('custom-alert-overlay').remove()" style="float: right; padding: 5px 15px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer;">אישור</button>
            </div>
        </div>
    `;

    // 2. Insert the modal into the page body
    document.body.insertAdjacentHTML('beforeend', modalHtml);
}



// ⭐ WARNING: Overriding native functions is generally frowned upon, 
// but it fulfills your request for a transparent global change.

// Save a reference to the native alert function (in case you need it later)
const nativeAlert = window.alert;

// Override the global alert function
window.alert = function (message) {
    showCustomAlert(message);
    // Optionally: Use nativeAlert(message); if you want the ugly one AND the custom one.
};


// ------------------------------------------------------------------
// Custom Confirm Dialog Implementation
// ------------------------------------------------------------------

/**
 * Displays a custom confirmation modal that returns a Promise.
 * @param {string} message - The confirmation message to display.
 * @param {string} title - The title of the modal.
 * @returns {Promise<boolean>} - Resolves true for OK, false for Cancel.
 */
function showCustomConfirm(message, title = "אישור פעולה") {
    // Return a Promise that resolves true (OK) or false (Cancel)
    return new Promise(resolve => {
        const overlayId = 'custom-confirm-overlay';

        // Define cleanup and resolve functions
        const onConfirm = () => {
            document.getElementById(overlayId)?.remove();
            resolve(true);
        };

        const onCancel = () => {
            document.getElementById(overlayId)?.remove();
            resolve(false);
        };

        // Modal HTML structure (using basic inline styles for simplicity)
        const modalHtml = `
            <div id="${overlayId}" style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0, 0, 0, 0.5); z-index: 10000; display: flex; justify-content: center; align-items: center;">
                <div style="background: white; padding: 25px; border-radius: 8px; box-shadow: 0 4px 10px rgba(0, 0, 0, 0.3); max-width: 400px; width: 90%;">
                    <h4 style="margin-top: 0; color: #dc3545; border-bottom: 1px solid #dee2e6; padding-bottom: 10px;">${title}</h4>
                    <p style="margin-bottom: 20px;">${message}</p>
                    <div style="display: flex; justify-content: flex-end; gap: 10px;">
                        <button id="custom-confirm-cancel" style="padding: 8px 15px; background: #6c757d; color: white; border: none; border-radius: 4px; cursor: pointer;">ביטול</button>
                        <button id="custom-confirm-ok" style="padding: 8px 15px; background: #dc3545; color: white; border: none; border-radius: 4px; cursor: pointer;">אישור מחיקה</button>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Attach event listeners after insertion
        document.getElementById('custom-confirm-ok').onclick = onConfirm;
        document.getElementById('custom-confirm-cancel').onclick = onCancel;
    });
}