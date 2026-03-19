document.addEventListener("DOMContentLoaded", function () {
    const images = document.querySelectorAll(".cs-image-shell img");

    images.forEach(function (img) {
        const shell = img.closest(".cs-image-shell");
        if (!shell) {
            return;
        }

        // Images are visible immediately; keep the class only for compatibility hooks.
        const markLoaded = function () {
            shell.classList.add("is-loaded");
        };

        if (!img.getAttribute("src")) {
            markLoaded();
        } else if (img.complete) {
            markLoaded();
        } else {
            img.addEventListener("load", markLoaded, { once: true });
            img.addEventListener("error", markLoaded, { once: true });
        }
    });
});
