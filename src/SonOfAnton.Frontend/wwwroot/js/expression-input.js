// ─── Expression Input JS Interop ─────────────────────────────
// contenteditable div üzerinde caret yönetimi, syntax highlighting
// ve autocomplete popup konumlama

window.ExpressionInputInterop = {

    // Editöre odaklan
    focus: function (el) {
        if (el) el.focus();
    },

    // Editörün metnini al
    getText: function (el) {
        return el ? (el.innerText || '') : '';
    },

    // Editöre metin yaz ve caret'i sona getir
    setText: function (el, text) {
        if (!el) return;
        el.innerText = text;
        this._moveCaretToEnd(el);
    },

    // Caret pozisyonunu al
    getCaretPosition: function (el) {
        if (!el) return 0;
        const sel = window.getSelection();
        if (!sel.rangeCount) return 0;
        const range = sel.getRangeAt(0);
        const preRange = range.cloneRange();
        preRange.selectNodeContents(el);
        preRange.setEnd(range.startContainer, range.startOffset);
        return preRange.toString().length;
    },

    // Caret pozisyonunu ayarla
    setCaretPosition: function (el, pos) {
        if (!el) return;
        const range = document.createRange();
        const sel = window.getSelection();

        let charCount = 0;
        let found = false;

        function walk(node) {
            if (found) return;
            if (node.nodeType === Node.TEXT_NODE) {
                const len = node.textContent.length;
                if (charCount + len >= pos) {
                    range.setStart(node, pos - charCount);
                    range.collapse(true);
                    found = true;
                }
                charCount += len;
            } else {
                for (let child of node.childNodes) {
                    walk(child);
                    if (found) break;
                }
            }
        }

        walk(el);
        if (found) {
            sel.removeAllRanges();
            sel.addRange(range);
        }
    },

    // Caret'in piksel konumunu al (popup konumlama için)
    getCaretPixelPosition: function (el) {
        const sel = window.getSelection();
        if (!sel.rangeCount) return { x: 0, y: 0 };
        const range = sel.getRangeAt(0);
        const rect = range.getBoundingClientRect();
        const elRect = el.getBoundingClientRect();
        return {
            x: rect.left - elRect.left,
            y: rect.bottom - elRect.top
        };
    },

    // Seçili metnin etrafına metin ekle
    insertAtCaret: function (el, text) {
        if (!el) return;
        el.focus();
        const sel = window.getSelection();
        if (!sel.rangeCount) return;
        const range = sel.getRangeAt(0);
        range.deleteContents();
        const textNode = document.createTextNode(text);
        range.insertNode(textNode);
        range.setStartAfter(textNode);
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);
    },

    // Syntax highlighting uygula
    applySyntaxHighlight: function (el) {
        if (!el) return;
        const text = el.innerText || '';
        const pos = this.getCaretPosition(el);

        // {{...}} blokları bul ve renklendirme uygula
        const highlighted = text.replace(
            /\{\{(.+?)\}\}/g,
            (match, inner) => {
                const coloredInner = this._highlightExpression(inner);
                return '<span class="expr-bracket">{{</span>' +
                    '<span class="expr-body">' + coloredInner + '</span>' +
                    '<span class="expr-bracket">}}</span>';
            }
        );

        // Sadece farklıysa güncelle (flicker önleme)
        if (el.innerHTML !== highlighted) {
            el.innerHTML = highlighted;
            this.setCaretPosition(el, pos);
        }
    },

    // İfade içeriğini renklendirme
    _highlightExpression: function (expr) {
        // String literaller
        let result = expr.replace(
            /"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'/g,
            '<span class="expr-string">$&</span>'
        );
        // Sayılar (bağımsız)
        result = result.replace(
            /(?<![a-zA-Z_$])(\d+\.?\d*)(?![a-zA-Z_$])/g,
            '<span class="expr-number">$1</span>'
        );
        // Fonksiyon adları (bir ( takip eden identifier)
        result = result.replace(
            /([a-zA-Z_]\w*)\s*(?=\()/g,
            '<span class="expr-func">$1</span>'
        );
        // Değişken referansları ($ ile başlayan)
        result = result.replace(
            /(\$[a-zA-Z_][\w.]*)/g,
            '<span class="expr-var">$1</span>'
        );
        return result;
    },

    // Caret'i sona taşı
    _moveCaretToEnd: function (el) {
        const range = document.createRange();
        const sel = window.getSelection();
        range.selectNodeContents(el);
        range.collapse(false);
        sel.removeAllRanges();
        sel.addRange(range);
    },

    // Debounced input handler kur
    setupInputHandler: function (el, dotnetRef) {
        if (!el || !dotnetRef) return;

        let debounceTimer = null;

        el.addEventListener('focus', () => {
            dotnetRef.invokeMethodAsync('OnJsFocus');
        });

        el.addEventListener('blur', () => {
            dotnetRef.invokeMethodAsync('OnJsBlur');
        });

        el.addEventListener('input', () => {
            // Syntax highlight
            this.applySyntaxHighlight(el);

            // Debounced Blazor callback
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                const text = el.innerText || '';
                const caretPos = this.getCaretPosition(el);
                dotnetRef.invokeMethodAsync('OnJsInput', text, caretPos);
            }, 150);
        });

        el.addEventListener('keydown', (e) => {
            // Autocomplete popup kontrolü
            dotnetRef.invokeMethodAsync('OnJsKeyDown', e.key, e.ctrlKey, e.shiftKey);

            // Tab ile autocomplete seçimi
            if (e.key === 'Tab') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('OnJsTab');
            }

            // Escape ile popup kapat
            if (e.key === 'Escape') {
                dotnetRef.invokeMethodAsync('OnJsEscape');
            }

            // ArrowUp / ArrowDown ile popup navigasyonu
            if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
                dotnetRef.invokeMethodAsync('OnJsArrow', e.key === 'ArrowUp' ? -1 : 1)
                    .then(handled => {
                        if (handled) e.preventDefault();
                    });
            }

            // Enter ile seçim
            if (e.key === 'Enter') {
                dotnetRef.invokeMethodAsync('OnJsEnter')
                    .then(handled => {
                        if (handled) e.preventDefault();
                    });
            }
        });

        // İlk yükleme highlighting
        this.applySyntaxHighlight(el);
    },

    // Popup konumunu hesapla
    getPopupPosition: function (el) {
        if (!el) return { x: 0, y: 0, width: 0 };
        const pos = this.getCaretPixelPosition(el);
        const elRect = el.getBoundingClientRect();
        return {
            x: pos.x,
            y: pos.y + 4,
            width: elRect.width
        };
    }
};
