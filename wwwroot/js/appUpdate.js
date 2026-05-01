// PWA update detection for Blazor WebAssembly.
// Notifies .NET when a newer service worker is installed and waiting,
// and provides apply()/checkForUpdate() helpers triggered from the UI.
window.appUpdate = (function () {
    let dotnetRef = null;
    let registration = null;

    async function register(ref) {
        dotnetRef = ref;

        if (!('serviceWorker' in navigator)) return;

        try {
            registration = await navigator.serviceWorker.getRegistration();
            if (!registration) return;

            // If a new worker is already waiting at startup, notify immediately.
            if (registration.waiting && navigator.serviceWorker.controller) {
                notify();
            }

            // Listen for new workers being installed.
            registration.addEventListener('updatefound', () => {
                const installing = registration.installing;
                if (!installing) return;
                installing.addEventListener('statechange', () => {
                    if (installing.state === 'installed' && navigator.serviceWorker.controller) {
                        notify();
                    }
                });
            });

            // When the active worker changes (after applying), reload to use new assets.
            let refreshing = false;
            navigator.serviceWorker.addEventListener('controllerchange', () => {
                if (refreshing) return;
                refreshing = true;
                window.location.reload();
            });
        } catch (e) {
            console.warn('appUpdate.register failed:', e);
        }
    }

    function notify() {
        if (dotnetRef) {
            dotnetRef.invokeMethodAsync('OnUpdateAvailable').catch(() => { });
        }
    }

    async function checkForUpdate() {
        try {
            if (!registration) {
                registration = await navigator.serviceWorker.getRegistration();
            }
            if (registration) {
                await registration.update();
            }
        } catch (e) {
            console.warn('appUpdate.checkForUpdate failed:', e);
        }
    }

    function apply() {
        if (registration && registration.waiting) {
            registration.waiting.postMessage('SKIP_WAITING');
        } else {
            window.location.reload();
        }
    }

    return { register, checkForUpdate, apply };
})();
