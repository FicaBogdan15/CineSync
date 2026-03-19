document.addEventListener("DOMContentLoaded", function () {
    const body = document.body;
    const btn = document.getElementById("themeToggle");

    const savedTheme = localStorage.getItem("cinesync-theme");
    if (savedTheme === "dark") {
        body.classList.add("dark-mode");
    }

    const syncLabel = function () {
        if (!btn) {
            return;
        }

        btn.textContent = body.classList.contains("dark-mode") ? "Light" : "Dark";
    };

    syncLabel();

    if (btn) {
        btn.addEventListener("click", function () {
            body.classList.toggle("dark-mode");

            const isDark = body.classList.contains("dark-mode");
            localStorage.setItem("cinesync-theme", isDark ? "dark" : "light");
            syncLabel();
        });
    }
});
