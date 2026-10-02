// Registers the service worker (kept out of index.html so the Content-Security-Policy needs no inline scripts).
// updateViaCache:'none' makes the browser always re-fetch service-worker.js so releases are picked up. The
// "new version — Update" prompt is driven from device.js (watchForUpdates).
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
}
