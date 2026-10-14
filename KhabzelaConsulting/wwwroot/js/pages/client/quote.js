// Client — Get a quote: live price estimate from the pricing rules, then hand off to booking.
// This is a preview only; the server recalculates from the same rules when the booking is confirmed.
(function () {
    const rules = JSON.parse(document.getElementById("pricing-rules").textContent);
    const { formatRand, initChoiceGroup, store } = Khabzela;

    const state = {
        service: "payroll",
        employees: 12,
        frequency: "monthly",
        bracket: { accounting: "lt1m", tax: "lt1m" }
    };

    const labels = {
        services: { accounting: "Accounting", payroll: "Payroll", tax: "Tax" },
        brackets: { lt1m: "turnover under R1m", "1to5m": "turnover R1m – R5m", "5to20m": "turnover R5m – R20m" }
    };

    const range = document.querySelector("[data-employees-range]");
    const number = document.querySelector("[data-employees-input]");
    const estimateEl = document.querySelector("[data-estimate]");
    const detailEl = document.querySelector("[data-estimate-detail]");

    // Rules are [min, max] pairs; null means the rule is in Draft, so there's no automatic estimate.
    function calculate() {
        if (state.service === "payroll") {
            const r = rules.payroll;
            const detail = `${state.employees} employee${state.employees === 1 ? "" : "s"} · ${state.frequency === "both" ? "weekly & monthly" : state.frequency}`;
            if (!r.perEmployee) return { min: null, max: null, detail };
            const base = r.base || [0, 0];
            const surcharge = state.frequency === "monthly" || !r.weeklySurcharge ? [0, 0] : r.weeklySurcharge;
            return {
                min: base[0] + r.perEmployee[0] * state.employees + surcharge[0],
                max: base[1] + r.perEmployee[1] * state.employees + surcharge[1],
                detail
            };
        }
        const range = rules[state.service].brackets[state.bracket[state.service]];
        const detail = labels.brackets[state.bracket[state.service]];
        return range ? { min: range[0], max: range[1], detail } : { min: null, max: null, detail };
    }

    function render() {
        const q = calculate();
        estimateEl.textContent = q.min == null
            ? "Priced on consultation"
            : `${formatRand(q.min)} – ${formatRand(q.max)} / mo`;
        detailEl.textContent = `${labels.services[state.service]} · ${q.detail}`;
        return q;
    }

    function setEmployees(value) {
        const n = Math.min(200, Math.max(1, parseInt(value, 10) || 1));
        state.employees = n;
        range.value = n;
        if (document.activeElement !== number) number.value = n;
        range.style.setProperty("--fill", ((n - 1) / (range.max - range.min)) * 100 + "%");
        render();
    }

    initChoiceGroup(document.querySelector("[data-service-group]"), (value) => {
        state.service = value;
        document.querySelectorAll("[data-panel]").forEach((p) => { p.hidden = p.dataset.panel !== value; });
        render();
    });
    initChoiceGroup(document.querySelector("[data-frequency-group]"), (value) => { state.frequency = value; render(); });
    document.querySelectorAll("[data-bracket-group]").forEach((group) => {
        initChoiceGroup(group, (value) => { state.bracket[group.dataset.bracketGroup] = value; render(); });
    });

    range.addEventListener("input", () => setEmployees(range.value));
    number.addEventListener("input", () => { if (number.value) setEmployees(number.value); });
    number.addEventListener("blur", () => { number.value = state.employees; });

    document.querySelector("[data-continue]").addEventListener("click", function () {
        const q = render();
        store.set("kc.quote", {
            service: state.service,
            serviceLabel: labels.services[state.service],
            min: q.min,
            max: q.max,
            detail: q.detail,
            inputs: { ...state }
        });
        window.location.href = this.dataset.href;
    });

    setEmployees(state.employees);
})();
