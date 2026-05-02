// Touch helpers for mobile:
//  1) Long-press tooltip: shows the [title] of the pressed element in a floating
//     popup. Suppresses the resulting click so the gesture does not also navigate.
//  2) Horizontal swipe inside the main viewport: triggers prev/next page navigation.
window.circleTouch = {
    _ref: null,
    _tip: null,
    _timer: 0,
    _startX: 0,
    _startY: 0,
    _startT: 0,
    _target: null,
    _suppressNextClick: false,

    register: function (dotnetRef) {
        this.unregister();
        this._ref = dotnetRef;

        // Single floating tooltip element, lazily created.
        if (!this._tip) {
            const t = document.createElement('div');
            t.className = 'touch-tooltip';
            t.setAttribute('role', 'tooltip');
            t.style.display = 'none';
            document.body.appendChild(t);
            this._tip = t;
        }

        document.addEventListener('touchstart', this._onStart, { passive: true });
        document.addEventListener('touchmove', this._onMove, { passive: true });
        document.addEventListener('touchend', this._onEnd, { passive: false });
        document.addEventListener('touchcancel', this._onCancel, { passive: true });

        // Capture-phase click suppressor for the click that fires after a long-press.
        document.addEventListener('click', this._onClickCapture, true);
    },

    unregister: function () {
        document.removeEventListener('touchstart', this._onStart);
        document.removeEventListener('touchmove', this._onMove);
        document.removeEventListener('touchend', this._onEnd);
        document.removeEventListener('touchcancel', this._onCancel);
        document.removeEventListener('click', this._onClickCapture, true);
        this._clearTimer();
        this._hide();
        this._ref = null;
    },

    _findTitled: function (el) {
        while (el && el !== document.body) {
            if (el.getAttribute && el.getAttribute('title')) return el;
            el = el.parentElement;
        }
        return null;
    },

    _onStart: function (e) {
        const self = window.circleTouch;
        if (e.touches.length !== 1) { self._clearTimer(); return; }
        const t = e.touches[0];
        self._startX = t.clientX;
        self._startY = t.clientY;
        self._startT = Date.now();
        self._target = e.target;

        const titled = self._findTitled(e.target);
        if (titled) {
            self._timer = window.setTimeout(function () {
                self._show(titled, t.clientX, t.clientY);
                self._suppressNextClick = true;
                self._timer = 0;
            }, 500);
        }
    },

    _onMove: function (e) {
        const self = window.circleTouch;
        if (e.touches.length !== 1) return;
        const t = e.touches[0];
        if (Math.abs(t.clientX - self._startX) > 10 || Math.abs(t.clientY - self._startY) > 10) {
            self._clearTimer();
        }
    },

    _onEnd: function (e) {
        const self = window.circleTouch;
        self._clearTimer();
        self._hide();

        // Swipe detection: only inside the main viewport, not in the drawer or breadcrumb.
        const t = (e.changedTouches && e.changedTouches[0]) || null;
        if (!t || !self._target) return;
        if (!self._target.closest || !self._target.closest('.app-viewport')) return;

        const dx = t.clientX - self._startX;
        const dy = t.clientY - self._startY;
        const dt = Date.now() - self._startT;
        const absX = Math.abs(dx), absY = Math.abs(dy);
        if (dt < 700 && absX > 60 && absX > absY * 1.6) {
            const action = dx < 0 ? 'next' : 'prev';
            if (self._ref) self._ref.invokeMethodAsync('OnKeyboardShortcut', action);
        }
    },

    _onCancel: function () {
        const self = window.circleTouch;
        self._clearTimer();
        self._hide();
    },

    _onClickCapture: function (e) {
        const self = window.circleTouch;
        if (self._suppressNextClick) {
            self._suppressNextClick = false;
            e.preventDefault();
            e.stopPropagation();
        }
    },

    _clearTimer: function () {
        if (this._timer) { window.clearTimeout(this._timer); this._timer = 0; }
    },

    _show: function (el, x, y) {
        const text = el.getAttribute('title');
        if (!text) return;
        // Stash & remove the native title to avoid the browser also showing it later.
        el.setAttribute('data-original-title', text);
        el.removeAttribute('title');

        const tip = this._tip;
        tip.textContent = text;
        tip.style.display = 'block';

        // Position above the touch point; clamp to viewport.
        const margin = 8;
        const rect = tip.getBoundingClientRect();
        let left = x - rect.width / 2;
        let top = y - rect.height - 16;
        if (left < margin) left = margin;
        if (left + rect.width > window.innerWidth - margin)
            left = window.innerWidth - rect.width - margin;
        if (top < margin) top = y + 24;
        tip.style.left = left + 'px';
        tip.style.top = top + 'px';
    },

    _hide: function () {
        if (!this._tip) return;
        this._tip.style.display = 'none';
        // Restore the native title on whatever element we last showed.
        const all = document.querySelectorAll('[data-original-title]');
        all.forEach(function (el) {
            el.setAttribute('title', el.getAttribute('data-original-title'));
            el.removeAttribute('data-original-title');
        });
    }
};
