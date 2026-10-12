// Admin — Team: invite administrators (they set their own password via a one-time link) and remove access.
(function () {
    const { toast, post } = Khabzela;
    const dialog = document.querySelector("[data-invite-dialog]");
    const form = document.querySelector("[data-invite-form]");
    const formStep = form.querySelector('[data-invite-step="form"]');
    const doneStep = form.querySelector('[data-invite-step="done"]');
    const errorEl = form.querySelector("[data-invite-error]");
    const save = form.querySelector("[data-invite-save]");
    const linkInput = form.querySelector("[data-invite-link]");
    const message = form.querySelector("[data-invite-message]");
    const fields = Array.from(formStep.querySelectorAll(".input"));
    const tbody = document.querySelector("[data-team-rows]");

    function showLink(result, name) {
        linkInput.value = result.link;
        message.textContent = result.emailed
            ? `Invitation emailed to ${name}. You can also share the link below.`
            : `Email isn't set up yet, so send ${name} this link yourself (e.g. by WhatsApp).`;
        formStep.hidden = true;
        doneStep.hidden = false;
        if (!dialog.open) dialog.showModal();
        linkInput.select();
    }

    document.querySelector("[data-invite-open]").addEventListener("click", () => {
        form.reset();
        fields.forEach((f) => f.classList.remove("is-invalid"));
        errorEl.textContent = "";
        formStep.hidden = false;
        doneStep.hidden = true;
        dialog.showModal();
        fields[0].focus();
    });
    form.querySelectorAll("[data-dialog-cancel]").forEach((b) => b.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", (e) => { if (e.target === dialog) dialog.close(); });

    form.querySelector("[data-copy-link]").addEventListener("click", async () => {
        try { await navigator.clipboard.writeText(linkInput.value); toast("Link copied"); }
        catch { linkInput.select(); toast("Press Ctrl/Cmd + C to copy"); }
    });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (!doneStep.hidden) return;
        const invalid = fields.filter((f) => !f.checkValidity() || (f.required && !f.value.trim()));
        fields.forEach((f) => f.classList.toggle("is-invalid", invalid.includes(f)));
        if (invalid.length) {
            errorEl.textContent = "Enter their full name and a valid email address.";
            invalid[0].focus();
            return;
        }
        errorEl.textContent = "";
        save.disabled = true;
        try {
            const data = Object.fromEntries(fields.map((f) => [f.name, f.value.trim()]));
            const result = await post("/Admin/InviteTeamMember", data);

            const row = document.createElement("tr");
            row.dataset.id = result.id;
            row.innerHTML = `
                <td data-label=""><span class="team-name"><span class="avatar"></span><span><strong></strong><span class="muted small"></span></span></span></td>
                <td data-label="Speciality"></td>
                <td data-label="Status"><span class="badge badge-due">Invited</span></td>
                <td data-label="" class="team-actions">
                    <button class="btn-link small" type="button" data-resend>Copy new invite link</button>
                    <button class="btn-link small team-remove" type="button" data-remove>Remove access</button>
                </td>`;
            row.querySelector(".avatar").textContent = result.initials;
            row.querySelector("strong").textContent = result.name;
            row.querySelector(".team-name .muted").textContent = result.email;
            row.children[1].textContent = result.speciality || "—";
            tbody.append(row);
            showLink(result, result.name);
        } catch (err) {
            errorEl.textContent = err.message;
        } finally {
            save.disabled = false;
        }
    });

    tbody.addEventListener("click", async (e) => {
        const row = e.target.closest("tr");
        const name = row?.querySelector("strong")?.textContent.replace(" (you)", "");

        if (e.target.closest("[data-resend]")) {
            try {
                const result = await post(`/Admin/ResendInvite/${row.dataset.id}`);
                form.reset();
                showLink(result, name);
            } catch (err) { toast(err.message); }
        }

        if (e.target.closest("[data-remove]")) {
            if (!confirm(`Remove ${name}'s access to the admin portal? They'll be signed out immediately.`)) return;
            try {
                await post(`/Admin/RemoveTeamMember/${row.dataset.id}`);
                row.remove();
                toast(`${name} no longer has access`);
            } catch (err) { toast(err.message); }
        }
    });
})();
