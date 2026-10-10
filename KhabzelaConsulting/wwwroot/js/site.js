// Khabzela Consulting — shared helpers used by every page.
// Page-specific scripts live in /js/pages/{area}/{page}.js

window.Khabzela = (function () {
    const currency = new Intl.NumberFormat("en-ZA", { maximumFractionDigits: 0 });

    function formatRand(value) {
        return "R " + currency.format(Math.round(value)).replace(/ /g, ",");
    }

    let toastTimer;
    function toast(message) {
        let el = document.querySelector(".toast");
        if (!el) {
            el = document.createElement("div");
            el.className = "toast";
            el.setAttribute("role", "status");
            el.setAttribute("aria-live", "polite");
            document.body.appendChild(el);
        }
        el.textContent = message;
        requestAnimationFrame(() => el.classList.add("is-visible"));
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => el.classList.remove("is-visible"), 2800);
    }

    // Single-select button groups: <div data-choice-group> <button class="choice">…
    function initChoiceGroup(group, onChange) {
        const buttons = Array.from(group.querySelectorAll(".choice"));
        buttons.forEach((btn) => {
            btn.addEventListener("click", () => {
                if (btn.disabled) return;
                buttons.forEach((b) => {
                    b.classList.toggle("is-active", b === btn);
                    b.setAttribute("aria-pressed", b === btn ? "true" : "false");
                });
                if (onChange) onChange(btn.dataset.value, btn);
            });
        });
    }

    // sessionStorage wrapper that never throws (private mode etc.)
    const store = {
        get(key) {
            try { return JSON.parse(sessionStorage.getItem(key)); } catch { return null; }
        },
        set(key, value) {
            try { sessionStorage.setItem(key, JSON.stringify(value)); } catch { /* ignore */ }
        }
    };

    return { formatRand, toast, initChoiceGroup, store };
})();
