// Zoom & pan controller for page content.
//
// - Mouse wheel zooms toward the cursor; left-button drag pans (only when zoomed).
// - Two-finger pinch zooms; one-finger drag pans only when zoomed (so a normal
//   horizontal swipe at zoom 1 keeps working as page navigation).
// - Double-tap toggles 1x <-> 2x at the tap point.
// - Keyboard helpers: zoomIn(), zoomOut(), reset().
// State is kept on a single attached element (the page content root); attaching
// a new element automatically detaches the previous one and resets state.
window.circleZoom = (function () {
    let el = null;
    let scale = 1, tx = 0, ty = 0;
    const MIN = 1, MAX = 6;
    const pointers = new Map();
    let pinchStartDist = 0, pinchStartScale = 1;
    let lastTapTime = 0, lastTapX = 0, lastTapY = 0;

    function apply() {
        if (!el) return;
        el.style.transform = `translate3d(${tx}px, ${ty}px, 0) scale(${scale})`;
        document.body.dataset.zoomed = scale > 1.01 ? '1' : '0';
    }

    function clampToBaseIfMin() {
        if (scale <= MIN + 0.001) { scale = MIN; tx = 0; ty = 0; }
    }

    function zoomAt(targetScale, cx, cy) {
        if (!el) return;
        const next = Math.min(MAX, Math.max(MIN, targetScale));
        const rect = el.getBoundingClientRect();
        // Pre-transform local coordinate of the focal point.
        const localX = (cx - rect.left - tx) / scale;
        const localY = (cy - rect.top - ty) / scale;
        scale = next;
        tx = cx - rect.left - localX * scale;
        ty = cy - rect.top - localY * scale;
        clampToBaseIfMin();
        apply();
    }

    function centerOfElement() {
        const r = el.getBoundingClientRect();
        return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }

    function onWheel(e) {
        if (!el) return;
        e.preventDefault();
        const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
        zoomAt(scale * factor, e.clientX, e.clientY);
    }

    function onPointerDown(e) {
        if (!el) return;
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        try { el.setPointerCapture(e.pointerId); } catch { /* ignore */ }
        pointers.set(e.pointerId, {
            x: e.clientX, y: e.clientY,
            sx: e.clientX, sy: e.clientY,
            type: e.pointerType
        });
        if (pointers.size === 2) {
            const [p1, p2] = [...pointers.values()];
            pinchStartDist = Math.hypot(p2.x - p1.x, p2.y - p1.y) || 1;
            pinchStartScale = scale;
        }
    }

    function onPointerMove(e) {
        const prev = pointers.get(e.pointerId);
        if (!prev) return;
        const dx = e.clientX - prev.x;
        const dy = e.clientY - prev.y;
        prev.x = e.clientX; prev.y = e.clientY;

        if (pointers.size === 1) {
            // Pan only when zoomed in - at scale 1 we let the global swipe
            // handler navigate between pages instead.
            if (scale > MIN + 0.001) {
                tx += dx; ty += dy;
                apply();
                if (e.cancelable) e.preventDefault();
            }
        } else if (pointers.size === 2) {
            const [p1, p2] = [...pointers.values()];
            const dist = Math.hypot(p2.x - p1.x, p2.y - p1.y) || 1;
            const cx = (p1.x + p2.x) / 2;
            const cy = (p1.y + p2.y) / 2;
            zoomAt(pinchStartScale * (dist / pinchStartDist), cx, cy);
            if (e.cancelable) e.preventDefault();
        }
    }

    function onPointerUp(e) {
        const p = pointers.get(e.pointerId);
        if (p) {
            // Double-tap toggle for touch input.
            if (p.type === 'touch'
                && Math.abs(e.clientX - p.sx) < 10
                && Math.abs(e.clientY - p.sy) < 10) {
                const now = Date.now();
                if (now - lastTapTime < 350
                    && Math.abs(e.clientX - lastTapX) < 30
                    && Math.abs(e.clientY - lastTapY) < 30) {
                    if (scale > MIN + 0.001) {
                        scale = MIN; tx = 0; ty = 0; apply();
                    } else {
                        zoomAt(2, e.clientX, e.clientY);
                    }
                    lastTapTime = 0;
                } else {
                    lastTapTime = now;
                    lastTapX = e.clientX; lastTapY = e.clientY;
                }
            }
            pointers.delete(e.pointerId);
        }
    }

    return {
        attach: function (target) {
            this.detach();
            if (!target) return;
            el = target;
            scale = 1; tx = 0; ty = 0;
            el.style.transformOrigin = '0 0';
            el.style.willChange = 'transform';
            // We handle pinch & pan ourselves so we can keep them confined to the
            // page content (drawer / breadcrumb / floating buttons stay at 1:1).
            el.style.touchAction = 'none';
            el.addEventListener('wheel', onWheel, { passive: false });
            el.addEventListener('pointerdown', onPointerDown);
            el.addEventListener('pointermove', onPointerMove);
            el.addEventListener('pointerup', onPointerUp);
            el.addEventListener('pointercancel', onPointerUp);
            apply();
        },
        detach: function () {
            if (!el) return;
            el.removeEventListener('wheel', onWheel);
            el.removeEventListener('pointerdown', onPointerDown);
            el.removeEventListener('pointermove', onPointerMove);
            el.removeEventListener('pointerup', onPointerUp);
            el.removeEventListener('pointercancel', onPointerUp);
            el.style.transform = '';
            el = null;
            pointers.clear();
            scale = 1; tx = 0; ty = 0;
            document.body.dataset.zoomed = '0';
        },
        panBy: function (dx, dy) {
            if (!el) return;
            tx += dx; ty += dy; apply();
        },
        reset: function () {
            scale = 1; tx = 0; ty = 0; apply();
        },
        zoomIn: function () {
            if (!el) return;
            const c = centerOfElement();
            zoomAt(scale * 1.25, c.x, c.y);
        },
        zoomOut: function () {
            if (!el) return;
            const c = centerOfElement();
            zoomAt(scale / 1.25, c.x, c.y);
        },
        isZoomed: function () { return scale > 1.01; }
    };
})();
