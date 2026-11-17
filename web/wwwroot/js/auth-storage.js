const mercantecSession = (() => {
  const sharedKey = 'mt_auth_session';
  const revisionKey = 'mt_auth_revision';
  const fallbackEpochKey = 'mt_auth_fallback_epoch';
  const lockName = 'mercantec-auth-session';
  const empty = { access: null, refresh: null, expiresAt: 0 };
  const pending = new Map();
  let observer;
  let channel;
  let storage;
  let localGeneration = 0;

  try {
    if (typeof BroadcastChannel === 'function' && navigator.locks && typeof crypto.randomUUID === 'function') {
      storage = window.localStorage;
      // Check that this browser allows the coordination record in this storage partition.
      const probe = lockName + '-probe';
      storage.setItem(probe, '1');
      storage.removeItem(probe);
      channel = new BroadcastChannel(lockName);
    }
  } catch { storage = null; }

  function readJson(value) {
    try { return JSON.parse(value); } catch { return null; }
  }

  function readRevision(value) {
    const revision = readJson(value);
    return typeof revision?.id === 'string' && Number.isSafeInteger(revision.version)
      && typeof revision.signedOut === 'boolean' ? revision : null;
  }

  function currentRevision() {
    return channel ? readRevision(storage.getItem(sharedKey)) : null;
  }

  function loginEpoch() {
    return channel ? currentRevision()?.id ?? '' : sessionStorage.getItem(fallbackEpochKey) ?? '';
  }

  function readTokens() {
    return {
      access: sessionStorage.getItem('mt_access_token'),
      refresh: sessionStorage.getItem('mt_refresh_token'),
      expiresAt: Number(sessionStorage.getItem('mt_expires_at') || '0'),
      revision: readRevision(sessionStorage.getItem(revisionKey))
    };
  }

  function storeTokens(tokens) {
    sessionStorage.setItem('mt_access_token', tokens.access);
    sessionStorage.setItem('mt_refresh_token', tokens.refresh);
    sessionStorage.setItem('mt_expires_at', String(tokens.expiresAt));
    if (tokens.revision) sessionStorage.setItem(revisionKey, JSON.stringify(tokens.revision));
  }

  function clearTabTokens() {
    sessionStorage.removeItem('mt_access_token');
    sessionStorage.removeItem('mt_refresh_token');
    sessionStorage.removeItem('mt_expires_at');
    sessionStorage.removeItem(revisionKey);
  }

  function sameSession(left, right) {
    return left?.id === right?.id && left?.signedOut === right?.signedOut;
  }

  function matchesCurrent(tokens) {
    const current = currentRevision();
    return !!tokens.access && !!current && !current.signedOut
      && sameSession(tokens.revision, current) && tokens.revision.version === current.version;
  }

  function getTokens() {
    const tokens = readTokens();
    if (!channel) return tokens;
    const current = currentRevision();
    if (!tokens.access || (sameSession(tokens.revision, current) && !current?.signedOut)) return tokens;
    clearTabTokens();
    return empty;
  }

  function usable(tokens) {
    return !!tokens.access && (tokens.expiresAt <= 0 || Date.now() / 1000 < tokens.expiresAt);
  }

  function notify() {
    if (typeof observer === 'function') observer();
    else if (observer) observer.invokeMethodAsync('OnSharedSessionChanged').catch(() => {});
  }

  function requestSession(revision) {
    if (!channel || !revision || revision.signedOut) return Promise.resolve();
    return new Promise(resolve => {
      const id = crypto.randomUUID();
      const timer = setTimeout(finish, 450);
      function finish() {
        clearTimeout(timer);
        pending.delete(id);
        resolve();
      }
      pending.set(id, { revision, finish });
      channel.postMessage({ type: 'request', id });
    });
  }

  function onMessage({ data }) {
    if (data?.type === 'request') {
      const tokens = getTokens();
      if (matchesCurrent(tokens) && (usable(tokens) || tokens.refresh))
        channel.postMessage({ type: 'response', id: data.id, tokens });
      return;
    }

    if (!['response', 'updated'].includes(data?.type) || !matchesCurrent(data.tokens ?? empty)) return;
    const tokens = data.tokens;
    const local = getTokens();
    if (data.type === 'response') {
      const request = pending.get(data.id);
      if (!request || !sameSession(request.revision, tokens.revision)) return;
      storeTokens(tokens);
      request.finish();
      return;
    }

    const requests = [...pending.values()].filter(request => sameSession(request.revision, tokens.revision));
    if (!sameSession(local.revision, tokens.revision) && !requests.length) return;
    if (local.revision?.version === tokens.revision.version) return;
    storeTokens(tokens);
    requests.forEach(request => request.finish());
    if (local.access) notify();
  }

  function onStorage(event) {
    if (event.key !== sharedKey && event.key !== null) return;
    const previous = readRevision(event.oldValue);
    const current = currentRevision();
    if (event.key !== null && (sameSession(previous, current) || (!previous && current && !current.signedOut))) return;
    if (!getTokens().access) notify();
  }

  async function initialize(receiveChanges) {
    observer = receiveChanges;
    if (!channel) return publicTokens(getTokens());
    const stored = readTokens();
    if (stored.access && !stored.revision && !currentRevision()) {
      await navigator.locks.request(lockName, () => {
        if (!currentRevision()) publish(stored, newRevision());
      });
    }
    const tokens = getTokens();
    if (!matchesCurrent(tokens) && !sessionStorage.getItem('mt_oauth_state'))
      await requestSession(currentRevision());
    return publicTokens(getTokens());
  }

  function newRevision(signedOut = false) {
    return { id: crypto.randomUUID(), version: 0, signedOut };
  }

  function publish(tokens, revision) {
    storage.setItem(sharedKey, JSON.stringify(revision));
    const updated = { ...tokens, revision };
    storeTokens(updated);
    channel.postMessage({ type: 'updated', tokens: updated });
  }

  function setTokens(access, refresh, expiresAt, expectedLoginEpoch) {
    const tokens = { access, refresh, expiresAt };
    if (!channel) {
      if (expectedLoginEpoch !== undefined && loginEpoch() !== expectedLoginEpoch) return false;
      localGeneration++;
      storeTokens(tokens);
      return true;
    }
    return navigator.locks.request(lockName, () => {
      // A logout or another completed sign-in supersedes this OAuth callback.
      if (expectedLoginEpoch !== undefined && loginEpoch() !== expectedLoginEpoch) return false;
      publish(tokens, newRevision());
      return true;
    });
  }

  function clearTokens(appLogout) {
    if (!channel || !appLogout) {
      localGeneration++;
      if (appLogout) sessionStorage.setItem(fallbackEpochKey, String(Number(loginEpoch()) + 1));
      return clearTabTokens();
    }
    return navigator.locks.request(lockName, () => {
      storage.setItem(sharedKey, JSON.stringify(newRevision(true)));
      clearTabTokens();
    });
  }

  function canUse(tokens, rejected) {
    const expiring = tokens.expiresAt > 0 && Date.now() / 1000 >= tokens.expiresAt - 30;
    return usable(tokens) && tokens.access !== rejected && (rejected !== null || !expiring);
  }

  async function exchangeRefresh(url, clientId, refreshToken) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 10000);
    try {
      const response = await fetch(url, {
        method: 'POST',
        body: new URLSearchParams({ grant_type: 'refresh_token', client_id: clientId, refresh_token: refreshToken }),
        signal: controller.signal
      });
      if (response.status === 400) return { invalid: (await response.json())?.error === 'invalid_grant' };
      if (!response.ok) return {};
      const payload = await response.json();
      if (typeof payload?.access_token !== 'string' || !payload.access_token.trim()) return {};
      return {
        tokens: {
          access: payload.access_token,
          refresh: payload.refresh_token ?? refreshToken,
          expiresAt: Math.floor(Date.now() / 1000) + (payload.expires_in > 0 ? payload.expires_in : 900)
        }
      };
    } catch { return {}; }
    finally { clearTimeout(timer); }
  }

  let localOperation = Promise.resolve();
  function withRefreshLock(refresh) {
    if (channel) return navigator.locks.request(lockName + '-refresh', refresh);
    const operation = localOperation.then(refresh);
    localOperation = operation.catch(() => {});
    return operation;
  }

  function refreshTokens(url, clientId, rejected, retryOnFailure) {
    // A request using the old bearer must never be retried as a different account.
    const session = getTokens().revision;
    const generation = localGeneration;
    const obsolete = () => channel
      ? !sameSession(session, currentRevision())
      : generation !== localGeneration;
    return withRefreshLock(async () => {
      let tokens = getTokens();
      if (!sameSession(tokens.revision, session) || obsolete()) return { access: null };
      if (channel && tokens.access && !matchesCurrent(tokens)) {
        await requestSession(currentRevision());
        tokens = getTokens();
        if (!sameSession(tokens.revision, session)) return { access: null };
        if (!matchesCurrent(tokens)) {
          if (!rejected && usable(tokens)) return { access: tokens.access };
          clearTabTokens();
          return { access: null, sessionEnded: true };
        }
      }
      if (rejected && usable(tokens) && tokens.revision?.version !== session?.version)
        return { access: tokens.access };
      if (canUse(tokens, rejected)) return { access: tokens.access };
      if (!tokens.refresh) return { access: null };

      let result = await exchangeRefresh(url, clientId, tokens.refresh);
      if (!result.tokens && !result.invalid && retryOnFailure && !usable(tokens) && !obsolete()) {
        await new Promise(resolve => setTimeout(resolve, 1000));
        if (!obsolete()) result = await exchangeRefresh(url, clientId, tokens.refresh);
      }
      function finish() {
        // Logout and a successful authorization callback must be free to invalidate an
        // in-flight refresh, even if the token server takes a long time to answer.
        if (obsolete()) return { access: null };
        if (result.tokens) {
          if (channel) publish(result.tokens, { ...tokens.revision, version: tokens.revision.version + 1 });
          else storeTokens(result.tokens);
          return { access: result.tokens.access };
        }
        if (result.invalid) {
          if (channel) storage.setItem(sharedKey, JSON.stringify(newRevision(true)));
          clearTabTokens();
          return { access: null, sessionEnded: true };
        }
        return { access: !rejected && usable(tokens) ? tokens.access : null, temporaryFailure: true };
      }
      return channel ? await navigator.locks.request(lockName, finish) : finish();
    });
  }

  function publicTokens({ access, refresh, expiresAt }) {
    return { access, refresh, expiresAt };
  }

  if (channel) {
    channel.onmessage = onMessage;
    window.addEventListener('storage', onStorage);
  }

  return {
    getTokens: receiveChanges => receiveChanges ? initialize(receiveChanges) : publicTokens(getTokens()),
    setTokens,
    clearTokens,
    refreshTokens,
    loginEpoch
  };
})();

window.mercantecAuth = {
  setTokens: mercantecSession.setTokens,
  getTokens: mercantecSession.getTokens,
  clearTokens: mercantecSession.clearTokens,
  refreshTokens: mercantecSession.refreshTokens,

  setPkce(verifier, state) {
    sessionStorage.setItem('mt_pkce_verifier', verifier);
    sessionStorage.setItem('mt_oauth_state', state);
    sessionStorage.setItem('mt_oauth_login_epoch', mercantecSession.loginEpoch());
  },
  getPkce() {
    return {
      verifier: sessionStorage.getItem('mt_pkce_verifier'),
      state: sessionStorage.getItem('mt_oauth_state'),
      loginEpoch: sessionStorage.getItem('mt_oauth_login_epoch') ?? ''
    };
  },
  clearPkce() {
    sessionStorage.removeItem('mt_pkce_verifier');
    sessionStorage.removeItem('mt_oauth_state');
    sessionStorage.removeItem('mt_oauth_login_epoch');
  },
  setLoginReturnPath(path) {
    sessionStorage.setItem('mt_login_return', JSON.stringify({ path, expiresAt: Date.now() + 30 * 60 * 1000 }));
  },
  consumeLoginReturnPath() {
    const raw = sessionStorage.getItem('mt_login_return');
    sessionStorage.removeItem('mt_login_return');
    try {
      const value = JSON.parse(raw);
      const now = Date.now();
      return typeof value?.path === 'string' && Number.isFinite(value.expiresAt)
        && value.expiresAt > now && value.expiresAt <= now + 30 * 60 * 1000 ? value.path : null;
    } catch { return null; }
  },
  clearLoginReturnPath() {
    sessionStorage.removeItem('mt_login_return');
  }
};

window.mercantecDownload = {
  fromBase64(filename, contentType, base64) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64}`;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
  }
};
