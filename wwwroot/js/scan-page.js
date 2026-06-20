(function () {
    let ajaxMapObj = null;
    let scanData = null;

    function initAjaxMap(lat, lon, ip, country) {
        const mapContainer = document.getElementById('ajaxMap');
        if (!mapContainer || typeof L === 'undefined') return;

        if (!lat || !lon) {
            mapContainer.parentElement.classList.add('d-none');
            return;
        }
        mapContainer.parentElement.classList.remove('d-none');

        if (ajaxMapObj) {
            ajaxMapObj.remove();
            ajaxMapObj = null;
        }

        ajaxMapObj = L.map('ajaxMap').setView([lat, lon], 12);

        const currentTheme = document.documentElement.getAttribute('data-bs-theme');
        const tileUrl = currentTheme === 'dark'
            ? 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png'
            : 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png';

        L.tileLayer(tileUrl, {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(ajaxMapObj);

        L.marker([lat, lon]).addTo(ajaxMapObj)
            .bindPopup(`<b>IP:</b> ${ip}<br><b>Vị trí:</b> ${country}`)
            .openPopup();
    }



    function animateGauge(score, level) {
        const circle = document.getElementById('ajaxGaugeCircle');
        const valText = document.getElementById('ajaxGaugeVal');
        const labelText = document.getElementById('ajaxGaugeLabel');
        if (!circle || !valText) return;

        circle.className = 'gauge-fill';
        if (level === 'Safe') circle.classList.add('gauge-safe');
        else if (level === 'Warning') circle.classList.add('gauge-warning');
        else circle.classList.add('gauge-danger');

        if (labelText) {
            labelText.textContent = level === 'Safe' ? 'An toàn' :
                level === 'Warning' ? 'Cảnh báo' : 'Nguy hiểm';
        }

        const r = 45;
        const circumference = 2 * Math.PI * r;
        circle.style.strokeDasharray = circumference;
        circle.style.strokeDashoffset = circumference;

        setTimeout(() => {
            circle.style.strokeDashoffset = circumference - (score / 100) * circumference;
        }, 50);

        let startTimestamp = null;
        function step(timestamp) {
            if (!startTimestamp) startTimestamp = timestamp;
            const progress = Math.min((timestamp - startTimestamp) / 1000, 1);
            valText.textContent = Math.round(progress * (2 - progress) * score);
            if (progress < 1) window.requestAnimationFrame(step);
        }
        window.requestAnimationFrame(step);
    }

    function formatScannedAt(value) {
        if (!value) return '-';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return value;

        const pad = n => String(n).padStart(2, '0');
        return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    }

    function mapApiScanData(data) {
        const geo = data.geolocation || {};
        const votes = data.domainVotes || {};
        return {
            id: data.id,
            url: data.url,
            normalizedDomain: data.normalizedDomain,
            status: data.status,
            statusCode: data.statusCode,
            responseTimeMs: data.responseTimeMs,
            isHttps: data.isHttps,
            riskScore: data.riskScore,
            riskLevel: data.riskLevel,
            reasons: data.reasons || [],
            scannedAt: formatScannedAt(data.scannedAt),
            ipAddress: geo.ipAddress,
            countryName: geo.countryName,
            countryCode: geo.countryCode,
            city: geo.city,
            isp: geo.isp,
            latitude: geo.latitude,
            longitude: geo.longitude,
            siteCategory: data.siteCategory,
            siteCategoryTags: data.siteCategoryTags,
            safeBrowsingStatus: data.safeBrowsingStatus,
            safeBrowsingThreatType: data.safeBrowsingThreatType,
            domainUpVotes: votes.upVotes ?? 0,
            domainDownVotes: votes.downVotes ?? 0,
            domainNetScore: votes.netScore ?? 0,
            userDomainVote: votes.userVote
        };
    }

    function renderAjaxDomainVote(data) {
        const container = document.getElementById('ajaxDomainVote');
        if (!container || !window.DomainVote) return;

        const domain = data.normalizedDomain || data.url;
        container.dataset.domain = domain;
        const isAuth = document.querySelector('#scanForm') != null; // guest can still see counts from API

        const upActive = data.userDomainVote === 1;
        const downActive = data.userDomainVote === -1;

        container.innerHTML = `
            <div class="domain-vote-widget p-3 rounded border bg-body" data-domain="${domain}">
                <h6 class="fw-bold mb-2"><i class="bi bi-hand-thumbs-up me-2"></i>Cộng đồng đánh giá domain</h6>
                <p class="small text-muted mb-2">${domain}</p>
                <div class="d-flex align-items-center gap-2 flex-wrap">
                    <button type="button" class="btn btn-sm ${upActive ? 'btn-success' : 'btn-outline-success'}" data-vote="1">
                        <i class="bi bi-hand-thumbs-up"></i> <span class="vote-up-count">${data.domainUpVotes ?? 0}</span>
                    </button>
                    <button type="button" class="btn btn-sm ${downActive ? 'btn-danger' : 'btn-outline-danger'}" data-vote="-1">
                        <i class="bi bi-hand-thumbs-down"></i> <span class="vote-down-count">${data.domainDownVotes ?? 0}</span>
                    </button>
                    <span class="small ms-1">Điểm: <strong class="vote-net-score">${data.domainNetScore ?? 0}</strong></span>
                </div>
            </div>`;

        const widget = container.querySelector('.domain-vote-widget');
        if (widget) window.DomainVote.bind(widget);
    }

    function updateStatCards(status, riskLevel) {
        const bump = id => {
            const card = document.getElementById(id)?.closest('.stat-card');
            if (!card) return;
            card.classList.remove('stat-bump');
            void card.offsetWidth;
            card.classList.add('stat-bump');
        };

        const inc = id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.textContent = String((parseInt(el.textContent, 10) || 0) + 1);
            bump(id);
        };

        inc('statTotal');
        if (status === 'Online') inc('statOnline');
        if (riskLevel === 'Warning') inc('statWarning');
        if (riskLevel === 'Suspicious') inc('statSuspicious');
    }

    function prependToHistoryTable(data) {
        const tbody = document.querySelector('table.table tbody');
        if (!tbody) return;

        const riskBadgeClass = data.riskLevel === 'Safe' ? 'bg-success' :
            data.riskLevel === 'Warning' ? 'bg-warning text-dark' : 'bg-danger';

        let statusClass = 'text-muted';
        if (data.status === 'Online') statusClass = 'text-success fw-semibold';
        else if (data.status === 'Redirect') statusClass = 'text-primary fw-semibold';
        else if (data.status === 'Client Error') statusClass = 'text-warning fw-semibold';
        else if (data.status === 'Server Error' || data.status === 'Offline') statusClass = 'text-danger fw-semibold';

        const tokenValue = document.querySelector('#scanForm input[name="__RequestVerificationToken"]')?.value || '';

        tbody.insertAdjacentHTML('afterbegin', `
            <tr class="fade-in-row">
                <td class="text-break-all" style="max-width:520px;">${data.url}</td>
                <td class="${statusClass}">${data.status}</td>
                <td>${data.statusCode ?? '-'}</td>
                <td>${data.responseTimeMs} ms</td>
                <td><span class="badge ${riskBadgeClass}">${data.riskLevel} (${data.riskScore})</span></td>
                <td>${data.scannedAt}</td>
                <td>
                    <a href="/Scan/Details/${data.id}" class="btn btn-sm btn-outline-primary me-1">Chi tiết</a>
                    <form action="/Scan/Delete/${data.id}" method="post" class="d-inline" onsubmit="return confirm('Bạn có chắc muốn xóa kết quả này?');">
                        <input type="hidden" name="__RequestVerificationToken" value="${tokenValue}" />
                        <button type="submit" class="btn btn-sm btn-outline-danger">Xóa</button>
                    </form>
                </td>
            </tr>`);

        const rows = tbody.querySelectorAll('tr');
        if (rows.length > 20) rows[rows.length - 1].remove();
    }

    document.addEventListener('DOMContentLoaded', function () {
        const form = document.getElementById('scanForm');
        const btn = document.getElementById('scanButton');
        const urlInput = document.getElementById('urlInput');
        if (!form || !btn || !urlInput) return;

        document.getElementById('themeToggle')?.addEventListener('click', () => {
            setTimeout(() => {
                if (ajaxMapObj && scanData?.latitude && scanData?.longitude) {
                    initAjaxMap(scanData.latitude, scanData.longitude, scanData.ipAddress, scanData.countryName);
                }
            }, 100);
        });

        function startScan() {
            if (btn.classList.contains('disabled')) return;

            const url = urlInput.value.trim();
            if (!url) return;

            const spinner = document.getElementById('buttonSpinner');
            const text = document.getElementById('buttonText');
            const loadingSection = document.getElementById('loadingSection');
            const ajaxResultCard = document.getElementById('ajaxResultCard');
            const scanningUrlText = document.getElementById('scanningUrlText');

            btn.classList.add('disabled');
            spinner?.classList.remove('d-none');
            if (text) text.innerText = 'Đang quét...';

            scanningUrlText.textContent = url;
            loadingSection.classList.remove('d-none');
            ajaxResultCard.classList.add('d-none');
            ajaxResultCard.classList.remove('slide-down-enter-active');

            const steps = ['step-1', 'step-2', 'step-3', 'step-4', 'step-5'];
            steps.forEach(id => {
                const el = document.getElementById(id);
                if (el) {
                    el.className = id === 'step-5' ? 'loading-step mb-0 text-muted' : 'loading-step mb-3 text-muted';
                    el.querySelector('i').className = 'bi bi-circle me-2';
                }
            });

            let scanCompleted = false;
            scanData = null;
            let stepTimers = [];

            function clearTimers() {
                stepTimers.forEach(t => clearTimeout(t));
                stepTimers = [];
            }

            function resetButtons() {
                btn.classList.remove('disabled');
                spinner?.classList.add('d-none');
                if (text) text.innerText = 'Scan';
            }

            function showResults(data) {
                resetButtons();
                urlInput.value = '';
                window.showToast?.('Phân tích URL hoàn tất!', 'success');

                document.getElementById('resultDetailLink').href = `/Scan/Details/${data.id}`;

                const statusText = document.getElementById('ajaxStatusText');
                statusText.textContent = data.status;
                statusText.className = 'fw-bold';
                if (data.status === 'Online') statusText.classList.add('text-success');
                else if (data.status === 'Redirect') statusText.classList.add('text-primary');
                else if (data.status === 'Client Error') statusText.classList.add('text-warning');
                else statusText.classList.add('text-danger');

                document.getElementById('ajaxStatusCodeText').textContent = data.statusCode ?? '-';
                document.getElementById('ajaxResponseTimeText').textContent = `${data.responseTimeMs} ms`;

                const httpsText = document.getElementById('ajaxHttpsText');
                httpsText.textContent = data.isHttps ? 'Có' : 'Không';
                httpsText.className = data.isHttps ? 'badge bg-success' : 'badge bg-danger';

                document.getElementById('ajaxIpText').textContent = data.ipAddress || '-';
                document.getElementById('ajaxIspText').textContent = data.isp || '-';

                const locationText = document.getElementById('ajaxLocationText');
                if (data.countryName && data.countryName !== 'Unknown') {
                    let flag = '';
                    if (data.countryCode?.length === 2) {
                        flag = String.fromCodePoint(...data.countryCode.toUpperCase().split('').map(c => 127397 + c.charCodeAt(0))) + ' ';
                    }
                    locationText.textContent = `${flag}${data.city}, ${data.countryName}`;
                } else {
                    locationText.textContent = 'Không xác định vị trí máy chủ';
                }

                const reasonsList = document.getElementById('ajaxReasonsList');
                reasonsList.innerHTML = '';
                if (data.reasons?.length) {
                    data.reasons.forEach(reason => {
                        const isDanger = /cá cược|cờ bạc|mất tài sản|lừa đảo/.test(reason);
                        let iconClass = 'badge bg-warning text-dark mt-1';
                        if (isDanger) iconClass = 'badge bg-danger mt-1';
                        else if (data.riskLevel === 'Safe') iconClass = 'badge bg-success mt-1';

                        const li = document.createElement('li');
                        li.className = 'list-group-item d-flex align-items-start gap-2 px-0 bg-transparent py-1 border-0';
                        li.innerHTML = `<span class="${iconClass}">!</span> <span class="text-body">${reason}</span>`;
                        reasonsList.appendChild(li);
                    });
                } else {
                    reasonsList.innerHTML = '<div class="text-muted text-center py-2">Không phát hiện dấu hiệu bất thường.</div>';
                }

                const riskBadge = document.getElementById('ajaxRiskBadge');
                riskBadge.textContent = `${data.riskLevel} (${data.riskScore}/100)`;
                riskBadge.className = 'badge px-3 py-2 fs-6';
                if (data.riskLevel === 'Safe') riskBadge.classList.add('bg-success');
                else if (data.riskLevel === 'Warning') riskBadge.classList.add('bg-warning', 'text-dark');
                else riskBadge.classList.add('bg-danger');

                const catEl = document.getElementById('ajaxCategoryBadges');
                if (catEl && window.DomainVote) {
                    catEl.innerHTML = window.DomainVote.renderCategory(data.siteCategory, data.siteCategoryTags);
                }
                const sbEl = document.getElementById('ajaxSafeBrowsingBadge');
                if (sbEl && window.DomainVote) {
                    sbEl.innerHTML = window.DomainVote.renderSafeBrowsingBadge(data.safeBrowsingStatus, data.safeBrowsingThreatType);
                }
                renderAjaxDomainVote(data);

                ajaxResultCard.classList.remove('d-none');
                void ajaxResultCard.offsetWidth;
                ajaxResultCard.classList.add('slide-down-enter-active');
                ajaxResultCard.scrollIntoView({ behavior: 'smooth', block: 'start' });

                animateGauge(data.riskScore, data.riskLevel);
                initAjaxMap(data.latitude, data.longitude, data.ipAddress, data.countryName);
                updateStatCards(data.status, data.riskLevel);
                prependToHistoryTable(data);
            }

            function runStep(index) {
                if (scanCompleted && scanData) {
                    for (let i = index; i < steps.length; i++) {
                        const stepEl = document.getElementById(steps[i]);
                        if (stepEl) {
                            stepEl.className = i === 4 ? 'loading-step mb-0 completed' : 'loading-step mb-3 completed';
                            stepEl.querySelector('i').className = 'bi bi-check-circle-fill me-2';
                        }
                    }
                    setTimeout(() => {
                        loadingSection.classList.add('d-none');
                        showResults(scanData);
                    }, 300);
                    return;
                }

                const stepEl = document.getElementById(steps[index]);
                if (stepEl) {
                    stepEl.className = index === 4 ? 'loading-step mb-0 active' : 'loading-step mb-3 active';
                    stepEl.querySelector('i').className = 'bi bi-arrow-repeat me-2';
                }

                if (index < steps.length - 1) {
                    stepTimers.push(setTimeout(() => {
                        if (stepEl) {
                            stepEl.className = 'loading-step mb-3 completed';
                            stepEl.querySelector('i').className = 'bi bi-check-circle-fill me-2';
                        }
                        runStep(index + 1);
                    }, 350));
                } else {
                    stepTimers.push(setTimeout(() => {
                        if (stepEl) {
                            stepEl.className = 'loading-step mb-0 completed';
                            stepEl.querySelector('i').className = 'bi bi-check-circle-fill me-2';
                        }
                        if (scanCompleted && scanData) {
                            setTimeout(() => {
                                loadingSection.classList.add('d-none');
                                showResults(scanData);
                            }, 300);
                        } else {
                            scanCompleted = true;
                        }
                    }, 350));
                }
            }

            runStep(0);

            fetch('/api/v1/scans', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ url, saveToHistory: true })
            })
                .then(r => r.json())
                .then(result => {
                    if (result.success && result.data) {
                        const mapped = mapApiScanData(result.data);
                        if (scanCompleted) {
                            loadingSection.classList.add('d-none');
                            showResults(mapped);
                        } else {
                            scanCompleted = true;
                            scanData = mapped;
                        }
                    } else {
                        clearTimers();
                        loadingSection.classList.add('d-none');
                        resetButtons();
                        window.showToast?.(result.message || 'Đã xảy ra lỗi.', 'danger');
                    }
                })
                .catch(() => {
                    clearTimers();
                    loadingSection.classList.add('d-none');
                    resetButtons();
                    window.showToast?.('Lỗi mạng hoặc máy chủ không phản hồi.', 'danger');
                });
        }

        btn.addEventListener('click', startScan);
        urlInput.addEventListener('keydown', e => {
            if (e.key === 'Enter') {
                e.preventDefault();
                startScan();
            }
        });
    });
})();
