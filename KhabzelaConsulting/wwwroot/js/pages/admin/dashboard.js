// Admin — Dashboard: count-up animation on stats and clickable table rows.
(function () {
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    document.querySelectorAll("[data-count]").forEach((el) => {
        const raw = el.dataset.count;
        const target = parseInt(raw.replace(/[^\d]/g, ""), 10);
        if (!target || reduceMotion) return;
        const prefix = raw.startsWith("R") ? "R " : "";
        const start = performance.now();
        const duration = 700;
        function frame(now) {
            const t = Math.min(1, (now - start) / duration);
            const value = Math.round(target * (1 - Math.pow(1 - t, 3)));
            el.textContent = prefix + value.toLocaleString("en-US");
            if (t < 1) requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
    });

    document.querySelectorAll("tr[data-href]").forEach((row) => {
        row.addEventListener("click", () => { window.location.href = row.dataset.href; });
    });
})();
