// Admin — Invoices: totals, mark as paid, and create a new invoice through a dialog.
// TODO (backend): persist invoices and status changes, and notify the client.
(function () {
    const { formatRand, toast } = Khabzela;
    const tbody = document.querySelector("[data-invoice-rows]");
    const dialog = document.querySelector("[data-invoice-dialog]");
    const form = document.querySelector("[data-invoice-form]");
    const fields = {
        client: form.querySelector("#inv-client"),
        description: form.querySelector("#inv-description"),
        amount: form.querySelector("#inv-amount"),
        due: form.querySelector("#inv-due")
    };

    function updateTotals() {
        const totals = { Due: 0, Overdue: 0, Paid: 0 };
        tbody.querySelectorAll("tr").forEach((r) => { totals[r.dataset.status] += parseFloat(r.dataset.amount); });
        // "Outstanding" includes overdue amounts.
        totals.Due += totals.Overdue;
        document.querySelectorAll("[data-total]").forEach((el) => { el.textContent = formatRand(totals[el.dataset.total]); });
    }

    function flash(row) {
        row.classList.remove("is-updated");
        void row.offsetWidth;
        row.classList.add("is-updated");
    }

    tbody.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-mark-paid]");
        if (!btn) return;
        const row = btn.closest("tr");
        row.dataset.status = "Paid";
        const badge = row.querySelector("[data-status-badge]");
        badge.textContent = "Paid";
        badge.className = "badge badge-paid";
        btn.remove();
        flash(row);
        updateTotals();
        toast("Invoice marked as paid — client notified");
    });

    function nextNumber() {
        const numbers = Array.from(tbody.querySelectorAll("strong")).map((s) => parseInt(s.textContent.replace(/\D/g, ""), 10) || 0);
        return Math.max(0, ...numbers) + 1;
    }

    document.querySelector("[data-new-invoice]").addEventListener("click", () => {
        form.reset();
        Object.values(fields).forEach((f) => f.classList.remove("is-invalid"));
        const due = new Date();
        due.setDate(due.getDate() + 14);
        fields.due.value = due.toISOString().slice(0, 10);
        dialog.showModal();
        fields.client.focus();
    });
    form.querySelectorAll("[data-dialog-cancel]").forEach((b) => b.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", (e) => { if (e.target === dialog) dialog.close(); });

    form.addEventListener("submit", (e) => {
        const invalid = Object.values(fields).filter((f) => !f.value || !f.checkValidity());
        Object.values(fields).forEach((f) => f.classList.toggle("is-invalid", invalid.includes(f)));
        if (invalid.length) { e.preventDefault(); invalid[0].focus(); return; }

        const amount = Math.round(parseFloat(fields.amount.value));
        const dueText = new Date(fields.due.value + "T00:00:00").toLocaleDateString("en-ZA", { day: "numeric", month: "short" });
        const row = document.createElement("tr");
        row.dataset.status = "Due";
        row.dataset.amount = amount;
        row.innerHTML = `
            <td data-label=""><span class="ai-inv"><strong></strong><span class="muted small"></span></span></td>
            <td data-label="Client"></td>
            <td data-label="Amount"></td>
            <td data-label="Due" class="text-muted"></td>
            <td data-label="Status"><span class="badge badge-due" data-status-badge>Due</span></td>
            <td data-label="" class="ai-actions"><button class="btn-link small" type="button" data-mark-paid>Mark paid</button></td>`;
        row.querySelector("strong").textContent = `Invoice #${nextNumber()}`;
        row.querySelector(".ai-inv .muted").textContent = fields.description.value.trim();
        row.children[1].textContent = fields.client.value;
        row.children[2].textContent = formatRand(amount);
        row.children[3].textContent = dueText;
        tbody.prepend(row);
        flash(row);
        updateTotals();
        toast("Invoice created — client notified");
    });

    updateTotals();
})();
