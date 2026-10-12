// Registers the service worker so the app can be installed on a phone or desktop.
(function () {
    if ("serviceWorker" in navigator) {
        window.addEventListener("load", () => {
            navigator.serviceWorker.register("/sw.js").catch(() => { /* app still works without it */ });
        });
    }
})();
