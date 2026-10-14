// Admin — Pricing rules: edit a rule's label, min/max amount and status. Saved rules feed the
// client quote calculator straight away.
(function () {
    const { toast, post } = Khabzela;
    const dialog = document.querySelector("[data-rule-dialog]");
    const form = document.querySelector("[data-rule-form]");
    const groupEl = document.querySelector("[data-dialog-group]");
    const unitEl = document.querySelector("[data-dialog-unit]");
    const errorEl = document.querySelector("[data-rule-error]");
    const saveBtn = document.querySelector("[data-rule-save]");
    const label = form.querySelector("#rule-variable");
    const min = form.querySelector("#rule-min");
    const max = form.querySelector("#rule-max");
    const statusGroup = form.querySelector("[data-rule-status]");

    let editingRow = null;

    function setStatus(value) {
        statusGroup.querySelectorAll(".choice").forEach((b) => {
            const on = b.dataset.value === value;
            b.classList.toggle("is-active", on);
            b.setAttribute("aria-pressed", String(on));
        });
    }
    statusGroup.addEventListener("click", (e) => {
        const btn = e.target.closest(".choice");
        if (btn) setStatus(btn.dataset.value);
    });

    function open(row) {
        editingRow = row;
        groupEl.textContent = row.closest("[data-group]").dataset.group;
        label.value = row.querySelector('[data-field="variable"]').textContent.trim();
        min.value = row.dataset.min;
        max.value = row.dataset.max;
        unitEl.textContent = `Amounts are ${row.dataset.unit}. Use the same value for both to charge a fixed amount.`;
        setStatus(row.querySelector('[data-field="status"]').textContent.trim());
        [label, min, max].forEach((i) => i.classList.remove("is-invalid"));
        errorEl.textContent = "";
        dialog.showModal();
        label.focus();
    }

    function flash(row) {
        row.classList.remove("is-updated");
        void row.offsetWidth;
        row.classList.add("is-updated");
    }

    document.addEventListener("click", (e) => {
        const edit = e.target.closest("[data-edit-rule]");
        if (edit) open(edit.closest("tr"));
    });

    form.querySelectorAll("[data-dialog-cancel]").forEach((b) => b.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", (e) => { if (e.target === dialog) dialog.close(); });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const invalid = [label, min, max].filter((i) => !i.value.trim() || !i.checkValidity());
        [label, min, max].forEach((i) => i.classList.toggle("is-invalid", invalid.includes(i)));
        if (invalid.length) {
            errorEl.textContent = "Fill in a name and amounts between R0 and R1,000,000.";
            invalid[0].focus();
            return;
        }
        if (parseFloat(max.value) < parseFloat(min.value)) {
            max.classList.add("is-invalid");
            errorEl.textContent = "The maximum can't be less than the minimum.";
            max.focus();
            return;
        }

        saveBtn.disabled = true;
        try {
            const status = statusGroup.querySelector(".is-active").dataset.value;
            const result = await post(`/Admin/UpdatePricingRule/${editingRow.dataset.id}`, {
                label: label.value.trim(), minValue: min.value, maxValue: max.value, status
            });
            editingRow.dataset.min = min.value;
            editingRow.dataset.max = max.value;
            editingRow.querySelector('[data-field="variable"]').textContent = result.label;
            editingRow.querySelector('[data-field="formula"]').textContent = result.formula;
            const statusEl = editingRow.querySelector('[data-field="status"]');
            statusEl.textContent = result.status;
            statusEl.className = `pr-status pr-status-${result.status.toLowerCase()}`;
            dialog.close();
            flash(editingRow);
            toast("Rule updated — the quote calculator uses it now");
        } catch (err) {
            errorEl.textContent = err.message;
        } finally {
            saveBtn.disabled = false;
        }
    });
})();
