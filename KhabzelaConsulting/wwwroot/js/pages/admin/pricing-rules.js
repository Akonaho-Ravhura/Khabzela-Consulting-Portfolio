// Admin — Pricing rules: add / edit / delete rules through a dialog.
// TODO (backend): persist changes so the quote calculator picks them up.
(function () {
    const { toast } = Khabzela;
    const dialog = document.querySelector("[data-rule-dialog]");
    const form = document.querySelector("[data-rule-form]");
    const titleEl = document.querySelector("[data-dialog-title]");
    const groupEl = document.querySelector("[data-dialog-group]");
    const variable = form.querySelector("#rule-variable");
    const formula = form.querySelector("#rule-formula");
    const statusGroup = form.querySelector("[data-rule-status]");
    const deleteBtn = form.querySelector("[data-rule-delete]");

    let editingRow = null;
    let targetBody = null;

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

    function open(row, group) {
        editingRow = row;
        targetBody = group.querySelector("tbody");
        titleEl.textContent = row ? "Edit rule" : "Add rule";
        groupEl.textContent = group.dataset.group;
        variable.value = row ? row.querySelector('[data-field="variable"]').textContent.trim() : "";
        formula.value = row ? row.querySelector('[data-field="formula"]').textContent.trim() : "";
        setStatus(row ? row.querySelector('[data-field="status"]').textContent.trim() : "Active");
        deleteBtn.hidden = !row;
        [variable, formula].forEach((i) => i.classList.remove("is-invalid"));
        dialog.showModal();
        variable.focus();
    }

    function buildRow() {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td data-label="Variable" data-field="variable"></td>
            <td data-label="Price impact" data-field="formula"></td>
            <td data-label="Status"><span class="pr-status" data-field="status"></span></td>
            <td data-label="" class="pr-actions"><button class="btn-link small" type="button" data-edit-rule>Edit</button></td>`;
        return tr;
    }

    function flash(row) {
        row.classList.remove("is-updated");
        void row.offsetWidth;
        row.classList.add("is-updated");
    }

    document.addEventListener("click", (e) => {
        const edit = e.target.closest("[data-edit-rule]");
        const add = e.target.closest("[data-add-rule]");
        if (edit) open(edit.closest("tr"), edit.closest("[data-group]"));
        if (add) open(null, add.closest("[data-group]"));
    });

    form.querySelectorAll("[data-dialog-cancel]").forEach((b) => b.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", (e) => { if (e.target === dialog) dialog.close(); });

    form.addEventListener("submit", (e) => {
        const invalid = [variable, formula].filter((i) => !i.value.trim());
        [variable, formula].forEach((i) => i.classList.toggle("is-invalid", invalid.includes(i)));
        if (invalid.length) { e.preventDefault(); invalid[0].focus(); return; }

        const status = statusGroup.querySelector(".is-active").dataset.value;
        const row = editingRow || targetBody.appendChild(buildRow());
        row.querySelector('[data-field="variable"]').textContent = variable.value.trim();
        row.querySelector('[data-field="formula"]').textContent = formula.value.trim();
        const statusEl = row.querySelector('[data-field="status"]');
        statusEl.textContent = status;
        statusEl.className = `pr-status pr-status-${status.toLowerCase()}`;
        flash(row);
        toast(editingRow ? "Rule updated" : "Rule added");
    });

    deleteBtn.addEventListener("click", () => {
        if (!editingRow) return;
        if (!confirm(`Delete "${editingRow.querySelector('[data-field="variable"]').textContent.trim()}"?`)) return;
        editingRow.remove();
        dialog.close();
        toast("Rule deleted");
    });
})();
