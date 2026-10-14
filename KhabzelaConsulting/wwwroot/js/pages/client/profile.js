// Client — Profile: save details (enabled only when something changed) and notification preferences.
(function () {
    const { toast, post } = Khabzela;
    const form = document.querySelector("[data-profile-form]");
    const save = form.querySelector("[data-save]");
    const errorEl = form.querySelector("[data-profile-error]");
    const inputs = Array.from(form.querySelectorAll(".input"));
    let initial = inputs.map((i) => i.value);

    function isDirty() { return inputs.some((i, idx) => i.value !== initial[idx]); }

    form.addEventListener("input", () => { save.disabled = !isDirty(); });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const invalid = inputs.find((i) => !i.checkValidity());
        inputs.forEach((i) => i.classList.toggle("is-invalid", !i.checkValidity()));
        if (invalid) {
            errorEl.textContent = invalid.validity.patternMismatch ? "Enter a valid South African mobile number."
                : invalid.validity.typeMismatch ? "Enter a valid email address." : "Please fill in the required fields.";
            invalid.focus();
            return;
        }
        errorEl.textContent = "";
        save.disabled = true;
        save.textContent = "Saving…";
        try {
            await post("/Client/Profile", Object.fromEntries(inputs.map((i) => [i.name, i.value.trim()])));
            initial = inputs.map((i) => i.value);
            document.querySelector(".profile-head h1").textContent = form.querySelector("#fullName").value.trim();
            toast("Profile updated");
        } catch (err) {
            errorEl.textContent = err.message;
            save.disabled = false;
        } finally {
            save.textContent = "Save changes";
        }
    });

    document.querySelectorAll("[data-pref]").forEach((toggle) => {
        toggle.addEventListener("change", async () => {
            const label = toggle.closest(".switch-row").querySelector(".switch-title").textContent;
            try {
                await post("/Client/Preference", { pref: toggle.dataset.pref, enabled: toggle.checked });
                toast(`${label} ${toggle.checked ? "on" : "off"}`);
            } catch (err) {
                toggle.checked = !toggle.checked;
                toast(err.message);
            }
        });
    });
})();
