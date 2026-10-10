// Client — Book appointment: day / time / consultant selection and confirmation.
// TODO (backend): POST the booking (with the saved quote) instead of only showing the confirmation.
(function () {
    const { formatRand, store, toast } = Khabzela;
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
        consultant: consultantGroup.querySelector(".is-active").dataset.value
    };

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
        const taken = booked[state.day] || [];
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
    radioGroup(consultantGroup, ".book-consultant", (btn) => { state.consultant = btn.dataset.value; });

    confirmBtn.addEventListener("click", () => {
        if (!state.time) return;
        const booking = { ...state, notes: document.getElementById("notes").value, quote };
        store.set("kc.booking", booking);

        document.querySelector("[data-done-text]").textContent =
            `We've sent a confirmation to your notifications. ${state.consultant} will see your estimate before the meeting.`;
        document.querySelector("[data-done-when]").textContent = `${state.dayText}, ${state.time}`;
        document.querySelector("[data-done-with]").textContent = state.consultant;
        document.querySelector("[data-done-service]").textContent = quote ? quote.serviceLabel : "General consultation";
        document.querySelector("[data-done-estimate]").textContent = quote ? estimateText : "—";

        document.querySelector('[data-book-step="pick"]').hidden = true;
        document.querySelector('[data-book-step="done"]').hidden = false;
        window.scrollTo({ top: 0 });
        toast("Appointment confirmed");
    });

    renderTimes();
})();
