// Account — Register: validation, password strength meter, confirm-password match.
(function () {
    const form = document.querySelector("[data-register-form]");
    if (!form) return;

    const password = form.querySelector("#password");
    const confirm = form.querySelector("#confirmPassword");
    const terms = form.querySelector("[data-terms]");
    const meter = form.querySelector(".strength");

    function score(value) {
        let s = 0;
        if (value.length >= 8) s++;
        if (/[A-Z]/.test(value) && /[a-z]/.test(value)) s++;
        if (/\d/.test(value)) s++;
        if (/[^A-Za-z0-9]/.test(value)) s++;
        return value ? Math.max(1, s) : 0;
    }

    function validate(input) {
        if (input === terms) return terms.checked ? "" : "Please accept the terms to continue.";
        if (input.validity.valueMissing) return "This field is required.";
        if (input.validity.typeMismatch) return "Enter a valid email address.";
        if (input.validity.patternMismatch) return "Enter a valid South African mobile number.";
        if (input.validity.tooShort) return `Use at least ${input.minLength} characters.`;
        if (input === confirm && confirm.value !== password.value) return "Passwords don't match.";
        return "";
    }

    function setError(input, message) {
        const slot = form.querySelector(`[data-error-for="${input.name}"]`);
        input.classList.toggle("is-invalid", !!message);
        input.setAttribute("aria-invalid", message ? "true" : "false");
        if (slot) slot.textContent = message;
    }

    const fields = [...form.querySelectorAll(".input"), terms];

    fields.forEach((input) => {
        input.addEventListener("blur", () => setError(input, validate(input)));
        input.addEventListener("input", () => { if (input.classList.contains("is-invalid")) setError(input, validate(input)); });
    });
    terms.addEventListener("change", () => setError(terms, validate(terms)));

    password.addEventListener("input", () => {
        meter.dataset.score = score(password.value);
        if (confirm.value) setError(confirm, validate(confirm));
    });

    form.addEventListener("submit", (e) => {
        let firstInvalid = null;
        fields.forEach((input) => {
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
