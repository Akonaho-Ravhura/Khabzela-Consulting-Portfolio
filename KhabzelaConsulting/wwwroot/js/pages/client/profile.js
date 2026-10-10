// Client — Profile: enable Save only when details change; notification preference toggles.
// TODO (backend): POST profile changes and preferences.
(function () {
    const { toast } = Khabzela;
    const form = document.querySelector("[data-profile-form]");
    const save = form.querySelector("[data-save]");
    const inputs = Array.from(form.querySelectorAll(".input"));
    let initial = inputs.map((i) => i.value);

    function isDirty() { return inputs.some((i, idx) => i.value !== initial[idx]); }

    form.addEventListener("input", () => { save.disabled = !isDirty(); });

    form.addEventListener("submit", (e) => {
        e.preventDefault();
        const invalid = inputs.find((i) => !i.checkValidity());
        inputs.forEach((i) => i.classList.toggle("is-invalid", !i.checkValidity()));
        if (invalid) { invalid.focus(); return; }
        initial = inputs.map((i) => i.value);
        save.disabled = true;
        toast("Profile updated");
    });

    document.querySelectorAll("[data-pref]").forEach((toggle) => {
        toggle.addEventListener("change", () => {
            const label = toggle.closest(".switch-row").querySelector(".switch-title").textContent;
            toast(`${label} ${toggle.checked ? "on" : "off"}`);
        });
    });
})();
