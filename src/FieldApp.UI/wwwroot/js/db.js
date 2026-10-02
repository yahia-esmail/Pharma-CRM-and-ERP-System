// Local IndexedDB database for the field app (plan 8.1). Every value is a JSON string produced by .NET
// (LocalDb.cs), so this module stays schema-agnostic: it only knows store names and keys.
// localStorage is avoided on purpose: it is synchronous, small, and evicted more aggressively by Safari.
//
// Stores:
//   kv      session, profile and cached API snapshots (dashboard, master data)
//   outbox  queued mutations waiting to reach the API
//   refs    local placeholder → server id, e.g. "order:<guid>" → 1234, for dependent outbox items
const DB_NAME = 'pharma-field';
const DB_VERSION = 2;
export const STORES = ['kv', 'outbox', 'refs'];

let dbPromise = null;

// Close our connection when the page goes away (reload, app update, closing the PWA). An abandoned
// connection can otherwise linger and keep the next page's transactions waiting behind it.
window.addEventListener('pagehide', () => {
  const pending = dbPromise;
  dbPromise = null;
  pending?.then(db => db.close()).catch(() => {});
});

function openDb() {
  if (dbPromise) return dbPromise;
  dbPromise = new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      for (const name of STORES) {
        if (!db.objectStoreNames.contains(name)) db.createObjectStore(name);
      }
    };
    req.onsuccess = () => {
      const db = req.result;
      // Another tab opened a newer version (after an app update): release so it can upgrade.
      db.onversionchange = () => { db.close(); dbPromise = null; };
      resolve(db);
    };
    req.onerror = () => { dbPromise = null; reject(req.error); };
    req.onblocked = () => console.warn('IndexedDB upgrade blocked by another open tab');
  });
  return dbPromise;
}

async function run(storeNames, mode, action) {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(storeNames, mode);
    let result;
    Promise.resolve(action(tx, value => { result = value; })).catch(reject);
    tx.oncomplete = () => resolve(result ?? null);
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
}

function request(req) {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

export const get = (store, key) =>
  run(store, 'readonly', async (tx, done) => done(await request(tx.objectStore(store).get(key))));

export const put = (store, key, value) =>
  run(store, 'readwrite', tx => { tx.objectStore(store).put(value, key); });

export const remove = (store, key) =>
  run(store, 'readwrite', tx => { tx.objectStore(store).delete(key); });

export const getAll = store =>
  run(store, 'readonly', async (tx, done) => done(await request(tx.objectStore(store).getAll())));

export const count = store =>
  run(store, 'readonly', async (tx, done) => done(await request(tx.objectStore(store).count())));

export const clear = store =>
  run(store, 'readwrite', tx => { tx.objectStore(store).clear(); });

export const clearAll = () =>
  run(STORES, 'readwrite', tx => { for (const name of STORES) tx.objectStore(name).clear(); });

// Asks the browser not to evict our data under storage pressure (most effective once installed).
export async function requestPersistence() {
  try {
    if (!navigator.storage?.persist) return false;
    return (await navigator.storage.persisted()) || (await navigator.storage.persist());
  } catch { return false; }
}
