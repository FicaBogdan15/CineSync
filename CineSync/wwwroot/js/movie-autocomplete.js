document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("movieSearchInput");
    const box = document.getElementById("movieSuggestionsBox");

    if (!input || !box) return;

    let debounceTimer = null;
    let activeIndex = -1;
    let currentSuggestions = [];

    function clearSuggestions() {
        box.innerHTML = "";
        box.classList.add("d-none");
        activeIndex = -1;
        currentSuggestions = [];
    }

    function renderSuggestions(items) {
        if (!items || items.length === 0) {
            clearSuggestions();
            return;
        }

        currentSuggestions = items;
        activeIndex = -1;

        box.innerHTML = items.map((item, index) => `
            <a href="/Movie/Details/${item.movieId}"
               class="cs-suggestion-item"
               data-index="${index}">
                <span class="cs-suggestion-title">${escapeHtml(item.title)}</span>
                <span class="cs-suggestion-meta">
                    ${escapeHtml(item.categoryName ?? "Unknown category")} • ${item.year}
                </span>
            </a>
        `).join("");

        box.classList.remove("d-none");

        const links = box.querySelectorAll(".cs-suggestion-item");
        links.forEach(link => {
            link.addEventListener("mouseenter", function () {
                setActiveItem(parseInt(link.dataset.index));
            });
        });
    }

    function setActiveItem(index) {
        const items = box.querySelectorAll(".cs-suggestion-item");
        items.forEach(i => i.classList.remove("active"));

        if (index >= 0 && index < items.length) {
            items[index].classList.add("active");
            activeIndex = index;
        } else {
            activeIndex = -1;
        }
    }

    async function fetchSuggestions(term) {
        try {
            const response = await fetch(`/Movie/Suggestions?term=${encodeURIComponent(term)}`);
            if (!response.ok) {
                clearSuggestions();
                return;
            }

            const data = await response.json();
            renderSuggestions(data);
        } catch {
            clearSuggestions();
        }
    }

    input.addEventListener("input", function () {
        const term = input.value.trim();

        clearTimeout(debounceTimer);

        if (term.length < 2) {
            clearSuggestions();
            return;
        }

        debounceTimer = setTimeout(() => {
            fetchSuggestions(term);
        }, 200);
    });

    input.addEventListener("keydown", function (e) {
        const items = box.querySelectorAll(".cs-suggestion-item");
        if (!items.length) return;

        if (e.key === "ArrowDown") {
            e.preventDefault();
            const nextIndex = activeIndex < items.length - 1 ? activeIndex + 1 : 0;
            setActiveItem(nextIndex);
        }

        if (e.key === "ArrowUp") {
            e.preventDefault();
            const prevIndex = activeIndex > 0 ? activeIndex - 1 : items.length - 1;
            setActiveItem(prevIndex);
        }

        if (e.key === "Enter") {
            if (activeIndex >= 0 && activeIndex < items.length) {
                e.preventDefault();
                window.location.href = items[activeIndex].href;
            }
        }

        if (e.key === "Escape") {
            clearSuggestions();
        }
    });

    document.addEventListener("click", function (e) {
        if (!box.contains(e.target) && e.target !== input) {
            clearSuggestions();
        }
    });

    function escapeHtml(text) {
        return text
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#039;");
    }
});
