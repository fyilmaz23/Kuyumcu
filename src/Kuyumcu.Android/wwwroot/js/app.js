// Küçük yardımcılar (Blazor'dan JS ile çağrılır)
window.kd = {
    // Uygulama içi gezinme geçmişi var mı? (ilk sayfa tek kayıttır)
    canGoBack: function () { return window.history.length > 1; },
    scrollTop: function () { window.scrollTo(0, 0); },
    scrollIntoView: function (id) {
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    },
    blur: function () { if (document.activeElement) document.activeElement.blur(); }
};
