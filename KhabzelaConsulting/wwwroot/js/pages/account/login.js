// Account — Login: password visibility toggle + client-side validation.
(function () {
    const form = document.querySelector("[data-login-form]");
    if (!form) return;

    const toggle = form.querySelector("[data-toggle-password]");
    const password = form.querySelector("#password");
    password.dataset.password = "";

    toggle.addEventListener("click", () => {
        const show = password.type === "password";
        password.type = show ? "text" : "password";
        toggle.setAttribute("aria-pressed", String(show));
        toggle.setAttribute("aria-label", show ? "Hide password" : "Show password");
    });

    function setError(input, message) {
        const slot = form.querySelector(`[data-error-for="${input.name}"]`);
        input.classList.toggle("is-invalid", !!message);
        input.setAttribute("aria-invalid", message ? "true" : "false");
        if (slot) slot.textContent = message || "";
    }

    function validate(input) {
        if (input.validity.valueMissing) return "This field is required.";
        if (input.validity.typeMismatch) return "Enter a valid email address.";
        if (input.validity.tooShort) return `Use at least ${input.minLength} characters.`;
        return "";
    }

    form.querySelectorAll(".input").forEach((input) => {
        input.addEventListener("blur", () => setError(input, validate(input)));
        input.addEventListener("input", () => { if (input.classList.contains("is-invalid")) setError(input, validate(input)); });
    });

    form.addEventListener("submit", (e) => {
        let firstInvalid = null;
        form.querySelectorAll(".input").forEach((input) => {
            const msg = validate(input);
            setError(input, msg);
            if (msg && !firstInvalid) firstInvalid = input;
        });
        if (firstInvalid) {
            e.preventDefault();
            firstInvalid.focus();
        }
    });
})();
