//
// Add listener to all form's submit buttons and disable them until page is reloaded
//
// This function runs once the entire page content is loaded

document.addEventListener('DOMContentLoaded', function () {

    const forms = document.querySelectorAll('form');

    forms.forEach(form => {
        // Only apply this logic to forms that perform a modification (POST method)
        if (form.method.toLowerCase() === 'post') {

            form.addEventListener('submit', function (e) {
                // We must check if the form is valid using the jQuery Validation plugin.
                // If it is invalid, we stop the spinner logic.
                if (typeof jQuery !== 'undefined' && typeof $.validator !== 'undefined') {
                    const $form = $(this);
                    // If the form is NOT valid, the validation framework has already taken over (or is about to).
                    // We must NOT show the spinner in this case.
                    if ($form.valid() === false) {
                        // The validation framework will display the error messages (e.g., "The field is required").
                        // We immediately exit this submit handler.
                        return;
                    }
                }

                // If validation passed OR validation scripts aren't loaded, proceed to disable button
                const submitButtons = this.querySelectorAll('button[type="submit"], input[type="submit"]');

                submitButtons.forEach(button => {
                    // Check if the button is not already disabled
                    if (!button.disabled) {
                        button.disabled = true;

                        // Optional: Provide visual feedback
                        const originalText = button.textContent;

                        // Set a temporary attribute to hold the original text
                        button.setAttribute('data-original-text', originalText);

                        button.textContent = 'שולח...';

                        // Add a simple Bootstrap loading spinner
                        button.insertAdjacentHTML('beforeend',
                            ' <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>');
                    }
                });
            });
        }
    });
});
