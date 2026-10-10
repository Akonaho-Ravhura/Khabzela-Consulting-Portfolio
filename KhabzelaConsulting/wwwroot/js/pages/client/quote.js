// Client — Get a quote: live price estimate from the pricing rules, then hand off to booking.
(function () {
    const rules = JSON.parse(document.getElementById("pricing-rules").textContent);
    const { formatRand, initChoiceGroup, store } = Khabzela;

    const state = {
        service: "payroll",
        employees: 12,
        frequency: "monthly",
        bracket: { accounting: "lt1m", tax: "lt1m" },
        cover: "business"
    };

    const labels = {
        services: { accounting: "Accounting", payroll: "Payroll", tax: "Tax", insurance: "Insurance advice" },
        brackets: { lt1m: "turnover under R1m", "1to5m": "turnover R1m – R5m", "5to20m": "turnover R5m – R20m" },
        cover: { business: "business cover", life: "life cover", "short-term": "short-term cover" }
    };

    const range = document.querySelector("[data-employees-range]");
    const number = document.querySelector("[data-employees-input]");
    const estimateEl = document.querySelector("[data-estimate]");
    const detailEl = document.querySelector("[data-estimate-detail]");

    function calculate() {
        switch (state.service) {
            case "payroll": {
                const r = rules.payroll;
                const surcharge = state.frequency === "monthly" ? 0 : r.weeklySurcharge;
                return {
                    min: r.base + r.perEmployeeMin * state.employees + surcharge,
                    max: r.base + r.perEmployeeMax * state.employees + surcharge,
                    detail: `${state.employees} employee${state.employees === 1 ? "" : "s"} · ${state.frequency === "both" ? "weekly & monthly" : state.frequency}`
                };
            }
            case "accounting":
            case "tax": {
                const [min, max] = rules[state.service].brackets[state.bracket[state.service]];
                return { min, max, detail: labels.brackets[state.bracket[state.service]] };
            }
            default:
                return { min: null, max: null, detail: labels.cover[state.cover] };
        }
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
    initChoiceGroup(document.querySelector("[data-cover-group]"), (value) => { state.cover = value; render(); });
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
