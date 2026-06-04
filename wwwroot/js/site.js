// Toast notification manager
window.showToast = function (message, type = 'success') {
    const container = document.querySelector('.toast-container');
    if (!container) {
        console.warn('Toast container not found in DOM.');
        return;
    }

    const toastId = 'toast-' + Date.now();
    const typeClass = 'toast-' + type; // toast-success, toast-danger, toast-warning, toast-info
    
    // Choose icon based on type
    let iconClass = 'bi-info-circle-fill';
    if (type === 'success') iconClass = 'bi-check-circle-fill';
    else if (type === 'error' || type === 'danger') iconClass = 'bi-exclamation-triangle-fill';
    else if (type === 'warning') iconClass = 'bi-exclamation-circle-fill';

    const toastHtml = `
        <div id="${toastId}" class="toast ${typeClass} align-items-center border-0" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="4000">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center gap-2">
                    <i class="bi ${iconClass} fs-5"></i>
                    <div>${message}</div>
                </div>
                <button type="button" class="btn-close btn-close-white m-auto me-3" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', toastHtml);
    const toastEl = document.getElementById(toastId);
    
    // Initialize with Bootstrap
    if (window.bootstrap && window.bootstrap.Toast) {
        const toast = new bootstrap.Toast(toastEl);
        toast.show();

        // Clean up DOM after hide
        toastEl.addEventListener('hidden.bs.toast', () => {
            toastEl.remove();
        });
    } else {
        // Fallback if bootstrap is not loaded yet
        toastEl.style.display = 'block';
        toastEl.style.opacity = '1';
        setTimeout(() => {
            toastEl.remove();
        }, 4000);
    }
};

