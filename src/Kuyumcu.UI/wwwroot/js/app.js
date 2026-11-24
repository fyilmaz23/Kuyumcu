// Kuyumcu Defteri – arayüz yardımcıları (klavye kısayolları, kaydırma, tema tercihi)
window.kuyumcu = (function () {
    let shortcutHandler = null;

    return {
        // Genel kısayollar: Ctrl/Cmd+K → müşteri arama, F2 → Giden, F3 → Gelen (sayfa destekliyorsa)
        registerShortcuts: function (dotnetRef) {
            if (shortcutHandler) document.removeEventListener('keydown', shortcutHandler);
            shortcutHandler = function (e) {
                const key = (e.key || '').toLowerCase();
                if ((e.ctrlKey || e.metaKey) && key === 'k') {
                    e.preventDefault();
                    const input = document.querySelector('#global-search input');
                    if (input) { input.focus(); input.select(); }
                    return;
                }
                // Alt+1..9 → üst menüdeki n. sayfa (sıra: Ana Sayfa, Müşteriler, İşlemler, Ayarlar)
                if (e.altKey && !e.ctrlKey && !e.metaKey && /^[1-9]$/.test(e.key)) {
                    if (document.querySelector('.mud-dialog-container')) return;
                    const items = document.querySelectorAll('.kd-topbar a.kd-topnav-item');
                    const target = items[parseInt(e.key, 10) - 1];
                    if (target) { e.preventDefault(); target.click(); }
                    return;
                }
                if (e.key === 'F2' || e.key === 'F3') {
                    // Açık bir pencere varken kısayol tetiklenmesin
                    if (document.querySelector('.mud-dialog-container')) return;
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('OnShortcut', e.key);
                }
            };
            document.addEventListener('keydown', shortcutHandler);
        },

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
