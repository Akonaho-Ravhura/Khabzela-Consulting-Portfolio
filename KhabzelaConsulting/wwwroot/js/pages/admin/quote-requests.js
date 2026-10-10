// Admin — Quote requests: status tabs with counts and inline status changes.
// TODO (backend): persist status changes.
(function () {
    const { initChoiceGroup, toast } = Khabzela;
    const rows = Array.from(document.querySelectorAll("[data-request-rows] tr"));
    const empty = document.querySelector("[data-empty]");
    let filter = "all";

    function updateCounts() {
        document.querySelectorAll("[data-count]").forEach((el) => {
            const key = el.dataset.count;
            el.textContent = rows.filter((r) => key === "all" || r.dataset.status === key).length;
        });
    }

    function apply() {
        let visible = 0;
        rows.forEach((r) => {
            const show = filter === "all" || r.dataset.status === filter;
            r.hidden = !show;
            if (show) visible++;
        });
        empty.hidden = visible > 0;
    }

    initChoiceGroup(document.querySelector("[data-filter-group]"), (value) => { filter = value; apply(); });

    document.querySelectorAll("[data-status-select]").forEach((select) => {
        select.addEventListener("change", () => {
            const row = select.closest("tr");
            row.dataset.status = select.value;
            select.className = `qr-status badge badge-${select.value.toLowerCase()}`;
            updateCounts();
            apply();
            toast(`Marked as ${select.value}`);
        });
    });

    updateCounts();
})();
