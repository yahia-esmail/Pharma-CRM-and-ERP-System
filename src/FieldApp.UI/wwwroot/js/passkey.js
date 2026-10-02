// Passkeys (WebAuthn) in the page — fingerprint / face unlock sign-in (plan 9.3). The server builds the options
// (ASP.NET Core Identity) and verifies the result; this file only talks to the phone's authenticator.
// Uses the browser's JSON helpers where present (Chrome 129+, Safari 18+), with a manual fallback for older phones.

const toBuf = s => { const b = s.replace(/-/g, '+').replace(/_/g, '/'); return Uint8Array.from(atob(b + '==='.slice((b.length + 3) % 4)), c => c.charCodeAt(0)).buffer; };
const toB64 = buf => btoa(String.fromCharCode(...new Uint8Array(buf))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

function creationOptions(json) {
  if (PublicKeyCredential.parseCreationOptionsFromJSON) return PublicKeyCredential.parseCreationOptionsFromJSON(json);
  return { ...json, challenge: toBuf(json.challenge), user: { ...json.user, id: toBuf(json.user.id) },
    excludeCredentials: (json.excludeCredentials ?? []).map(c => ({ ...c, id: toBuf(c.id) })) };
}

function requestOptions(json) {
  if (PublicKeyCredential.parseRequestOptionsFromJSON) return PublicKeyCredential.parseRequestOptionsFromJSON(json);
  return { ...json, challenge: toBuf(json.challenge), allowCredentials: (json.allowCredentials ?? []).map(c => ({ ...c, id: toBuf(c.id) })) };
}

function serialize(cred) {
  if (typeof cred.toJSON === 'function') return JSON.stringify(cred.toJSON());
  const r = cred.response;
  const response = r.attestationObject
    ? { clientDataJSON: toB64(r.clientDataJSON), attestationObject: toB64(r.attestationObject), transports: r.getTransports?.() ?? [] }
    : { clientDataJSON: toB64(r.clientDataJSON), authenticatorData: toB64(r.authenticatorData), signature: toB64(r.signature),
        userHandle: r.userHandle ? toB64(r.userHandle) : null };
  return JSON.stringify({ id: cred.id, rawId: toB64(cred.rawId), type: cred.type, authenticatorAttachment: cred.authenticatorAttachment ?? null,
    response, clientExtensionResults: cred.getClientExtensionResults?.() ?? {} });
}

const failure = e => ({ ok: false, error: e?.name === 'NotAllowedError' ? 'cancelled' : e?.name === 'InvalidStateError' ? 'exists' : (e?.name || 'failed') });

// True when this phone can do it with its own fingerprint / face unlock.
export async function available() {
  try { return !!window.PublicKeyCredential && await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable(); }
  catch { return false; }
}

export async function create(optionsJson) {
  try {
    const cred = await navigator.credentials.create({ publicKey: creationOptions(JSON.parse(optionsJson)) });
    return { ok: true, json: serialize(cred) };
  } catch (e) { return failure(e); }
}

export async function get(optionsJson) {
  try {
    const cred = await navigator.credentials.get({ publicKey: requestOptions(JSON.parse(optionsJson)) });
    return { ok: true, json: serialize(cred) };
  } catch (e) { return failure(e); }
}
