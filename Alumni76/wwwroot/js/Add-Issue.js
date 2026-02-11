// =======================================================
// 🚨 GLOBAL STATE & FUNCTIONS (Defined outside DOMContentLoaded)
// This placement ensures they are available to all handlers, including the global paste handler.
// =======================================================

// Global variable to track the currently active paste button element
let activePasteButton = null;
const ORIGINAL_PASTE_TEXT = "הדבק"; // Assuming this is the original text

/**
 * Toggles the button's visual state for paste mode.
 */
function managePasteButtonState(buttonElement, activate) {
    const ORIGINAL_CLASS = 'btn-outline-primary';
    const ACTIVE_CLASS = 'btn-warning';
    if (activate) {
        // Reset any other active button first
        if (activePasteButton && activePasteButton !== buttonElement) {
            managePasteButtonState(activePasteButton, false);
        }

        // 🟢 Activate Paste Mode: Remove original class, add warning class
        buttonElement.classList.remove(ORIGINAL_CLASS);
        buttonElement.classList.add(ACTIVE_CLASS);
        buttonElement.textContent = 'העתק מהמסך, והדבק בעזרת V^';
        activePasteButton = buttonElement;

    } else {
        // 🔴 Reset Paste Mode: Remove warning class, add original class back
        buttonElement.classList.remove(ACTIVE_CLASS);
        buttonElement.classList.add(ORIGINAL_CLASS);
        buttonElement.textContent = ORIGINAL_PASTE_TEXT;
        activePasteButton = null;
    }
}

// =======================================================
// DOM CONTENT LOADED
// =======================================================
document.addEventListener('DOMContentLoaded', function () {

    // --- Global State for New Issue Files (Local to DOMContentLoaded) ---
    let newIssueFiles = [];

    // --- Reusable Function to Create a DataTransfer object ---
    const createDataTransfer = (files) => {
        const dataTransfer = new DataTransfer();
        if (Array.isArray(files)) {
            files.filter(f => f && f.size > 0).forEach(f => dataTransfer.items.add(f));
        }
        return dataTransfer;
    };

    // --- Function to Render New Issue Previews and Update File Input (Handles multiple files) ---
    const updateNewIssueFilesAndPreview = () => {
        const issueId = 'NewIssue';
        const fileInput = document.getElementById('NewIssueDrawings');
        const previewList = document.getElementById(`previewList_${issueId}`);
        const previewContainer = document.getElementById(`previewContainer_${issueId}`);
        const fileCountSpan = document.getElementById(`fileCount_${issueId}`);

        previewList.innerHTML = '';
        fileCountSpan.textContent = newIssueFiles.length;

        if (newIssueFiles.length > 0) {
            previewContainer.classList.remove('d-none');

            // 1. Render Previews and Delete Buttons
            newIssueFiles.forEach((file, index) => {
                const reader = new FileReader();
                reader.onload = function (e) {
                    const fileDiv = document.createElement('div');
                    fileDiv.className = 'd-flex flex-column align-items-center me-2 mb-2';

                    const img = document.createElement('img');
                    img.className = 'img-thumbnail';
                    img.src = e.target.result;
                    img.style.maxWidth = '75px';
                    img.style.maxHeight = '75px';
                    img.title = file.name;

                    const deleteBtn = document.createElement('button');
                    deleteBtn.textContent = 'מחק';
                    deleteBtn.className = 'btn btn-sm btn-danger mt-1 py-0 px-1';
                    deleteBtn.type = 'button';
                    deleteBtn.onclick = function () {
                        // Remove file from the central array and refresh
                        newIssueFiles.splice(index, 1);
                        updateNewIssueFilesAndPreview();
                    };

                    fileDiv.appendChild(img);
                    fileDiv.appendChild(deleteBtn);
                    previewList.appendChild(fileDiv);
                };
                reader.readAsDataURL(file);
            });

            // 2. Update the HIDDEN file input with the accumulated list
            fileInput.files = createDataTransfer(newIssueFiles).files;

        } else {
            previewContainer.classList.add('d-none');
            // Ensure the file input is completely empty if the list is empty
            fileInput.files = createDataTransfer([]).files;
        }
    };

    // --- Helper Function to Handle Single Image Preview, Setup, and Auto-Submit (For Existing Issues) ---
    const handleSingleFileSetup = (issueId, file) => {
        const previewContainer = document.getElementById(`previewContainer_${issueId}`);
        const previewImage = document.getElementById(`previewImage_${issueId}`);
        const uploadBtn = document.getElementById(`uploadBtn_${issueId}`);
        const fileInput = document.getElementById(`NewDrawing_${issueId}`);

        // 1. Get the form and the two non-button controls
        const form = document.getElementById(`issueForm_${issueId}`);
        const pasteBtn = document.getElementById(`pasteBtn_${issueId}`);
        const fileLabel = document.querySelector(`label[for="${fileInput.id}"]`);

        // 2. Show preview (for a brief moment while page reloads)
        previewImage.src = URL.createObjectURL(file);
        previewContainer.classList.remove('d-none');

        // 3. Hide all the primary action buttons/labels
        document.getElementById(`pasteBtn_${issueId}`).classList.add('d-none');
        fileLabel.classList.add('d-none');

        // HIDE "Upload" button (as we are auto-submitting)
        if (uploadBtn) {
            uploadBtn.classList.add('d-none');
        }

        // 4. Update the hidden file input with the new file (OVERWRITE)
        fileInput.files = createDataTransfer([file]).files;

        // 5. 🚨 CRITICAL FIX: Disable the buttons/labels NOW to prevent double-click BEFORE submit()
        if (pasteBtn) pasteBtn.disabled = true;
        if (fileLabel) fileLabel.style.pointerEvents = 'none';

        // 6. Auto-submit the form
        if (form) {
            form.submit();
        }
    };

    // --- Clearing Function (REUSED for Existing Issues) ---
    const clearSinglePreview = (issueId) => {
        const previewContainer = document.getElementById(`previewContainer_${issueId}`);
        const uploadBtn = document.getElementById(`uploadBtn_${issueId}`);
        const fileInput = document.getElementById(`NewDrawing_${issueId}`);

        // Reset File Input
        fileInput.files = createDataTransfer([]).files;

        // Hide preview and upload button
        previewContainer.classList.add('d-none');
        if (uploadBtn) {
            uploadBtn.classList.add('d-none');
        }

        // Show paste and choose file buttons
        document.getElementById(`pasteBtn_${issueId}`).classList.remove('d-none');
        document.querySelector(`label[for="${fileInput.id}"]`).classList.remove('d-none');
    };

    // --- Main Handler for new files (Select/Paste) ---

    //const MAX_FILE_SIZE = @ReportModel.maxFileSizeinMB; // 1 MB
    const handleNewFile = (issueId, file) => {
        if (file.size > MAX_FILE_SIZE * 1024 * 1024) {
            var sizeError = "שגיאה: גודל הקובץ חורג מהמותר (מקסימום " + MAX_FILE_SIZE + " MB)";
            alert(sizeError); // Error: File size exceeds the limit.
            return false;
        }
        if (issueId === 'NewIssue') {
            if (newIssueFiles.length >= 3) {
                alert(`שגיאה: לא ניתן להוסיף יותר מ-3 תמונות לנושא זה.`); // Error: Cannot add more than 3 images
                return false;
            }
            newIssueFiles.push(file);
            updateNewIssueFilesAndPreview();
            return true;
        } else {
            handleSingleFileSetup(issueId, file);
            return true;
        }
    }


    // --- 1. Attach Clear Listeners ---
    // For Existing Issues (Cancel button)
    document.querySelectorAll('.clear-preview').forEach(button => {
        button.addEventListener('click', function () {
            const issueId = this.getAttribute('data-issue-id');
            clearSinglePreview(issueId);
        });
    });
    // For New Issue (Clear ALL button)
    document.querySelectorAll('.clear-all-previews').forEach(button => {
        button.addEventListener('click', function () {
            newIssueFiles = [];
            updateNewIssueFilesAndPreview();
        });
    });

    // --- 2. Attach Change Listener for Manual File Selection ---
    document.querySelectorAll('input[type="file"]').forEach(input => {
        input.addEventListener('change', function () {
            const id = this.id;
            let issueId;

            if (id === 'NewIssueDrawings') {
                issueId = 'NewIssue';
            } else if (id.includes('_')) {
                issueId = id.split('_')[1];
            } else {
                return;
            }

            if (this.files.length > 0) {
                // Pass the selected file to the main handler
                handleNewFile(issueId, this.files[0]);
            }
        });
    });


    // --- 3. Attach Click Listener for Paste Button (The New Logic) ---
    document.querySelectorAll('.paste-trigger').forEach(button => {
        button.addEventListener('click', function (e) {
            e.preventDefault();
            const issueId = this.getAttribute('data-issue-id');

            if (activePasteButton === this) {
                // Click to CANCEL/RESET
                managePasteButtonState(this, false);
                window.currentPasteIssueId = null;
            } else {
                // Click to ACTIVATE paste mode
                managePasteButtonState(this, true);
                window.currentPasteIssueId = issueId;
            }
        });
    });

    // --- 4. Main Document Paste Listener ---
    document.addEventListener('paste', function (event) {
        if (!window.currentPasteIssueId) return;

        const items = (event.clipboardData || event.originalEvent.clipboardData).items;
        let imageItem = null;

        for (let i = 0; i < items.length; i++) {
            if (items[i].type.indexOf('image') !== -1) {
                imageItem = items[i];
                break;
            }
        }

        if (imageItem) {
            event.preventDefault();

            const blob = imageItem.getAsFile();
            if (blob) {
                const now = new Date();
                const fileName = `pasted_image_${now.getTime()}.png`;
                const file = new File([blob], fileName, { type: 'image/png' });

                // Check if file handling was successful
                if (handleNewFile(window.currentPasteIssueId, file)) {
                    // SUCCESS: Reset the button state immediately after successful handling
                    if (activePasteButton) {
                        managePasteButtonState(activePasteButton, false);
                    }
                }
                else {
                    // FAILURE: Reset the button state on error as well, so the user can try again.
                    if (activePasteButton) {
                        managePasteButtonState(activePasteButton, false);
                    }
                    alert(`אופס.... משהו השתבש. התמונה לא הועלתה`); // Something went wrong
                }
            }
        }

        window.currentPasteIssueId = null;
    });

    // --- 5. Manual Click Listener for File Dialog (Prevents Scroll Glitch) ---
    document.querySelectorAll('.file-label').forEach(label => {
        label.addEventListener('click', function (e) {
            e.preventDefault();

            const inputId = this.getAttribute('for');
            const fileInput = document.getElementById(inputId);
            if (fileInput) {
                fileInput.click();
            }
        });
    });
});
