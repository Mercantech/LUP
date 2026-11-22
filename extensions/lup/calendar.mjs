const dateFormat = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'long' });
const fullDateFormat = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'long' });
const timeFormat = new Intl.DateTimeFormat('da-DK', { hour: '2-digit', minute: '2-digit' });
const dayNames = ['Man', 'Tir', 'Ons', 'Tor', 'Fre', 'Lør', 'Søn'];
const eventKinds = { teaching: 'LUP-skema', deadline: 'Frist', outlook: 'Outlook' };

export function addDays(date, count) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + count);
}

export function weekStart(date) {
  return addDays(date, -((date.getDay() + 6) % 7));
}

function calendarDay(date) {
  return Date.UTC(date.getFullYear(), date.getMonth(), date.getDate());
}

export function eventsOnDay(events, date) {
  const start = addDays(date, 0).getTime();
  const end = addDays(date, 1).getTime();
  const allDayStart = calendarDay(date);
  const allDayEnd = calendarDay(addDays(date, 1));

  return events.filter(event => {
    const first = Date.parse(event.start);
    const last = Date.parse(event.end);
    const lower = event.isAllDay ? allDayStart : start;
    const upper = event.isAllDay ? allDayEnd : end;
    return first < upper && (last > lower || (first === last && first >= lower));
  }).sort((left, right) => Number(right.isAllDay) - Number(left.isAllDay)
    || Date.parse(left.start) - Date.parse(right.start));
}

function displayTime(event, date) {
  if (event.isAllDay) return 'Hele dagen';
  const start = Date.parse(event.start), end = Date.parse(event.end);
  if (start === end || event.kind === 'deadline') return timeFormat.format(start);
  return timeFormat.format(Math.max(start, addDays(date, 0)))
    + '–' + (end >= addDays(date, 1) ? '24.00' : timeFormat.format(end));
}

export function bindCalendar({ onAccessChanged }) {
  const gate = document.querySelector('#calendar-gate');
  const gateMessage = document.querySelector('#calendar-gate-message');
  const accessLink = document.querySelector('#calendar-access-link');
  const overview = document.querySelector('#calendar-overview');
  const weekLabel = document.querySelector('#calendar-week');
  const days = document.querySelector('#calendar-days');
  const dateHeading = document.querySelector('#calendar-date');
  const status = document.querySelector('#calendar-status');
  const list = document.querySelector('#calendar-events');
  const note = document.querySelector('#calendar-note');
  const retry = document.querySelector('#calendar-retry');
  const today = addDays(new Date(), 0);
  let selected = today;
  let week = weekStart(today);
  let events = [];
  let outlookStatus;
  let revision = 0;
  let loadState = 'loading';
  let access;

  const dayButtons = dayNames.map((name, index) => {
    const button = document.createElement('button');
    button.className = 'calendar-day';
    button.type = 'button';
    const weekday = document.createElement('span');
    weekday.textContent = name;
    const number = document.createElement('strong');
    const dot = document.createElement('span');
    dot.className = 'day-dot';
    dot.setAttribute('aria-hidden', 'true');
    button.append(weekday, number, dot);
    button.addEventListener('click', () => {
      selected = addDays(week, index);
      render();
    });
    days.append(button);
    return { button, number };
  });

  function render() {
    weekLabel.textContent = dateFormat.formatRange(week, addDays(week, 6));
    dayButtons.forEach(({ button, number }, index) => {
      const date = addDays(week, index);
      number.textContent = date.getDate();
      button.setAttribute('aria-label', fullDateFormat.format(date));
      button.setAttribute('aria-pressed', String(+date === +selected));
      if (+date === +today) button.setAttribute('aria-current', 'date');
      else button.removeAttribute('aria-current');
      button.classList.toggle('has-events', eventsOnDay(events, date).length > 0);
    });

    const dateName = fullDateFormat.format(selected);
    dateHeading.textContent = dateName.charAt(0).toUpperCase() + dateName.slice(1);
    const daily = eventsOnDay(events, selected);
    retry.hidden = loadState !== 'error';
    list.hidden = daily.length === 0;
    status.textContent = loadState === 'loading' ? 'Henter kalender…'
      : loadState === 'error' ? 'Kalenderen kunne ikke hentes.'
        : daily.length ? '' : 'Ingen aftaler eller frister denne dag.';
    list.replaceChildren(...daily.map(event => {
      const entry = document.createElement('li');
      entry.dataset.kind = event.kind;
      const time = document.createElement('span');
      time.className = 'event-time';
      time.textContent = displayTime(event, selected);
      const details = document.createElement('div');
      const title = document.createElement('strong');
      title.className = 'event-title';
      title.textContent = event.title;
      const source = document.createElement('span');
      source.className = 'event-source';
      source.textContent = [...new Set([eventKinds[event.kind], event.source, event.location].filter(Boolean))].join(' · ');
      details.append(title, source);
      entry.append(time, details);
      return entry;
    }));
    list.scrollTop = 0;
    note.textContent = outlookStatus === 'unavailable' ? 'Outlook kunne ikke hentes lige nu.' : '';
    note.hidden = !note.textContent;
  }

  async function load() {
    const request = ++revision;
    events = [];
    outlookStatus = undefined;
    loadState = 'loading';
    render();
    try {
      const response = await chrome.runtime.sendMessage({
        type: 'calendar:week',
        from: week.toISOString(),
        to: addDays(week, 7).toISOString(),
      });
      if (request !== revision) return;
      if (response?.code === 'calendar_access' || response?.code === 'connection_required') {
        onAccessChanged(response.code);
        return;
      }
      if (!response?.ok) throw new Error('Calendar request failed');
      events = response.events;
      outlookStatus = response.outlookStatus;
      loadState = 'ready';
    } catch {
      if (request !== revision) return;
      loadState = 'error';
    }
    render();
  }

  function moveWeek(offset) {
    week = addDays(week, offset);
    selected = +week === +weekStart(today) ? today : week;
    void load();
  }

  document.querySelector('#calendar-prev').addEventListener('click', () => moveWeek(-7));
  document.querySelector('#calendar-next').addEventListener('click', () => moveWeek(7));
  document.querySelector('#calendar-today').addEventListener('click', () => {
    const previousWeek = week;
    selected = today;
    week = weekStart(today);
    if (+week === +previousWeek) render();
    else void load();
  });
  retry.addEventListener('click', () => {
    dateHeading.focus({ preventScroll: true });
    void load();
  });

  return state => {
    if (access === state) return;
    access = state;
    const ready = state === 'ready';
    gate.hidden = overview.hidden = true;
    accessLink.hidden = state !== 'upgrade';
    gateMessage.textContent = state === 'loading' ? 'Tjekker, om kalenderen er tilgængelig…'
      : state === 'upgrade' ? 'Din forbindelse kan gemme emojis. Tilføj en nøgle med kalenderadgang for at se dit skema her.'
        : 'Forbind en personlig nøgle med kalenderadgang for at se dit skema og dine aftaler her.';
    gate.hidden = ready;
    overview.hidden = !ready;
    if (ready) void load();
    else {
      ++revision;
      events = [];
      outlookStatus = undefined;
      list.replaceChildren();
    }
  };
}
