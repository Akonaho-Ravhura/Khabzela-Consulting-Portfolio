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

    // POST form-encoded data with the anti-forgery token; resolves with the JSON response or throws an
    // Error whose message is safe to show to the user.
    async function post(url, data) {
        const token = document.querySelector('meta[name="csrf-token"]')?.content || "";
        const body = new URLSearchParams();
        Object.entries(data || {}).forEach(([k, v]) => body.append(k, v == null ? "" : String(v)));
        let res;
        try {
            res = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                headers: { "RequestVerificationToken": token, "Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json" },
                body
            });
        } catch {
            throw new Error("You seem to be offline. Check your connection and try again.");
        }
        let json = {};
        try { json = await res.json(); } catch { /* empty or non-JSON body */ }
        if (res.status === 401 || (res.redirected && res.url.includes("/Account/Login"))) {
            window.location.href = "/Account/Login?returnUrl=" + encodeURIComponent(location.pathname);
            throw new Error("Please sign in again.");
        }
        if (!res.ok) {
            const err = new Error(json.error || (res.status === 429
                ? "Too many attempts — please wait a few minutes and try again."
                : "Something went wrong on our side. Please try again."));
            err.status = res.status;
            err.data = json;
            throw err;
        }
        return json;
    }

    // Show a toast passed from the server after a redirect (TempData["Toast"]).
    document.addEventListener("DOMContentLoaded", () => {
        const flash = document.querySelector("[data-flash-toast]");
        if (flash && flash.dataset.flashToast) toast(flash.dataset.flashToast);
    });

    return { formatRand, toast, initChoiceGroup, store, post };
})();
