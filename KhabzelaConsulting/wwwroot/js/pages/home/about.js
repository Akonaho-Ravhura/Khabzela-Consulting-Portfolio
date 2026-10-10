// Home — About us: stagger the reveal of cards and timeline items within each group.
(function () {
    document.querySelectorAll(".values-grid, .team-grid, [data-timeline]").forEach((group) => {
        Array.from(group.children).forEach((el, i) => {
            el.style.transitionDelay = `${i * 90}ms`;
            // Clear the delay after the reveal so hover effects stay instant.
            el.addEventListener("transitionend", () => { el.style.transitionDelay = ""; }, { once: true });
        });
    });
})();
