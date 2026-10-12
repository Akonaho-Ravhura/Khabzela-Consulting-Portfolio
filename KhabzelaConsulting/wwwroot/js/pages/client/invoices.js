// Client — Invoices: status filter and expandable invoice rows.
(function () {
    const { initChoiceGroup } = Khabzela;
    const invoices = Array.from(document.querySelectorAll(".inv"));

    initChoiceGroup(document.querySelector("[data-filter-group]"), (status) => {
        invoices.forEach((inv) => { inv.hidden = status !== "all" && inv.dataset.status !== status; });
    });

    invoices.forEach((inv) => {
        const row = inv.querySelector(".inv-row");
        const detail = inv.querySelector(".inv-detail");
        row.addEventListener("click", () => {
            const open = row.getAttribute("aria-expanded") === "true";
            row.setAttribute("aria-expanded", String(!open));
            detail.hidden = open;
        });
    });
})();
