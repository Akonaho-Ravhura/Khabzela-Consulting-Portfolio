// Account — Forgot password: validate the email before the form posts to the server.
(function () {
    const form = document.querySelector("[data-forgot-form]");
    if (!form) return;

    const email = form.querySelector("#email");
    const error = form.querySelector('[data-error-for="email"]');

    form.addEventListener("submit", (e) => {
        let msg = "";
        if (email.validity.valueMissing) msg = "This field is required.";
        else if (email.validity.typeMismatch) msg = "Enter a valid email address.";

        email.classList.toggle("is-invalid", !!msg);
        error.textContent = msg;
        if (msg) { e.preventDefault(); email.focus(); }
    });
})();
