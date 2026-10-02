// Network status, service-worker update flow and diagnostics for the PWA host.

// ---- Network -------------------------------------------------------------------------------------
// navigator.onLine only says "some network interface is up" — a captive portal or dead mobile data
// still reports true. Phase 3 adds a lightweight API ping on top; this is the fast first signal.
let networkNotify = null;

export function watchNetwork(dotnetRef) {
  unwatchNetwork();
  networkNotify = () => dotnetRef.invokeMethodAsync('OnNetworkChanged', navigator.onLine);
  window.addEventListener('online', networkNotify);
  window.addEventListener('offline', networkNotify);
  return navigator.onLine;
}

export function unwatchNetwork() {
  if (!networkNotify) return;
  window.removeEventListener('online', networkNotify);
  window.removeEventListener('offline', networkNotify);
  networkNotify = null;
}

// ---- Page visibility -----------------------------------------------------------------------------
// Returning to the foreground is the moment to sync (and, from phase 4, to resume GPS tracking).
let visibilityNotify = null;

export function watchVisibility(dotnetRef) {
  if (visibilityNotify) document.removeEventListener('visibilitychange', visibilityNotify);
  visibilityNotify = () => dotnetRef.invokeMethodAsync('OnVisibilityChanged', document.visibilityState === 'visible');
  document.addEventListener('visibilitychange', visibilityNotify);
}

// ---- Service-worker updates ---------------------------------------------------------------------
// A new version installs into "waiting" and is only activated when the rep taps "Update" — never
// mid-way through entering an order (plan 10.2).
export async function watchForUpdates(dotnetRef) {
  if (!('serviceWorker' in navigator)) return;
  const reg = await navigator.serviceWorker.getRegistration();
  if (!reg) return;

  const announce = () => dotnetRef.invokeMethodAsync('OnUpdateAvailable');
  if (reg.waiting && navigator.serviceWorker.controller) announce();

  reg.addEventListener('updatefound', () => {
    const worker = reg.installing;
    worker?.addEventListener('statechange', () => {
      // "installed" with an existing controller == an update, not the first install.
      if (worker.state === 'installed' && navigator.serviceWorker.controller) announce();
    });
  });

  // Installed PWAs can stay open for days without navigating, so check explicitly.
  setInterval(() => reg.update().catch(() => {}), 60 * 60 * 1000);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') reg.update().catch(() => {});
  });
}

export async function applyUpdate() {
  const reg = await navigator.serviceWorker.getRegistration();
  if (!reg?.waiting) { location.reload(); return; }
  navigator.serviceWorker.addEventListener('controllerchange', () => location.reload(), { once: true });
  reg.waiting.postMessage('SKIP_WAITING');
}

// ---- Diagnostics --------------------------------------------------------------------------------
export async function getDiagnostics() {
  const result = {
    userAgent: navigator.userAgent,
    online: navigator.onLine,
    standalone: window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true,
    serviceWorker: 'unsupported',
    geolocationPermission: 'unknown',
    storagePersisted: null,
    storageUsageBytes: null,
    storageQuotaBytes: null
  };

  if ('serviceWorker' in navigator) {
    const reg = await navigator.serviceWorker.getRegistration();
    result.serviceWorker = !reg ? 'not registered'
      : reg.waiting ? 'update waiting'
      : reg.active ? 'active' : 'installing';
  }
  try {
    if (navigator.permissions) {
      result.geolocationPermission = (await navigator.permissions.query({ name: 'geolocation' })).state;
    }
  } catch { /* Safari < 16 rejects the query */ }
  try {
    if (navigator.storage) {
      result.storagePersisted = await navigator.storage.persisted?.() ?? null;
      const estimate = await navigator.storage.estimate?.();
      result.storageUsageBytes = estimate?.usage ?? null;
      result.storageQuotaBytes = estimate?.quota ?? null;
    }
  } catch { }
  return result;
}
