// Home — Privacy: highlight the table-of-contents link for the section in view.
(function () {
    const links = Array.from(document.querySelectorAll("[data-toc] a"));
    if (!("IntersectionObserver" in window) || !links.length) return;

    const io = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;
            links.forEach((a) => a.classList.toggle("is-active", a.getAttribute("href") === "#" + entry.target.id));
        });
    }, { rootMargin: "-20% 0px -70% 0px" });

    links.forEach((a) => {
        const target = document.querySelector(a.getAttribute("href"));
        if (target) io.observe(target);
    });
})();
