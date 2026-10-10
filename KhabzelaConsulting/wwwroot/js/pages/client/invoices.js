// Client — Invoices: status filter and expandable invoice rows.
(function () {
    const { initChoiceGroup, toast } = Khabzela;
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
        inv.querySelector(".inv-download").addEventListener("click", () => {
            // TODO (backend): link to the generated invoice PDF.
            toast("Invoice PDFs will be available once the backend is connected");
        });
    });
})();
