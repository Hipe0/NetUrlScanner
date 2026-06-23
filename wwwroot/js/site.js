// Toast notification manager
window.showToast = function (message, type = 'success') {
    const container = document.querySelector('.toast-container');
    if (!container) {
        console.warn('Toast container not found in DOM.');
        return;
    }

    const toastId = 'toast-' + Date.now();
    const typeClass = 'toast-' + type;

    let iconClass = 'bi-info-circle-fill';
    if (type === 'success') iconClass = 'bi-check-circle-fill';
    else if (type === 'error' || type === 'danger') iconClass = 'bi-exclamation-triangle-fill';
    else if (type === 'warning') iconClass = 'bi-exclamation-circle-fill';

    const toastHtml = `
        <div id="${toastId}" class="toast ${typeClass} align-items-center border-0" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="4000">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center gap-2">
                    <i class="bi ${iconClass} fs-5"></i>
                    <div class="toast-message"></div>
                </div>
                <button type="button" class="btn-close btn-close-white m-auto me-3" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', toastHtml);
    const toastEl = document.getElementById(toastId);
    toastEl.querySelector('.toast-message').textContent = message;

    if (window.bootstrap && window.bootstrap.Toast) {
        const toast = new bootstrap.Toast(toastEl);
        toast.show();
        toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
    } else {
        toastEl.style.display = 'block';
        toastEl.style.opacity = '1';
        setTimeout(() => toastEl.remove(), 4000);
    }
};

function initThemeToggle() {
    const toggleBtn = document.getElementById('themeToggle');
    if (!toggleBtn) return;

    const icon = toggleBtn.querySelector('i');
    const currentTheme = document.documentElement.getAttribute('data-bs-theme');

    if (currentTheme === 'dark') {
        icon?.classList.replace('bi-moon-fill', 'bi-sun-fill');
    }

    toggleBtn.addEventListener('click', () => {
        const html = document.documentElement;
        const isDark = html.getAttribute('data-bs-theme') === 'dark';
        const newTheme = isDark ? 'light' : 'dark';

        html.setAttribute('data-bs-theme', newTheme);
        localStorage.setItem('theme', newTheme);

        if (icon) {
            icon.classList.toggle('bi-moon-fill', newTheme === 'light');
            icon.classList.toggle('bi-sun-fill', newTheme === 'dark');
        }
    });
}

function initFlashToasts() {
    // TempData Success/Error từ server — đọc data-flash-* trên body (tiếng Việt UTF-8)
    const body = document.body;
    const success = body?.dataset.flashSuccess;
    const error = body?.dataset.flashError;

    if (success) window.showToast(success, 'success');
    if (error) window.showToast(error, 'danger');
}

document.addEventListener('DOMContentLoaded', () => {
    initThemeToggle();
    initFlashToasts();
});
