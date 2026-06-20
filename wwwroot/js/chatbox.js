(function () {
    'use strict';

    let markedReady = null;

    function loadMarked() {
        if (window.marked) return Promise.resolve(window.marked);
        if (markedReady) return markedReady;

        markedReady = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/marked@12.0.2/marked.min.js';
            script.async = true;
            script.onload = () => resolve(window.marked);
            script.onerror = () => reject(new Error('marked load failed'));
            document.head.appendChild(script);
        });

        return markedReady;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function renderBotMessage(text) {
        return loadMarked()
            .then(marked => {
                if (marked?.parse) return marked.parse(text, { breaks: true });
                return escapeHtml(text);
            })
            .catch(() => escapeHtml(text));
    }

    document.addEventListener('DOMContentLoaded', () => {
        const chatBtn = document.getElementById('ai-chat-btn');
        const chatWindow = document.getElementById('ai-chat-window');
        const closeBtn = document.getElementById('ai-chat-close');
        const sendBtn = document.getElementById('ai-chat-send');
        const chatInput = document.getElementById('ai-chat-input');
        const messagesArea = document.getElementById('ai-chat-messages');
        const typingIndicator = document.getElementById('ai-typing');

        if (!chatBtn || !chatWindow) return;

        chatBtn.addEventListener('click', () => {
            const open = chatWindow.hidden;
            chatWindow.hidden = !open;
            chatBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
            if (open) {
                chatInput?.focus();
                loadMarked().catch(() => {});
            }
        });

        closeBtn?.addEventListener('click', () => {
            chatWindow.hidden = true;
            chatBtn.setAttribute('aria-expanded', 'false');
        });

        const appendMessage = async (text, sender) => {
            const msgDiv = document.createElement('div');
            msgDiv.className = `chat-msg ${sender}`;
            if (sender === 'bot') {
                msgDiv.innerHTML = await renderBotMessage(text);
            } else {
                msgDiv.textContent = text;
            }
            messagesArea.appendChild(msgDiv);
            messagesArea.scrollTop = messagesArea.scrollHeight;
        };

        const sendMessage = async () => {
            const message = chatInput.value.trim();
            if (!message) return;

            chatInput.value = '';
            sendBtn.disabled = true;
            appendMessage(message, 'user');

            typingIndicator.hidden = false;

            try {
                const response = await fetch('/api/chat', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message })
                });

                if (response.ok) {
                    const data = await response.json();
                    await appendMessage(data.message, 'bot');
                } else {
                    const err = await response.json().catch(() => ({}));
                    await appendMessage('Lỗi: ' + (err.message || 'Đã có lỗi xảy ra.'), 'bot');
                }
            } catch {
                await appendMessage('Lỗi kết nối tới máy chủ.', 'bot');
            } finally {
                typingIndicator.hidden = true;
                sendBtn.disabled = false;
                chatInput.focus();
            }
        };

        sendBtn?.addEventListener('click', sendMessage);
        chatInput?.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
    });
})();
