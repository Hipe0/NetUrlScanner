(function () {
    'use strict';

    const FALLBACK_BANKS = [
        { bin: '970422', shortName: 'MBBank', name: 'Ngân hàng Quân đội' },
        { bin: '970436', shortName: 'Vietcombank', name: 'Ngân hàng Ngoại thương Việt Nam' },
        { bin: '970415', shortName: 'VietinBank', name: 'Ngân hàng Công thương Việt Nam' },
        { bin: '970418', shortName: 'BIDV', name: 'Ngân hàng Đầu tư và Phát triển Việt Nam' },
        { bin: '970407', shortName: 'Techcombank', name: 'Ngân hàng Kỹ thương Việt Nam' },
        { bin: '970416', shortName: 'ACB', name: 'Ngân hàng Á Châu' }
    ];

    let banksPromise = null;

    function fetchBanks() {
        if (!banksPromise) {
            banksPromise = fetch('/api/v1/banks')
                .then(res => res.ok ? res.json() : [])
                .then(data => (data && data.length > 0 ? data : FALLBACK_BANKS))
                .catch(() => FALLBACK_BANKS);
        }
        return banksPromise;
    }

    function toggleCustomBank(select, customInput, show) {
        if (!customInput) return;
        customInput.classList.toggle('d-none', !show);
        customInput.required = show;
        if (!show) customInput.value = '';
    }

    window.BankSelect = {
        init(options) {
            const select = document.getElementById(options.selectId);
            const customInput = options.customInputId
                ? document.getElementById(options.customInputId)
                : null;

            if (!select) return;

            const selectedId = options.selectedBankId || '';
            const otherValue = options.otherCustomValue || '';

            fetchBanks().then(banks => {
                const fragment = document.createDocumentFragment();
                banks.forEach(bank => {
                    const option = document.createElement('option');
                    option.value = bank.bin;
                    option.textContent = `[${bank.shortName}] ${bank.name}`;
                    if (bank.bin === selectedId) option.selected = true;
                    fragment.appendChild(option);
                });
                select.appendChild(fragment);

                if (selectedId === 'OTHER') {
                    select.value = 'OTHER';
                    if (customInput) {
                        toggleCustomBank(select, customInput, true);
                        customInput.value = otherValue;
                    }
                } else if (selectedId && !banks.some(b => b.bin === selectedId)) {
                    select.value = 'OTHER';
                    if (customInput) {
                        toggleCustomBank(select, customInput, true);
                        customInput.value = selectedId;
                    }
                }
            });

            select.addEventListener('change', () => {
                const isOther = select.value === 'OTHER';
                toggleCustomBank(select, customInput, isOther);
                if (typeof options.onOtherChange === 'function') {
                    options.onOtherChange(isOther);
                }
            });
        }
    };
})();
