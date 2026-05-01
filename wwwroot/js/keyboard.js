window.circleKeyboard = {
    register: function (dotnetRef, mappingJson) {
        if (window._circleKeyHandler) {
            window.removeEventListener('keydown', window._circleKeyHandler);
        }
        // mappingJson: { "ArrowLeft": "prev", " ": "fullscreen", ... }
        const mapping = JSON.parse(mappingJson || '{}');
        window._circleKeyHandler = function (e) {
            if (e.ctrlKey || e.altKey || e.metaKey) return;
            const target = e.target;
            if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)) return;
            const action = mapping[e.key];
            if (action) {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('OnKeyboardShortcut', action);
            }
        };
        window.addEventListener('keydown', window._circleKeyHandler);
    },
    unregister: function () {
        if (window._circleKeyHandler) {
            window.removeEventListener('keydown', window._circleKeyHandler);
            window._circleKeyHandler = null;
        }
    }
};
