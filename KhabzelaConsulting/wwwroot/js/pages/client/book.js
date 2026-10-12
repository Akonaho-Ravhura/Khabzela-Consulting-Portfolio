// Client — Book appointment: day / time / consultant selection and confirmation.
// Confirming POSTs the quote answers + slot; the server recalculates the estimate and saves both.
(function () {
    const { formatRand, store, toast, post } = Khabzela;
    const booked = JSON.parse(document.getElementById("booked-slots").textContent);
    const quote = store.get("kc.quote");

    const dayGroup = document.querySelector("[data-day-group]");
    const timeGroup = document.querySelector("[data-time-group]");
    const consultantGroup = document.querySelector("[data-consultant-group]");
    const dayLabel = document.querySelector("[data-day-label]");
    const confirmBtn = document.querySelector("[data-confirm]");

    const state = {
        day: dayGroup.querySelector(".is-active").dataset.value,
        dayText: dayGroup.querySelector(".is-active").dataset.label,
        time: null,
        consultant: consultantGroup.querySelector(".is-active")?.dataset.value,
        consultantName: consultantGroup.querySelector(".is-active")?.dataset.name
    };
    const errorEl = document.querySelector("[data-book-error]");

    const estimateText = quote && quote.min != null
        ? `${formatRand(quote.min)} – ${formatRand(quote.max)}/mo`
        : "Priced on consultation";

    if (quote) {
        document.querySelector("[data-quote-summary]").textContent =
            `Your ${quote.serviceLabel} estimate (${estimateText}) is saved — pick a time to confirm it with our team.`;
    }

    // Radio-group behaviour for a set of buttons, with arrow-key support.
    function radioGroup(group, selector, onSelect) {
        const items = () => Array.from(group.querySelectorAll(selector));
        function select(btn, focus) {
            items().forEach((b) => {
                const on = b === btn;
                b.classList.toggle("is-active", on);
                b.setAttribute("aria-checked", String(on));
                b.tabIndex = on ? 0 : -1;
            });
            if (focus) btn.focus();
            onSelect(btn);
        }
        group.addEventListener("click", (e) => {
            const btn = e.target.closest(selector);
            if (btn && !btn.disabled) select(btn, false);
        });
        group.addEventListener("keydown", (e) => {
            if (!["ArrowRight", "ArrowLeft", "ArrowDown", "ArrowUp"].includes(e.key)) return;
            const enabled = items().filter((b) => !b.disabled);
            const idx = enabled.indexOf(document.activeElement);
            const step = e.key === "ArrowRight" || e.key === "ArrowDown" ? 1 : -1;
            const next = enabled[(idx + step + enabled.length) % enabled.length];
            if (next) { e.preventDefault(); select(next, true); }
        });
        const active = group.querySelector(selector + ".is-active") || items().find((b) => !b.disabled);
        items().forEach((b) => { b.tabIndex = b === active ? 0 : -1; });
    }

    function renderTimes() {
        const taken = (booked[state.consultant] || {})[state.day] || [];
        timeGroup.querySelectorAll(".choice").forEach((btn) => {
            const isTaken = taken.includes(btn.dataset.value);
            btn.disabled = isTaken;
            btn.setAttribute("aria-label", isTaken ? `${btn.dataset.value} (unavailable)` : btn.dataset.value);
            if (isTaken && btn.dataset.value === state.time) state.time = null;
            const on = btn.dataset.value === state.time;
            btn.classList.toggle("is-active", on);
            btn.setAttribute("aria-checked", String(on));
        });
        updateConfirm();
    }

    function updateConfirm() {
        confirmBtn.disabled = !state.time;
        confirmBtn.textContent = state.time ? `Confirm appointment — ${state.dayText}, ${state.time}` : "Select a time";
    }

    radioGroup(dayGroup, ".choice", (btn) => {
        state.day = btn.dataset.value;
        state.dayText = btn.dataset.label;
        dayLabel.textContent = btn.dataset.label;
        renderTimes();
    });
    radioGroup(timeGroup, ".choice", (btn) => { state.time = btn.dataset.value; updateConfirm(); });
    radioGroup(consultantGroup, ".book-consultant", (btn) => {
        state.consultant = btn.dataset.value;
        state.consultantName = btn.dataset.name;
        renderTimes();
    });

    confirmBtn.addEventListener("click", async () => {
        if (!state.time) return;
        if (!quote) {
            errorEl.textContent = "Your quote has expired — please get a quote first.";
            return;
        }
        errorEl.textContent = "";
        confirmBtn.disabled = true;
        confirmBtn.textContent = "Booking…";

        try {
            const inputs = quote.inputs || {};
            const result = await post("/Client/Book", {
                service: quote.service,
                employees: inputs.employees,
                frequency: inputs.frequency,
                bracket: (inputs.bracket || {})[quote.service] || "lt1m",
                day: state.day,
                time: state.time,
                consultantId: state.consultant,
                notes: document.getElementById("notes").value
            });

            store.set("kc.quote", null);
            document.querySelector("[data-done-text]").textContent =
                `We've added a confirmation to your notifications. ${result.with} will see your estimate before the meeting.`;
            document.querySelector("[data-done-when]").textContent = result.when;
            document.querySelector("[data-done-with]").textContent = result.with;
            document.querySelector("[data-done-service]").textContent = result.service;
            document.querySelector("[data-done-estimate]").textContent = result.estimate;

            document.querySelector('[data-book-step="pick"]').hidden = true;
            document.querySelector('[data-book-step="done"]').hidden = false;
            window.scrollTo({ top: 0 });
            toast("Appointment confirmed");
        } catch (err) {
            errorEl.textContent = err.message;
            if (err.status === 409) {
                // Someone else took the slot: mark it as unavailable.
                booked[state.consultant] = booked[state.consultant] || {};
                (booked[state.consultant][state.day] = booked[state.consultant][state.day] || []).push(state.time);
                state.time = null;
                renderTimes();
            } else {
                updateConfirm();
            }
        }
    });

    if (!quote) {
        document.querySelector("[data-quote-summary]").innerHTML =
            'Start with a quote so we can prepare for your meeting. <a class="btn-link" href="/Client/Quote">Get a quote →</a>';
    }

    renderTimes();
})();
