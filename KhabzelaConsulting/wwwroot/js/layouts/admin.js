// Admin layout — off-canvas sidebar for tablet / mobile.
(function () {
    const sidebar = document.getElementById("admin-sidebar");
    const scrim = document.querySelector(".admin-scrim");
    const openBtn = document.querySelector("[data-sidebar-open]");
    if (!sidebar || !openBtn) return;

    function setOpen(open) {
        sidebar.classList.toggle("is-open", open);
        scrim.hidden = !open;
        openBtn.setAttribute("aria-expanded", String(open));
        document.body.style.overflow = open ? "hidden" : "";
        if (open) sidebar.querySelector(".admin-nav a")?.focus();
    }

    openBtn.addEventListener("click", () => setOpen(true));
    document.querySelectorAll("[data-sidebar-close]").forEach((el) => el.addEventListener("click", () => setOpen(false)));
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && sidebar.classList.contains("is-open")) {
            setOpen(false);
            openBtn.focus();
        }
    });
    window.matchMedia("(min-width: 1024px)").addEventListener("change", (e) => { if (e.matches) setOpen(false); });
})();
