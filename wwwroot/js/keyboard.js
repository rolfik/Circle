window.circleKeyboard = {
    register: function (dotnetRef, mappingJson) {
        if (window._circleKeyHandler) {
            window.removeEventListener('keydown', window._circleKeyHandler);
        }
        // mappingJson: { "ArrowLeft": "prev", " ": "fullscreen", ... }
        // Accept friendly aliases in configuration so users can write "Space",
        // "Spacebar", "Esc", "Plus" etc. instead of the raw KeyboardEvent.key value.
        const aliases = {
            "Space": " ",
            "Spacebar": " ",
            "Esc": "Escape",
            "Plus": "+",
            "Minus": "-",
            "Up": "ArrowUp",
            "Down": "ArrowDown",
            "Left": "ArrowLeft",
            "Right": "ArrowRight"
        };
        const raw = JSON.parse(mappingJson || '{}');
        const mapping = {};
        for (const key in raw) {
            const action = raw[key];
            mapping[key] = action;
            if (aliases[key]) mapping[aliases[key]] = action;
            // Also accept case-insensitive single-letter shortcuts (e.g. "s" matches Shift+S too).
            if (key.length === 1) {
                mapping[key.toLowerCase()] = action;
                mapping[key.toUpperCase()] = action;
            }
        }
        window._circleKeyHandler = function (e) {
            if (e.ctrlKey || e.altKey || e.metaKey) return;
            const target = e.target;
            if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)) return;

            // While the page content is zoomed in, arrow keys pan instead of
            // navigating between pages. Falls back to the configured action
            // (prev/next/chapter-first/chapter-last) when at base zoom.
            if (window.circleZoom && window.circleZoom.isZoomed && window.circleZoom.isZoomed()) {
                const step = 60;
                let dx = 0, dy = 0;
                switch (e.key) {
                    case 'ArrowLeft': dx = step; break;
                    case 'ArrowRight': dx = -step; break;
                    case 'ArrowUp': dy = step; break;
                    case 'ArrowDown': dy = -step; break;
                }
                if (dx !== 0 || dy !== 0) {
                    e.preventDefault();
                    window.circleZoom.panBy(dx, dy);
                    return;
                }
            }

            const action = mapping[e.key];
            if (action) {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('OnKeyboardShortcut', action);
            }
        };
        window.addEventListener('keydown', window._circleKeyHandler);

        // Map the dedicated back/forward buttons on a mouse (button 3 = back,
        // button 4 = forward) to the configured prev/next page actions instead
        // of the browser's history navigation. We have to handle both mousedown
        // (where preventDefault stops the browser from navigating) and
        // auxclick (which fires for non-primary buttons).
        if (window._circleMouseHandler) {
            window.removeEventListener('mousedown', window._circleMouseHandler);
            window.removeEventListener('auxclick', window._circleMouseAuxHandler);
        }
        window._circleMouseHandler = function (e) {
            if (e.button !== 3 && e.button !== 4) return;
            e.preventDefault();
            e.stopPropagation();
            const action = e.button === 3 ? 'prev' : 'next';
            dotnetRef.invokeMethodAsync('OnKeyboardShortcut', action);
        };
        window._circleMouseAuxHandler = function (e) {
            if (e.button === 3 || e.button === 4) {
                e.preventDefault();
                e.stopPropagation();
            }
        };
        window.addEventListener('mousedown', window._circleMouseHandler);
        window.addEventListener('auxclick', window._circleMouseAuxHandler);
    },
    unregister: function () {
        if (window._circleKeyHandler) {
            window.removeEventListener('keydown', window._circleKeyHandler);
            window._circleKeyHandler = null;
        }
        if (window._circleMouseHandler) {
            window.removeEventListener('mousedown', window._circleMouseHandler);
            window._circleMouseHandler = null;
        }
        if (window._circleMouseAuxHandler) {
            window.removeEventListener('auxclick', window._circleMouseAuxHandler);
            window._circleMouseAuxHandler = null;
        }
    }
};

// Scrolls the breadcrumb bar so the current (last) item is visible.
// Used on small screens where the full breadcrumb path doesn't fit on one line.
window.circleBreadcrumb = {
    scrollToCurrent: function () {
        const center = document.querySelector('.breadcrumb-bar .nav-bar-center');
        if (!center) return;
        const current = center.querySelector('.crumb-current');
        if (current && typeof current.scrollIntoView === 'function') {
            current.scrollIntoView({ behavior: 'smooth', inline: 'end', block: 'nearest' });
        } else {
            center.scrollLeft = center.scrollWidth;
        }
    }
};

// Scrolls the navigation drawer so the currently selected item is visible
// when the drawer opens. The selected entry is marked with .selected-nav-link.
window.circleDrawer = {
    scrollToSelected: function () {
        // Wait one frame (and a bit) so the MudDrawer open animation has placed
        // the panel and its content has its final scrollHeight.
        const doScroll = () => {
            const selected = document.querySelector('.mud-drawer .selected-nav-link');
            if (!selected) return;
            if (typeof selected.scrollIntoView === 'function') {
                selected.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'nearest' });
            }
        };
        if (typeof requestAnimationFrame === 'function') {
            requestAnimationFrame(() => requestAnimationFrame(doScroll));
        } else {
            setTimeout(doScroll, 50);
        }
    }
};
