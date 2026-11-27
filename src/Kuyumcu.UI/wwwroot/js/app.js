// Kuyumcu Defteri – arayüz yardımcıları (klavye kısayolları, kaydırma, tema tercihi)
window.kuyumcu = (function () {
    let shortcutHandler = null;
    const scanInputs = {}; // id → { el, keydown, stray }

    return {
        // Genel kısayollar: Ctrl/Cmd+K → müşteri arama, Alt+1..8 → menü, F2 → Giden, F3 → Gelen,
        // Alt+← / Alt+→ → önceki / sonraki müşteri (sayfa destekliyorsa)
        registerShortcuts: function (dotnetRef, layoutRef) {
            if (shortcutHandler) document.removeEventListener('keydown', shortcutHandler);
            const dialogOpen = function () { return !!document.querySelector('.mud-dialog-container'); };
            shortcutHandler = function (e) {
                const key = (e.key || '').toLowerCase();
                if ((e.ctrlKey || e.metaKey) && key === 'k') {
                    e.preventDefault();
                    const input = document.querySelector('#global-search input');
                    if (input) { input.focus(); input.select(); }
                    else if (layoutRef) layoutRef.invokeMethodAsync('OpenSearch'); // menü daraltılmış
                    return;
                }
                // Alt+1..9 → sol menüdeki n. sayfa (menü sırası). Sadece üst sıradaki rakam tuşları:
                // bazı barkod okuyucular karakterleri "Alt + numerik tuş takımı" ile gönderir; onlar sayfa değiştirmemeli.
                if (e.altKey && !e.ctrlKey && !e.metaKey && /^[1-9]$/.test(e.key) && !(e.code || '').startsWith('Numpad')) {
                    if (dialogOpen()) return;
                    const items = document.querySelectorAll('.kd-sidebar a.kd-nav-item');
                    const target = items[parseInt(e.key, 10) - 1];
                    if (target) { e.preventDefault(); target.click(); }
                    return;
                }
                // Alt+← / Alt+→ → sayfalar arasında geri / ileri (tarayıcıdaki gibi). WebView'in kendi kısayolu yerine
                // burada yapılır: açık pencere varken sayfa değişmesin.
                if (e.altKey && !e.ctrlKey && !e.metaKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
                    e.preventDefault();
                    if (dialogOpen()) return;
                    if (e.key === 'ArrowLeft') history.back(); else history.forward();
                    return;
                }
                // Ctrl+← / Ctrl+→ → önceki / sonraki müşteri (müşteri sayfasında). Yazı alanındayken imleç kelime atlar.
                if (e.ctrlKey && !e.altKey && !e.metaKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
                    const a = document.activeElement;
                    if (a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.isContentEditable)) return;
                    if (dialogOpen()) return;
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('OnShortcut', e.key === 'ArrowLeft' ? 'PrevCustomer' : 'NextCustomer');
                    return;
                }
                if (e.key === 'F2' || e.key === 'F3') {
                    // Açık bir pencere varken kısayol tetiklenmesin
                    if (dialogOpen()) return;
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('OnShortcut', e.key);
                }
            };
            document.addEventListener('keydown', shortcutHandler);

            // Faredeki yan tuşlar (geri = 3, ileri = 4). Tarayıcının kendi işlemi engellenip burada yapılır:
            // böylece tek adım gidilir ve açık pencere varken sayfa değişmez.
            if (!window.kuyumcu._mouseNav) {
                window.kuyumcu._mouseNav = true;
                const isSide = function (e) { return e.button === 3 || e.button === 4; };
                document.addEventListener('mousedown', function (e) { if (isSide(e)) e.preventDefault(); }, true);
                document.addEventListener('mouseup', function (e) {
                    if (!isSide(e)) return;
                    e.preventDefault();
                    if (dialogOpen()) return;
                    if (e.button === 3) history.back(); else history.forward();
                }, true);
            }
        },

        // Geri / ileri gidilebilir mi (menüdeki düğmeler için). Navigation API yoksa ikisi de açık sayılır.
        navState: function () {
            const n = window.navigation;
            return n && typeof n.canGoBack === 'boolean'
                ? [n.canGoBack, n.canGoForward]
                : [history.length > 1, true];
        },
        goBack: function () { history.back(); },
        goForward: function () { history.forward(); },

        isNarrowScreen: function () { return window.innerWidth < 1280; },

        // id bir input'a ya da input içeren bir kapsayıcıya ait olabilir; pencere animasyonu için kısa süre dener
        focusAndSelect: function (id) {
            let tries = 0;
            (function attempt() {
                let el = document.getElementById(id);
                if (el && el.tagName !== 'INPUT') el = el.querySelector('input');
                if (el) { el.focus(); el.select && el.select(); return; }
                if (++tries < 20) setTimeout(attempt, 50);
            })();
        },

        // Barkod okutma / Enter ile gönderilen giriş alanı.
        // Okuyucu (Sunlux RH10) klavye gibi rakamları yazıp sonuna Enter ekler; sadece Enter'la gelen giriş işlenir.
        // .NET tarafı OnEnter(value) çağrılır; false dönerse (geçersiz giriş) metin geri yazılır. Sonra alan yeniden seçilir.
        // captureStrayKeys: odak bir giriş alanında değilken gelen rakam tuşları bu alana yönlendirilir
        // (odak kaybolsa da okutma boşa gitmez). Açık pencere varken ya da başka bir alana yazılırken karışmaz.
        attachScanInput: function (id, dotnetRef, captureStrayKeys) {
            const el = document.getElementById(id);
            if (!el) return;
            this.detachScanInput(id);

            const keydown = function (e) {
                if (e.key !== 'Enter' && e.code !== 'NumpadEnter') return;
                e.preventDefault();
                e.stopPropagation();
                // Değer hemen alınıp alan temizlenir: .NET işlerken gelen bir sonraki okutma kaybolmaz
                const value = el.value;
                el.value = '';
                dotnetRef.invokeMethodAsync('OnEnter', value)
                    .then(function (accepted) {
                        // Reddedilen giriş (ör. hatalı gram) düzeltilebilsin diye geri yazılır
                        if (!accepted && el.value === '') el.value = value;
                    })
                    .catch(function () { if (el.value === '') el.value = value; })
                    .finally(function () {
                        if (!el.disabled && document.activeElement !== el && !document.querySelector('.mud-dialog-container')) el.focus();
                        if (document.activeElement === el) el.select();
                    });
            };
            el.addEventListener('keydown', keydown);
            let stray = null;

            if (captureStrayKeys) {
                stray = function (e) {
                    if (e.ctrlKey || e.altKey || e.metaKey || e.key.length !== 1 || e.key < '0' || e.key > '9') return;
                    const a = document.activeElement;
                    if (a === el || el.disabled) return;
                    if (a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.tagName === 'SELECT' || a.isContentEditable)) return;
                    if (document.querySelector('.mud-dialog-container')) return;
                    el.value = '';
                    el.focus(); // tuşun karakteri artık bu alana yazılır
                };
                document.addEventListener('keydown', stray, true);
            }
            scanInputs[id] = { el: el, keydown: keydown, stray: stray };

            // Sayfa geçişinde Blazor odağı başlığa (h1) taşır; ondan sonra okutma alanını seç
            const focusIfFree = function () {
                const a = document.activeElement;
                const busyElsewhere = a && a !== el && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.tagName === 'SELECT');
                if (!busyElsewhere && !el.disabled && !document.querySelector('.mud-dialog-container')) { el.focus(); el.select(); }
            };
            setTimeout(focusIfFree, 0);
            setTimeout(focusIfFree, 150);
        },

        // Okutma alanını başka bir alandan (ör. taş gramı) gönderir: alanda Enter'a basılmış gibi.
        // Alan boşsa gönderilmez, sadece odaklanır (önce gram yazılsın). Gönderildiyse true.
        submitScanInput: function (id) {
            const el = document.getElementById(id);
            if (!el || el.disabled) return false;
            if (!el.value.trim()) { el.focus(); return false; }
            el.focus();
            el.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true }));
            return true;
        },

        detachScanInput: function (id) {
            // Eleman sayfadan kalkmış olsa da belge dinleyicisi kaldırılır
            const entry = scanInputs[id];
            if (!entry) return;
            entry.el.removeEventListener('keydown', entry.keydown);
            if (entry.stray) document.removeEventListener('keydown', entry.stray, true);
            delete scanInputs[id];
        },

        // Okutma sonucu için kısa ses: başarılı tek kısa bip, hata iki kalın bip (kasada ekrana bakmadan anlaşılsın)
        beep: function (ok) {
            try {
                const Ctx = window.AudioContext || window.webkitAudioContext;
                if (!Ctx) return;
                const ctx = window.kuyumcu._audio || (window.kuyumcu._audio = new Ctx());
                const play = function (freq, start, dur) {
                    const o = ctx.createOscillator(), g = ctx.createGain();
                    o.frequency.value = freq; o.type = 'square';
                    g.gain.value = 0.05;
                    o.connect(g); g.connect(ctx.destination);
                    o.start(ctx.currentTime + start); o.stop(ctx.currentTime + start + dur);
                };
                if (ok) play(1400, 0, 0.07);
                else { play(300, 0, 0.15); play(300, 0.22, 0.15); }
            } catch { }
        },

        setInputValue: function (id, value) {
            const el = document.getElementById(id);
            if (el) el.value = value;
        },

        // Elemanı ortaya kaydırır ve kısa süre vurgular
        scrollIntoView: function (id) {
            const el = document.getElementById(id);
            if (!el) return;
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            el.classList.remove('kd-flash');
            void el.offsetWidth;
            el.classList.add('kd-flash');
            setTimeout(function () { el.classList.remove('kd-flash'); }, 1600);
        },

        getPreference: function (key) {
            try { return localStorage.getItem('kd.' + key); } catch { return null; }
        },
        setPreference: function (key, value) {
            try { localStorage.setItem('kd.' + key, value); } catch { }
        }
    };
})();
