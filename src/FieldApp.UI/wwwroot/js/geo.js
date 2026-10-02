// Browser Geolocation for the field app (plan 7). Two modes:
//   getBestFix  — Best-of-N high-accuracy fix for events (check-in, order, collection…)
//   startWatch  — foreground tracking; the caller stops it when the page is hidden
//
// Results are returned as plain objects ({ ok, fix } / { ok:false, code, message }) instead of
// rejected promises: GeolocationPositionError keeps its fields on the prototype, so a rejection would
// reach .NET as an opaque JSException and the Denied/Unavailable/Timeout distinction would be lost.

const ERR = { UNSUPPORTED: 0, DENIED: 1, UNAVAILABLE: 2, TIMEOUT: 3, INSECURE: 4 };

function toFix(p, samples, elapsedMs, source) {
  const c = p.coords;
  return {
    latitude: c.latitude,
    longitude: c.longitude,
    accuracyMeters: c.accuracy,
    altitudeMeters: c.altitude,
    speedMps: c.speed,
    heading: Number.isFinite(c.heading) ? c.heading : null,
    deviceTimestampUtc: new Date(p.timestamp).toISOString(),
    samplesCollected: samples,
    elapsedMs: Math.round(elapsedMs),
    source
  };
}

function unavailableReason() {
  if (!window.isSecureContext) return { ok: false, code: ERR.INSECURE, message: 'Location needs HTTPS.' };
  if (!('geolocation' in navigator)) return { ok: false, code: ERR.UNSUPPORTED, message: 'This browser has no location support.' };
  return null;
}

// Best-of-N: watch with high accuracy, keep the most accurate reading, stop early once it is good enough.
// Readings clearly older than the request (cached fixes the OS hands back instantly) are ignored, because
// an old fix at the previous pharmacy must never be used to check in at this one. A few seconds of slack
// are allowed: a GNSS fix is timestamped when computed, which can precede its delivery.
const STALE_TOLERANCE_MS = 10000;
let finishCurrentFix = null;

// "Use current location" button: stop waiting for a better reading and settle for the best so far.
export function acceptCurrentFix() { finishCurrentFix?.(); }

export function getBestFix(dotnetRef, targetAccuracy, maxWaitMs) {
  const unavailable = unavailableReason();
  if (unavailable) return Promise.resolve(unavailable);

  return new Promise(resolve => {
    const started = performance.now();
    const requestedAt = Date.now();
    const options = { enableHighAccuracy: true, maximumAge: 0, timeout: maxWaitMs };
    let best = null, samples = 0, done = false, watchId = null, timer = null, unavailableTimer = null;

    const finish = (error) => {
      if (done) return;
      done = true;
      finishCurrentFix = null;
      if (watchId !== null) navigator.geolocation.clearWatch(watchId);
      clearTimeout(timer);
      clearTimeout(unavailableTimer);
      if (best) resolve({ ok: true, fix: toFix(best, samples, performance.now() - started, 'Event') });
      else resolve(error ?? { ok: false, code: ERR.TIMEOUT, message: 'No location fix before the time limit.' });
    };

    const onPosition = p => {
      if (done || p.timestamp < requestedAt - STALE_TOLERANCE_MS) return;
      clearTimeout(unavailableTimer);
      unavailableTimer = null;
      samples++;
      if (!best || p.coords.accuracy < best.coords.accuracy) best = p;
      dotnetRef?.invokeMethodAsync('OnFixProgress', best.coords.accuracy, samples).catch(() => {});
      if (p.coords.accuracy <= targetAccuracy) finish();
    };

    const onError = e => {
      if (done) return;
      // Denied is final. UNAVAILABLE is often a momentary signal loss, so it only ends the request if no
      // reading follows within a few seconds (location switched off keeps failing, a blip recovers).
      // A per-reading TIMEOUT is never final: our own timer decides.
      if (e.code === ERR.DENIED) finish({ ok: false, code: e.code, message: e.message });
      else if (e.code === ERR.UNAVAILABLE && !best && unavailableTimer === null) {
        unavailableTimer = setTimeout(() => finish({ ok: false, code: e.code, message: e.message }), 3000);
      }
    };

    watchId = navigator.geolocation.watchPosition(onPosition, onError, options);
    // A new watch doesn't always get an immediate reading while another one (tracking) is open, and
    // some browsers only call a watch back when the position changes; a one-shot request guarantees a
    // first sample. Both feed the same Best-of-N.
    navigator.geolocation.getCurrentPosition(onPosition, onError, options);
    timer = setTimeout(() => finish(), maxWaitMs);
    finishCurrentFix = () => finish();
  });
}

let trackWatchId = null;

export function startWatch(dotnetRef, source) {
  const unavailable = unavailableReason();
  if (unavailable) return unavailable;
  stopWatch();
  const started = performance.now();
  trackWatchId = navigator.geolocation.watchPosition(
    p => dotnetRef.invokeMethodAsync('OnTrackFix', toFix(p, 1, performance.now() - started, source)).catch(() => {}),
    e => dotnetRef.invokeMethodAsync('OnTrackError', e.code, e.message).catch(() => {}),
    // maximumAge lets the OS reuse a fix up to 10 s old instead of powering the GPS for every callback.
    { enableHighAccuracy: true, maximumAge: 10000, timeout: 60000 }
  );
  return { ok: true };
}

export function stopWatch() {
  if (trackWatchId !== null) {
    navigator.geolocation.clearWatch(trackWatchId);
    trackWatchId = null;
  }
}

// 'granted' | 'denied' | 'prompt' | 'unknown'. Safari only gained Permissions API support for
// geolocation in 16, and some embedded browsers throw — callers treat 'unknown' like 'prompt'.
export async function queryPermission() {
  if (unavailableReason()) return 'unsupported';
  try {
    if (!navigator.permissions?.query) return 'unknown';
    return (await navigator.permissions.query({ name: 'geolocation' })).state;
  } catch { return 'unknown'; }
}

export function platform() {
  const ua = navigator.userAgent;
  if (/iPhone|iPad|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)) return 'ios';
  if (/Android/.test(ua)) return 'android';
  return 'other';
}

// ---- Screen wake lock (Route Mode, plan 7.5) ------------------------------------------------------
// The lock is released by the browser whenever the page is hidden, so it is re-acquired on return.
let wakeLock = null, wakeWanted = false;

async function acquireWakeLock() {
  if (!wakeWanted || wakeLock || document.visibilityState !== 'visible') return;
  try {
    wakeLock = await navigator.wakeLock.request('screen');
    wakeLock.addEventListener('release', () => { wakeLock = null; });
  } catch { wakeLock = null; }
}

document.addEventListener('visibilitychange', () => { acquireWakeLock(); });

export async function setWakeLock(enabled) {
  if (!('wakeLock' in navigator)) return false;
  wakeWanted = enabled;
  if (enabled) { await acquireWakeLock(); return wakeLock !== null; }
  if (wakeLock) { try { await wakeLock.release(); } catch { } wakeLock = null; }
  return false;
}

export async function batteryLevel() {
  try { return navigator.getBattery ? (await navigator.getBattery()).level : null; }   // not on iOS / Firefox
  catch { return null; }
}
