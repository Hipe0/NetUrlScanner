(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        const typeUrl = document.getElementById('typeUrl');
        const typeBank = document.getElementById('typeBank');
        const urlSection = document.getElementById('urlSection');
        const bankSection = document.getElementById('bankSection');
        const bankSelect = document.getElementById('bankSelect');
        const btnLookup = document.getElementById('btnLookup');
        const accountNumber = document.getElementById('accountNumber');
        const accountOwnerName = document.getElementById('accountOwnerName');

        if (!typeUrl || !typeBank) return;

        function toggleSections() {
            const bankMode = typeBank.checked;
            urlSection.hidden = bankMode;
            bankSection.hidden = !bankMode;
        }

        typeUrl.addEventListener('change', toggleSections);
        typeBank.addEventListener('change', toggleSections);
        toggleSections();

        if (window.BankSelect && bankSelect) {
            window.BankSelect.init({
                selectId: 'bankSelect',
                customInputId: 'customBankInput',
                onOtherChange(isOther) {
                    if (!accountOwnerName) return;
                    if (isOther) accountOwnerName.removeAttribute('readonly');
                    else accountOwnerName.setAttribute('readonly', 'true');
                }
            });
        }

        btnLookup?.addEventListener('click', () => {
            const bankId = bankSelect?.value;
            const accNo = accountNumber?.value.trim() || '';

            if (!bankId || !accNo) {
                alert('Vui lòng chọn Ngân hàng và nhập Số tài khoản trước khi kiểm tra!');
                return;
            }

            if (bankId === 'OTHER') {
                alert('Không thể tự động tra cứu tên chủ tài khoản đối với ngân hàng "Khác". Vui lòng tự nhập.');
                return;
            }

            btnLookup.disabled = true;
            btnLookup.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Đang tải...';
            if (accountOwnerName) accountOwnerName.value = '';

            fetch(`/api/v1/banks/lookup?bankId=${encodeURIComponent(bankId)}&accountNumber=${encodeURIComponent(accNo)}`)
                .then(res => {
                    if (!res.ok) throw new Error('Not found');
                    return res.json();
                })
                .then(data => {
                    if (data?.accountName && accountOwnerName) {
                        accountOwnerName.value = data.accountName;
                    }
                })
                .catch(() => alert('Không tìm thấy thông tin tài khoản hoặc có lỗi xảy ra.'))
                .finally(() => {
                    btnLookup.disabled = false;
                    btnLookup.textContent = 'Tra tên TK';
                });
        });
    });
})();
