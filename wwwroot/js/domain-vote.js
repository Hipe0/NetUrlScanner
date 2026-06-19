window.DomainVote = (function () {
    function renderSafeBrowsingBadge(status, threatType) {
        if (!status || status === 'Disabled') {
            return '<span class="badge bg-secondary">Chưa bật Safe Browsing</span>';
        }
        if (status === 'Clean') {
            return '<span class="badge bg-success"><i class="bi bi-shield-check me-1"></i>Google: An toàn</span>';
        }
        if (status === 'Threat') {
            const label = threatType ? threatType.replace(/_/g, ' ') : 'Nguy hiểm';
            return `<span class="badge bg-danger"><i class="bi bi-shield-exclamation me-1"></i>Google: ${label}</span>`;
        }
        return '<span class="badge bg-warning text-dark">Google: Không kiểm tra được</span>';
    }

    function renderCategory(category, tags) {
        if (!category) return '<span class="text-muted small">Chưa phân loại (trang không phản hồi HTML)</span>';
        let html = `<span class="badge bg-info text-dark me-1">${category}</span>`;
        if (tags) {
            tags.split(',').map(t => t.trim()).filter(Boolean).forEach(tag => {
                if (tag !== category) html += `<span class="badge bg-light text-dark border me-1">${tag}</span>`;
            });
        }
        return html;
    }

    function updateVoteUi(container, stats) {
        if (!container || !stats) return;
        const upBtn = container.querySelector('[data-vote="1"]');
        const downBtn = container.querySelector('[data-vote="-1"]');
        const upEl = container.querySelector('.vote-up-count');
        const downEl = container.querySelector('.vote-down-count');
        const netEl = container.querySelector('.vote-net-score');

        if (upEl) upEl.textContent = stats.upVotes ?? 0;
        if (downEl) downEl.textContent = stats.downVotes ?? 0;
        if (netEl) netEl.textContent = stats.netScore ?? 0;

        [upBtn, downBtn].forEach(btn => btn?.classList.remove('active', 'btn-success', 'btn-danger', 'btn-outline-success', 'btn-outline-danger'));
        if (upBtn) upBtn.classList.add(stats.userVote === 1 ? 'btn-success' : 'btn-outline-success');
        if (downBtn) downBtn.classList.add(stats.userVote === -1 ? 'btn-danger' : 'btn-outline-danger');
        if (stats.userVote === 1) upBtn?.classList.add('active');
        if (stats.userVote === -1) downBtn?.classList.add('active');
    }

    function bind(container) {
        if (!container || container.dataset.bound === '1') return;
        container.dataset.bound = '1';

        container.querySelectorAll('[data-vote]').forEach(btn => {
            btn.addEventListener('click', async function () {
                const domain = container.dataset.domain;
                const vote = parseInt(this.dataset.vote, 10);
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                const formData = new FormData();
                if (token) formData.append('__RequestVerificationToken', token);
                formData.append('domain', domain);
                formData.append('vote', vote);

                try {
                    const res = await fetch('/Domain/Vote', { method: 'POST', body: formData });
                    const json = await res.json();
                    if (json.success) {
                        updateVoteUi(container, {
                            upVotes: json.data.upVotes,
                            downVotes: json.data.downVotes,
                            netScore: json.data.netScore,
                            userVote: json.data.userVote
                        });
                        window.showToast?.(json.message, 'success');
                    } else {
                        window.showToast?.(json.message || 'Không thể bình chọn', 'danger');
                    }
                } catch {
                    window.showToast?.('Lỗi kết nối server', 'danger');
                }
            });
        });
    }

    return { renderSafeBrowsingBadge, renderCategory, updateVoteUi, bind };
})();
