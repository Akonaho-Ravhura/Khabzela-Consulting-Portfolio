// Admin — Invoices: totals, mark as paid, and create a new invoice through a dialog.
// Creating and marking paid are saved on the server, which also notifies the client.
(function () {
    const { formatRand, toast, post } = Khabzela;
    const errorEl = document.querySelector("[data-invoice-error]");
    const saveBtn = document.querySelector("[data-invoice-save]");
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

    tbody.addEventListener("click", async (e) => {
        const btn = e.target.closest("[data-mark-paid]");
        if (!btn) return;
        const row = btn.closest("tr");
        btn.disabled = true;
        try {
            await post(`/Admin/MarkInvoicePaid/${row.dataset.id}`);
        } catch (err) {
            btn.disabled = false;
            toast(err.message);
            return;
        }
        row.dataset.status = "Paid";
        const badge = row.querySelector("[data-status-badge]");
        badge.textContent = "Paid";
        badge.className = "badge badge-paid";
        btn.remove();
        flash(row);
        updateTotals();
        toast("Invoice marked as paid — client notified");
    });

    document.querySelector("[data-new-invoice]").addEventListener("click", () => {
        form.reset();
        Object.values(fields).forEach((f) => f.classList.remove("is-invalid"));
        const due = new Date();
        due.setDate(due.getDate() + 14);
        fields.due.value = due.toISOString().slice(0, 10);
        errorEl.textContent = "";
        dialog.showModal();
        fields.client.focus();
    });
    form.querySelectorAll("[data-dialog-cancel]").forEach((b) => b.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", (e) => { if (e.target === dialog) dialog.close(); });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const invalid = Object.values(fields).filter((f) => !f.value || !f.checkValidity());
        Object.values(fields).forEach((f) => f.classList.toggle("is-invalid", invalid.includes(f)));
        if (invalid.length) {
            errorEl.textContent = "Fill in all the fields (amount of at least R1).";
            invalid[0].focus();
            return;
        }

        saveBtn.disabled = true;
        let result;
        try {
            result = await post("/Admin/CreateInvoice", {
                clientId: fields.client.value,
                description: fields.description.value.trim(),
                amount: fields.amount.value,
                dueDate: fields.due.value
            });
        } catch (err) {
            errorEl.textContent = err.message;
            return;
        } finally {
            saveBtn.disabled = false;
        }

        const row = document.createElement("tr");
        row.dataset.status = "Due";
        row.dataset.amount = result.amount;
        row.dataset.id = result.id;
        row.innerHTML = `
            <td data-label=""><span class="ai-inv"><strong></strong><span class="muted small"></span></span></td>
            <td data-label="Client"></td>
            <td data-label="Amount"></td>
            <td data-label="Due" class="text-muted"></td>
            <td data-label="Status"><span class="badge badge-due" data-status-badge>Due</span></td>
            <td data-label="" class="ai-actions"><button class="btn-link small" type="button" data-mark-paid>Mark paid</button></td>`;
        row.querySelector("strong").textContent = `Invoice ${result.number}`;
        row.querySelector(".ai-inv .muted").textContent = result.description;
        row.children[1].textContent = result.client;
        row.children[2].textContent = formatRand(result.amount);
        row.children[3].textContent = result.due;
        tbody.prepend(row);
        document.querySelector("[data-invoices-empty]")?.remove();
        dialog.close();
        flash(row);
        updateTotals();
        toast("Invoice created — client notified");
    });

    updateTotals();
})();
