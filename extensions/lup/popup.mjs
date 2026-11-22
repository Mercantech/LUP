import { bindCalendar } from './calendar.mjs';

const badge = document.querySelector('#connection-badge');
const label = document.querySelector('#connection-label');
const status = document.querySelector('#connection');
const error = document.querySelector('#access-error');
const key = document.querySelector('#access-key');
const connect = document.querySelector('#connect');
const disconnect = document.querySelector('#disconnect');
const settings = document.querySelector('#connection-settings');
const summary = settings.querySelector('summary');
const summaryLabel = document.querySelector('#connection-summary');
let busy = false;
let lastConnected = null;
let connection;
let revision = 0;

const showCalendarAccess = bindCalendar({
  onAccessChanged: code => show(code === 'calendar_access'
    ? { ...connection, canReadCalendar: false }
    : { connected: false, message: 'Forbindelsen er udløbet eller tilbagekaldt. Tilføj en ny nøgle.' }),
});

function show(state) {
  connection = state;
  const connected = state?.connected === true;
  badge.dataset.state = connected ? 'connected' : 'disconnected';
  label.textContent = connected ? 'Forbundet' : 'Ikke forbundet';
  status.textContent = connected
    ? (state.email ? 'Forbundet som ' + state.email + '.' : 'Denne Chrome-profil er forbundet med LUP.')
    : state?.message || 'Hent en personlig nøgle på LUP.dk, og indsæt den her.';
  disconnect.hidden = !connected;
  summaryLabel.textContent = connected ? 'Forbindelse og indstillinger' : 'Forbind udvidelsen';

  // Avoid closing a form while someone is entering a replacement key.
  if (lastConnected !== connected) settings.open = !connected;
  lastConnected = connected;
  showCalendarAccess(connected ? (state.canReadCalendar ? 'ready' : 'upgrade') : 'disconnected');
}

function setBusy(value, clearing = false) {
  busy = value;
  key.disabled = connect.disabled = disconnect.disabled = value;
  settings.setAttribute('aria-busy', String(value));
  connect.textContent = value && !clearing ? 'Forbinder…' : 'Forbind LUP';
}

function showError(message) {
  error.textContent = message;
  error.hidden = false;
  settings.open = true;
}

async function refreshConnection() {
  const request = ++revision;
  try {
    const state = await chrome.runtime.sendMessage({ type: 'settings:status' });
    if (revision === request && !busy) show(state);
  } catch {
    if (revision === request && !busy) show({ connected: false, message: 'LUP kunne ikke kontaktes. Prøv igen senere.' });
  }
}

document.querySelector('#access-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (busy) return;
  ++revision;
  error.hidden = true;
  const selection = [key.selectionStart, key.selectionEnd];
  let failed = false;
  setBusy(true);
  try {
    const response = await chrome.runtime.sendMessage({ type: 'settings:save', key: key.value });
    if (!response?.ok) throw new Error(response?.message || 'Forbindelsen kunne ikke gemmes.');
    key.value = '';
    showCalendarAccess('loading');
    show(response);
    settings.open = false;
    summary.focus({ preventScroll: true });
  } catch (failure) {
    failed = true;
    showError(failure.message || 'LUP kunne ikke kontaktes. Prøv igen.');
  } finally {
    setBusy(false);
    if (failed) {
      key.focus({ preventScroll: true });
      if (selection.every(Number.isInteger)) key.setSelectionRange(...selection);
    }
  }
});

disconnect.addEventListener('click', async () => {
  if (busy) return;
  ++revision;
  error.hidden = true;
  setBusy(true, true);
  let cleared = false;
  try {
    const response = await chrome.runtime.sendMessage({ type: 'settings:clear' });
    if (!response?.ok) throw new Error('Forbindelsen kunne ikke fjernes. Prøv igen.');
    key.value = '';
    show({ connected: false, message: 'Forbindelsen er fjernet fra denne Chrome-profil.' });
    cleared = true;
  } catch (failure) {
    showError(failure.message || 'Forbindelsen kunne ikke fjernes. Prøv igen.');
  } finally {
    setBusy(false);
    if (cleared) key.focus();
  }
});

chrome.storage.onChanged.addListener((changes, area) => {
  if (area !== 'local' || !Object.hasOwn(changes, 'lup.upload.access') || busy) return;
  showCalendarAccess('loading');
  void refreshConnection();
});
void refreshConnection();
