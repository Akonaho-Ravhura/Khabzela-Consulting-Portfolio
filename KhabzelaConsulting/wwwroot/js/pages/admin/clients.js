// Admin — Clients: live search and tax-status filter.
(function () {
    const search = document.querySelector("[data-client-search]");
    const filter = document.querySelector("[data-status-filter]");
    const rows = Array.from(document.querySelectorAll("[data-client-rows] tr"));
    const empty = document.querySelector("[data-clients-empty]");

    function apply() {
        const q = search.value.trim().toLowerCase();
        const status = filter.value;
        let visible = 0;
        rows.forEach((row) => {
            const show = (!q || row.dataset.search.includes(q)) && (!status || row.dataset.status === status);
            row.hidden = !show;
            if (show) visible++;
        });
        empty.hidden = visible > 0;
    }

    search.addEventListener("input", apply);
    filter.addEventListener("change", apply);
})();
