// Admin — Client detail: choose a tax status, add a note, save & notify (prepends to history).
// TODO (backend): POST the status + note and send the in-app notification.
(function () {
    const { toast } = Khabzela;
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

    form.addEventListener("submit", (e) => {
        e.preventDefault();
        if (status === savedStatus && !note.value.trim()) {
            toast("Choose a new status or add a note first");
            return;
        }

        save.disabled = true;
        save.textContent = "Sending…";

        setTimeout(() => {
            const time = new Date().toLocaleTimeString("en-ZA", { hour: "2-digit", minute: "2-digit" });
            const li = document.createElement("li");
            li.className = "is-new";
            const title = document.createElement("p");
            title.textContent = status === savedStatus ? "Note from your accountant" : `Status → ${status}`;
            const meta = document.createElement("p");
            meta.className = "muted small";
            meta.textContent = `Sent to client today, ${time} · Delivered`;
            li.append(title, meta);
            history.prepend(li);

            savedStatus = status;
            note.value = "";
            count.textContent = "0";
            save.disabled = false;
            save.textContent = "Save & notify client";
            toast("Client notified");
        }, 500);
    });
})();
