// Client layout — shows the unread dot on the Notifications tab.
// Until the backend exists, unread state is kept in sessionStorage by the notifications page.
(function () {
    const dot = document.querySelector("[data-unread-dot]");
    if (!dot) return;
    const read = Khabzela.store.get("kc.notificationsRead");
    dot.hidden = !!read;
})();
