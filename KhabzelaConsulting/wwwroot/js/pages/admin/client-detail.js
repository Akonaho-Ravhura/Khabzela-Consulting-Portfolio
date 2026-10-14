// Admin — Client detail: choose a tax status, add a note, save & notify (prepends to history).
// Saving POSTs to the server, which stores the status and creates the client's notification.
(function () {
    const { toast, post } = Khabzela;
    const errorEl = document.querySelector("[data-status-error]");
    const group = document.querySelector("[data-status-group]");
    const form = document.querySelector("[data-status-form]");
    const note = document.querySelector("[data-note]");
    const count = document.querySelector("[data-note-count]");
    const history = document.querySelector("[data-history]");
    const save = document.querySelector("[data-save]");

    let status = group.querySelector(".is-active")?.dataset.value;
    let savedStatus = status;

    group.addEventListener("click", (e) => {
        const btn = e.target.closest(".choice");
        if (!btn) return;
        group.querySelectorAll(".choice").forEach((b) => {
            b.classList.toggle("is-active", b === btn);
            b.setAttribute("aria-checked", String(b === btn));
        });
        status = btn.dataset.value;
    });

    note.addEventListener("input", () => { count.textContent = note.value.length; });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        errorEl.textContent = "";
        if (status === savedStatus && !note.value.trim()) {
            errorEl.textContent = "Choose a new status or add a note first.";
            return;
        }

        save.disabled = true;
        save.textContent = "Sending…";
        try {
            const result = await post(form.dataset.url, { status, note: note.value.trim() });
            const li = document.createElement("li");
            li.className = "is-new";
            const title = document.createElement("p");
            title.textContent = result.title;
            const meta = document.createElement("p");
            meta.className = "muted small";
            meta.textContent = result.meta;
            li.append(title, meta);
            history.prepend(li);
            document.querySelector("[data-history-empty]").hidden = true;

            savedStatus = status;
            note.value = "";
            count.textContent = "0";
            toast("Client notified");
        } catch (err) {
            errorEl.textContent = err.message;
        } finally {
            save.disabled = false;
            save.textContent = "Save & notify client";
        }
    });
})();

// Tax profile: save which SARS returns apply, then refresh the upcoming deadlines list.
(function () {
    const { toast, post } = Khabzela;
    const form = document.querySelector("[data-profile-form]");
    if (!form) return;
    const errorEl = form.querySelector("[data-profile-error]");
    const save = form.querySelector("[data-profile-save]");
    const vatToggle = form.querySelector("[data-vat-toggle]");
    const vatCategory = form.querySelector("[data-vat-category]");
    const list = document.querySelector("[data-deadlines]");
    const empty = document.querySelector("[data-deadlines-empty]");

    vatToggle.addEventListener("change", () => { vatCategory.hidden = !vatToggle.checked; });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        errorEl.textContent = "";
        save.disabled = true;
        const data = {};
        form.querySelectorAll("input[type=checkbox]").forEach((c) => { data[c.name] = c.checked; });
        form.querySelectorAll("select").forEach((s) => { data[s.name] = s.value; });
        try {
            const result = await post(form.dataset.url, data);
            list.innerHTML = "";
            result.deadlines.forEach((d) => {
                const li = document.createElement("li");
                const title = document.createElement("p");
                title.textContent = `${d.title} — ${d.due}`;
                const detail = document.createElement("p");
                detail.className = "muted small";
                detail.textContent = d.detail;
                li.append(title, detail);
                list.append(li);
            });
            empty.hidden = result.deadlines.length > 0;
            toast("Tax profile saved — reminders updated");
        } catch (err) {
            errorEl.textContent = err.message;
        } finally {
            save.disabled = false;
        }
    });
})();
