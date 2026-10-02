// Web Push in the service worker (plan 10.4), shared by service-worker.js (dev) and service-worker.published.js.
// The payload comes from WebPushDispatchService: { title, body, notificationId, type, entityType, entityId }.

self.addEventListener('push', event => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; } catch { data = { body: event.data ? event.data.text() : '' }; }

  event.waitUntil((async () => {
    await self.registration.showNotification(data.title || 'Field CRM', {
      body: data.body || '',
      icon: 'icon-192.png',
      badge: 'icon-192.png',
      // One notification per server notification: a re-delivered push replaces instead of stacking.
      tag: data.notificationId ? `n-${data.notificationId}` : undefined,
      data
    });
    // An open app refreshes its list and unread badge.
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    windows.forEach(w => w.postMessage({ type: 'push-received', notificationId: data.notificationId ?? null }));
  })());
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  const d = event.notification.data || {};
  // The app maps the related record to its screen (NotificationLinks) and marks the notification read.
  const path = d.notificationId
    ? `notifications/open/${d.notificationId}?type=${encodeURIComponent(d.type || '')}&entity=${encodeURIComponent(d.entityType || '')}&eid=${d.entityId ?? ''}`
    : 'notifications';

  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const open = windows.find(w => new URL(w.url).origin === self.location.origin);
    if (open) {
      // Navigate inside the running app (no reload, nothing typed is lost).
      open.postMessage({ type: 'open-path', path });
      return open.focus();
    }
    return self.clients.openWindow(new URL(path, self.registration.scope).href);
  })());
});
