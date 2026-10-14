// Client — Notifications: filter by type and mark all as read.
(function () {
    const { initChoiceGroup, toast, post } = Khabzela;
    const items = Array.from(document.querySelectorAll("[data-notif-list] .notif"));
    const empty = document.querySelector("[data-notif-empty]");
    const markAll = document.querySelector("[data-mark-all]");

    function markRead() {
        items.forEach((n) => n.classList.remove("is-unread"));
        markAll.hidden = true;
        const dot = document.querySelector("[data-unread-dot]");
        if (dot) dot.hidden = true;
    }

    initChoiceGroup(document.querySelector("[data-filter-group]"), (type) => {
        let visible = 0;
        items.forEach((n) => {
            const show = type === "all" || n.dataset.type === type;
            n.hidden = !show;
            if (show) visible++;
        });
        empty.hidden = visible > 0;
    });

    markAll.addEventListener("click", async () => {
        markAll.disabled = true;
        try {
            await post("/Client/MarkAllRead");
            markRead();
            toast("All notifications marked as read");
        } catch (err) {
            toast(err.message);
        } finally {
            markAll.disabled = false;
        }
    });
})();
