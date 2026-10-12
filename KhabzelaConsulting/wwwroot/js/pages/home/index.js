// Home — stat counters and the animated quote card in the hero.
(function () {
    const { formatRand } = Khabzela;
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // Count up hero stats once visible
    const counters = document.querySelectorAll("[data-count-to]");
    function countUp(el) {
        const target = parseInt(el.dataset.countTo, 10);
        const start = performance.now();
        function frame(now) {
            const t = Math.min(1, (now - start) / 1200);
            el.textContent = Math.round(target * (1 - Math.pow(1 - t, 3)));
            if (t < 1) requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
    }
    if (!reduceMotion && "IntersectionObserver" in window) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach((e) => { if (e.isIntersecting) { countUp(e.target); io.unobserve(e.target); } });
        }, { threshold: 0.6 });
        counters.forEach((el) => io.observe(el));
    }

    // Hero quote card: cycle through a few employee counts using the payroll pricing formula.
    const figure = document.querySelector("[data-hero-estimate]");
    const employeesEl = document.querySelector("[data-hero-employees]");
    const bar = document.querySelector("[data-hero-bar]");
    if (figure && !reduceMotion) {
        const samples = [12, 25, 40, 8];
        let i = 0;
        setInterval(() => {
            i = (i + 1) % samples.length;
            const n = samples[i];
            const min = 250 + 50 * n, max = 250 + 100 * n;
            figure.innerHTML = `${formatRand(min)} – ${formatRand(max)}<span>/mo</span>`;
            employeesEl.textContent = n;
            bar.style.width = Math.max(4, (n / 50) * 100) + "%";
        }, 3200);
    }
})();
