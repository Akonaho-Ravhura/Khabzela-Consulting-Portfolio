// Client — Home: time-aware greeting and live countdown to the provisional tax deadline.
(function () {
    const greeting = document.querySelector("[data-greeting]");
    if (greeting) {
        const h = new Date().getHours();
        greeting.textContent = h < 12 ? "Good morning," : h < 17 ? "Good afternoon," : "Good evening,";
    }

    const daysEl = document.querySelector("[data-days-left]");
    if (daysEl && daysEl.dataset.due) {
        const due = new Date(daysEl.dataset.due + "T00:00:00");
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const days = Math.round((due - today) / 86400000);
        if (days >= 0) daysEl.textContent = days;
    }
})();
