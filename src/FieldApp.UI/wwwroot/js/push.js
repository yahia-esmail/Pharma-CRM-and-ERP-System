// Web Push in the page (plan 10.4): permission, subscription, and messages from the service worker (push-sw.js).

const supported = () => 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
const standalone = () => window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
const ios = () => /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

async function registration() {
  return await navigator.serviceWorker.getRegistration() ?? await navigator.serviceWorker.ready;
}

export async function status() {
  if (!supported()) return { supported: false, permission: 'unsupported', subscribed: false, standalone: standalone(), ios: ios() };
  let subscribed = false;
  try { subscribed = !!(await (await registration())?.pushManager.getSubscription()); } catch { }
  return { supported: true, permission: Notification.permission, subscribed, standalone: standalone(), ios: ios() };
}

function keyBytes(base64url) {
  const padded = (base64url + '='.repeat((4 - base64url.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
  return Uint8Array.from(atob(padded), c => c.charCodeAt(0));
}

// Asks permission (must follow a tap) and subscribes. Returns the subscription JSON for the API, or an error code.
export async function subscribe(publicKey) {
  if (!supported()) return { ok: false, error: 'unsupported' };
  const permission = await Notification.requestPermission();
  if (permission !== 'granted') return { ok: false, error: permission };   // denied | default
  try {
    const reg = await registration();
    let sub = await reg.pushManager.getSubscription();
    if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(publicKey) });
    return { ok: true, json: JSON.stringify(sub) };
  } catch (e) {
    return { ok: false, error: e?.name || 'failed' };
  }
}

// Returns the endpoint that was removed (to tell the API), or null.
export async function unsubscribe() {
  if (!supported()) return null;
  try {
    const sub = await (await registration())?.pushManager.getSubscription();
    if (!sub) return null;
    const endpoint = sub.endpoint;
    await sub.unsubscribe();
    return endpoint;
  } catch { return null; }
}

let listening = false;
export function listen(dotnetRef) {
  if (listening || !('serviceWorker' in navigator)) return;
  listening = true;
  navigator.serviceWorker.addEventListener('message', e => {
    if (e.data?.type === 'push-received') dotnetRef.invokeMethodAsync('OnPushReceived');
    if (e.data?.type === 'open-path') dotnetRef.invokeMethodAsync('OnOpenPath', e.data.path);
  });
}
