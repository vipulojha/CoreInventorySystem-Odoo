(() => {
    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const sourceSelects = [...document.querySelectorAll("[data-location-source]")];

    const syncLocationOptions = (sourceSelect) => {
        const form = sourceSelect.closest("form") || document;
        const selectedWarehouse = sourceSelect.selectedOptions[0]?.dataset.code || "";
        const locationSelects = [...form.querySelectorAll("[data-location-target]")];

        locationSelects.forEach((locationSelect) => {
            const options = [...locationSelect.options];
            let hasVisibleSelection = false;

            options.forEach((option) => {
                if (!option.value) {
                    option.hidden = false;
                    option.disabled = false;
                    return;
                }

                const visible = !selectedWarehouse || option.dataset.group === selectedWarehouse;
                option.hidden = !visible;
                option.disabled = !visible;

                if (visible && option.selected) {
                    hasVisibleSelection = true;
                }
            });

            if (!hasVisibleSelection) {
                locationSelect.value = "";
            }
        });
    };

    sourceSelects.forEach((select) => {
        syncLocationOptions(select);
        select.addEventListener("change", () => syncLocationOptions(select));
    });

    const table = document.querySelector("#line-items-table");
    const addButton = document.querySelector("[data-add-line]");
    const template = document.querySelector("#line-row-template");

    if (table && addButton && template) {
        const reindex = () => {
            const rows = [...table.querySelectorAll("tbody tr")];
            rows.forEach((row, index) => {
                row.querySelectorAll("[name]").forEach(field => {
                    field.name = field.name.replace(/Lines\[\d+\]/g, `Lines[${index}]`);
                });
            });
            table.dataset.lineCount = String(rows.length);
        };

        addButton.addEventListener("click", () => {
            const tbody = table.querySelector("tbody");
            const index = Number(table.dataset.lineCount || "0");
            const html = template.innerHTML.replaceAll("__index__", String(index));
            tbody.insertAdjacentHTML("beforeend", html);
            reindex();
        });

        table.addEventListener("click", event => {
            const button = event.target.closest("[data-remove-line]");
            if (!button) {
                return;
            }

            const rows = table.querySelectorAll("tbody tr");
            if (rows.length <= 1) {
                return;
            }

            button.closest("tr")?.remove();
            reindex();
        });
    }

    const revealItems = [...document.querySelectorAll("[data-reveal]")];
    if (!prefersReducedMotion && "IntersectionObserver" in window && revealItems.length > 0) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) {
                    return;
                }

                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.12 });

        revealItems.forEach((item, index) => {
            item.style.setProperty("--reveal-delay", `${Math.min(index * 70, 420)}ms`);
            observer.observe(item);
        });
    } else {
        revealItems.forEach((item) => item.classList.add("is-visible"));
    }

    const supportsFinePointer = window.matchMedia("(pointer: fine)").matches;
    const motionSurfaces = [...document.querySelectorAll(".topbar, .brand, .user-chip, .panel, .hero-banner, .stat-card, .board-column, .board-card, .list-card")];

    if (!prefersReducedMotion && supportsFinePointer) {
        motionSurfaces.forEach((surface) => {
            surface.addEventListener("pointermove", (event) => {
                const rect = surface.getBoundingClientRect();
                const x = ((event.clientX - rect.left) / rect.width) * 100;
                const y = ((event.clientY - rect.top) / rect.height) * 100;
                surface.style.setProperty("--spotlight-x", `${x.toFixed(2)}%`);
                surface.style.setProperty("--spotlight-y", `${y.toFixed(2)}%`);
            });

            surface.addEventListener("pointerleave", () => {
                surface.style.removeProperty("--spotlight-x");
                surface.style.removeProperty("--spotlight-y");
            });
        });
    }
})();
