// Account — Forgot password: validate email, then show the confirmation step.
// TODO (backend): POST the email to a reset endpoint instead of only switching steps.
(function () {
    const form = document.querySelector("[data-forgot-form]");
    if (!form) return;

    const email = form.querySelector("#email");
    const error = form.querySelector('[data-error-for="email"]');

    form.addEventListener("submit", (e) => {
        e.preventDefault();
        let msg = "";
        if (email.validity.valueMissing) msg = "This field is required.";
        else if (email.validity.typeMismatch) msg = "Enter a valid email address.";

        email.classList.toggle("is-invalid", !!msg);
        error.textContent = msg;
        if (msg) { email.focus(); return; }

        document.querySelector("[data-sent-email]").textContent = email.value;
        document.querySelector('[data-forgot-step="request"]').hidden = true;
        document.querySelector('[data-forgot-step="sent"]').hidden = false;
    });
})();
