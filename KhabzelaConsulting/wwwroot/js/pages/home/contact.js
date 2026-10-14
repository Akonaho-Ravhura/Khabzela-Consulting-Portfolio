// Home — Contact us: form validation + success state, open/closed indicator, single-open FAQ.
// Sends the message to the server, which saves it and emails it to vukani@khabzelaconsulting.co.za.
(function () {
    const form = document.querySelector("[data-contact-form]");
    const success = document.querySelector("[data-contact-success]");
    const submit = form.querySelector("[data-submit]");
    const message = form.querySelector("#message");
    const counter = form.querySelector("[data-char-count]");
    const consent = form.querySelector("[data-consent]");
    const formError = form.querySelector("[data-form-error]");
    const fields = [form.querySelector("#name"), form.querySelector("#email"), form.querySelector("#phone"), message, consent];

    function validate(input) {
        if (input === consent) return consent.checked ? "" : "Please give consent so we can reply.";
        if (input.id === "phone" && input.value && !/^[+\d][\d\s()-]{8,}$/.test(input.value)) return "Enter a valid phone number.";
        if (input.validity.valueMissing) return "This field is required.";
        if (input.validity.typeMismatch) return "Enter a valid email address.";
        if (input.validity.tooShort) return `Please write at least ${input.minLength} characters.`;
        return "";
    }

    function setError(input, msg) {
        const slot = form.querySelector(`[data-error-for="${input.name}"]`);
        input.classList.toggle("is-invalid", !!msg);
        input.setAttribute("aria-invalid", msg ? "true" : "false");
        if (slot) slot.textContent = msg;
    }

    fields.forEach((input) => {
        input.addEventListener("blur", () => setError(input, validate(input)));
        input.addEventListener("input", () => { if (input.classList.contains("is-invalid")) setError(input, validate(input)); });
    });
    consent.addEventListener("change", () => setError(consent, validate(consent)));
    message.addEventListener("input", () => { counter.textContent = message.value.length; });

    form.addEventListener("submit", (e) => {
        e.preventDefault();
        let firstInvalid = null;
        fields.forEach((input) => {
            const msg = validate(input);
            setError(input, msg);
            if (msg && !firstInvalid) firstInvalid = input;
        });
        if (firstInvalid) { firstInvalid.focus(); return; }

        submit.disabled = true;
        submit.textContent = "Sending…";
        formError.textContent = "";
        const data = Object.fromEntries(new FormData(form).entries());
        data.consent = consent.checked;

        Khabzela.post(form.action, data)
            .then(() => {
                document.querySelector("[data-success-name]").textContent = form.querySelector("#name").value.trim().split(" ")[0];
                form.hidden = true;
                success.hidden = false;
                success.focus();
            })
            .catch((err) => {
                // Field errors from the server's validation, if any; otherwise a general message.
                const errors = (err.data && err.data.errors) || {};
                Object.entries(errors).forEach(([name, msg]) => {
                    const input = form.querySelector(`[name="${name}"]`);
                    if (input) setError(input, msg);
                });
                formError.textContent = Object.keys(errors).length ? "Please check the highlighted fields." : err.message;
            })
            .finally(() => {
                submit.disabled = false;
                submit.textContent = "Send message";
            });
    });

    document.querySelector("[data-send-another]").addEventListener("click", () => {
        form.reset();
        counter.textContent = "0";
        submit.disabled = false;
        submit.textContent = "Send message";
        success.hidden = true;
        form.hidden = false;
        form.querySelector("#name").focus();
    });

    // Open / closed indicator (Mon–Fri 08:00–17:00, South African time)
    const status = document.querySelector("[data-open-status]");
    if (status) {
        const parts = new Intl.DateTimeFormat("en-ZA", { timeZone: "Africa/Johannesburg", weekday: "short", hour: "numeric", hour12: false }).formatToParts(new Date());
        const day = parts.find((p) => p.type === "weekday").value;
        const hour = parseInt(parts.find((p) => p.type === "hour").value, 10);
        const open = !["Sat", "Sun"].includes(day) && hour >= 8 && hour < 17;
        status.textContent = open ? "● Open now" : "● Closed — leave a message";
        status.classList.add(open ? "is-open" : "is-closed");
    }

    // Only one FAQ open at a time
    const items = document.querySelectorAll("[data-faq] details");
    items.forEach((d) => d.addEventListener("toggle", () => {
        if (d.open) items.forEach((other) => { if (other !== d) other.open = false; });
    }));
})();
