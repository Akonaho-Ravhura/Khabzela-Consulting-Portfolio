// Client — Notifications: filter by type and mark all as read.
(function () {
    const { initChoiceGroup, store, toast } = Khabzela;
    const items = Array.from(document.querySelectorAll("[data-notif-list] .notif"));
    const empty = document.querySelector("[data-notif-empty]");
    const markAll = document.querySelector("[data-mark-all]");

    function markRead() {
        items.forEach((n) => n.classList.remove("is-unread"));
        markAll.hidden = true;
        const dot = document.querySelector("[data-unread-dot]");
        if (dot) dot.hidden = true;
    }

    if (store.get("kc.notificationsRead")) markRead();

    initChoiceGroup(document.querySelector("[data-filter-group]"), (type) => {
        let visible = 0;
        items.forEach((n) => {
            const show = type === "all" || n.dataset.type === type;
            n.hidden = !show;
            if (show) visible++;
        });
        empty.hidden = visible > 0;
    });

    markAll.addEventListener("click", () => {
        // TODO (backend): persist read state on the server.
        store.set("kc.notificationsRead", true);
        markRead();
        toast("All notifications marked as read");
    });
})();
