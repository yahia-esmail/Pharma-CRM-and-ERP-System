// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Push works in development too.
self.importScripts('./push-sw.js');

self.addEventListener('message', event => {
    if (event.data === 'SKIP_WAITING') self.skipWaiting();
});
