import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { flushSync } from 'react-dom';
import {
  liveKinds, LiveRequestError, type LiveClient, type LiveEntry, type LiveHistory, type LiveKind, type LiveModule,
  type LivePollType, type LiveRound, type LiveSnapshot,
} from './live-course-api';
import { activeRoundForBlock, liveMaxOptions, liveOptionLimit, livePromptLimit, validLiveChoices, visibleEntries, wordCloud } from './live-course-state.mjs';
import { enterFullscreen, leaveFullscreen } from './fullscreen.mjs';
import './live-course.css';

interface Runtime {
  client: LiveClient;
  snapshot: LiveSnapshot | null;
  busy: boolean;
  offline: boolean;
  issue: string;
  command: (segments: string[], body?: object) => Promise<boolean>;
}
const LiveContext = createContext<Runtime | null>(null);
export const useLiveCourse = () => useContext(LiveContext);
const problem = (error: unknown, fallback: string) => error instanceof Error && error.name !== 'TypeError' ? error.message : fallback;

export function LiveCourseProvider({ client, enabled, children }:
  { client: LiveClient | null; enabled: boolean; children: ReactNode }) {
  const scope = useRef<AbortController | null>(null);
  const epoch = useRef(0);
  const pending = useRef(false);
  const current = useRef<LiveSnapshot | null>(null);
  const [snapshot, setSnapshot] = useState<LiveSnapshot | null>(null);
  const [offline, setOffline] = useState(false);
  const [busy, setBusy] = useState(false);
  const [issue, setIssue] = useState('');

  useEffect(() => {
    if (!client || !enabled) return;
    const lifetime = new AbortController();
    scope.current = lifetime;
    current.current = null;
    pending.current = false;
    setSnapshot(null);
    setOffline(false);
    setIssue('');
    setBusy(false);
    let timer: number | undefined;
    let reading = false;
    const read = async () => {
      if (lifetime.signal.aborted || reading) return;
      window.clearTimeout(timer);
      if (!pending.current) {
        reading = true;
        const readEpoch = ++epoch.current;
        try {
          const result = await client.read(lifetime.signal);
          if (!lifetime.signal.aborted && readEpoch === epoch.current) {
            current.current = result.session;
            setSnapshot(result.session);
            setOffline(false);
          }
        } catch {
          if (!lifetime.signal.aborted && readEpoch === epoch.current) setOffline(true);
        } finally { reading = false; }
      }
      if (!lifetime.signal.aborted) {
        timer = window.setTimeout(read, document.hidden ? 12000 : current.current ? 2200 : 4500);
      }
    };
    const resume = () => { if (!document.hidden) void read(); };
    document.addEventListener('visibilitychange', resume);
    window.addEventListener('online', resume);
    void read();
    return () => {
      lifetime.abort();
      if (scope.current === lifetime) scope.current = null;
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', resume);
      window.removeEventListener('online', resume);
    };
  }, [client, enabled]);

  const command = async (segments: string[], body = {}) => {
    const lifetime = scope.current;
    if (!client || !enabled || !lifetime || pending.current) return false;
    pending.current = true;
    ++epoch.current; // A GET begun before a command must not overwrite its newer snapshot.
    setBusy(true);
    setIssue('');
    try {
      const result = await client.command(segments, body, lifetime.signal);
      if (lifetime.signal.aborted) return false;
      current.current = result.session;
      setSnapshot(result.session);
      setOffline(false);
      return true;
    } catch (error) {
      if (!lifetime.signal.aborted) setIssue(problem(error, 'Handlingen kunne ikke gennemføres. Prøv igen.'));
      return false;
    } finally {
      if (!lifetime.signal.aborted) {
        pending.current = false;
        setBusy(false);
      }
    }
  };
  return <LiveContext.Provider value={client && enabled ? { client, snapshot, busy, offline, issue, command } : null}>{children}</LiveContext.Provider>;
}

const reactions = [
  { emoji: '👍', label: 'Forstået' }, { emoji: '🤔', label: 'Forklar igen' },
  { emoji: '💡', label: 'Idé' }, { emoji: '🙋', label: 'Spørgsmål' },
];
function ReactionTotals({ snapshot }: { snapshot: LiveSnapshot }) {
  const active = snapshot.reactions.filter(item => item.count > 0);
  return active.length ? <span className="notion-live-reaction-totals" aria-label="Holdets reaktioner">
    {active.map(item => <span key={item.emoji}>{item.emoji} <strong>{item.count}</strong></span>)}
  </span> : null;
}
export function LivePageBanner({ participant, publishedRevision, onResume }:
  { participant: boolean; publishedRevision?: number; onResume?: () => void }) {
  const runtime = useLiveCourse();
  const snapshot = runtime?.snapshot;
  if (!runtime || !snapshot) return null;
  const activeBlock = snapshot.round?.blockId;
  const isCurrent = snapshot.publishedRevision === publishedRevision;
  const viewActivity = () => activeBlock && document.getElementById('live-aktivitet-' + activeBlock)?.scrollIntoView({ block: 'center' });
  return <section className="notion-live-banner" aria-label="Live på holdet">
    <header><span className="notion-live-status"><i />Live på holdet</span><span>Slide {snapshot.slideIndex + 1}</span><ReactionTotals snapshot={snapshot} /></header>
    {!isCurrent && <p>Præsentationen bruger en anden udgave af siden. Genindlæs forløbet for at følge med.</p>}
    {runtime.offline && <p role="status">Forbindelsen blev afbrudt. LUP forsøger at forbinde igen.</p>}
    {participant && isCurrent && <div className="notion-live-reactions" aria-label="Send en reaktion">
      {reactions.map(item => <button key={item.emoji} type="button" aria-label={item.label} title={item.label} disabled={runtime.busy || runtime.offline}
        onClick={() => { void runtime.command([snapshot.id, 'react'], { emoji: item.emoji }); }}>{item.emoji}</button>)}
      {activeBlock && <button type="button" className="notion-live-goto" onClick={viewActivity}>Gå til det aktive spørgsmål</button>}
    </div>}
    {!participant && snapshot.mayManage && <div className="notion-live-actions">
      {isCurrent && onResume && <button type="button" onClick={onResume}>Genoptag præsentation</button>}
      <button type="button" disabled={runtime.busy || runtime.offline}
        onClick={() => { void runtime.command([snapshot.id, 'stop']); }}>Afslut live</button>
    </div>}
    {runtime.issue && <p role="alert">{runtime.issue}</p>}
  </section>;
}

const pollTypes: { id: LivePollType; title: string }[] = [
  { id: 'choice', title: 'Fælles valg' }, { id: 'mood', title: 'Vibe tjek' },
  { id: 'scale', title: 'Skala 1–5' }, { id: 'text', title: 'Kort svar' },
];
const kinds = Object.fromEntries(liveKinds.map(item => [item.kind, item.title])) as Record<LiveKind, string>;
function isChoice(kind: LiveKind, pollType?: LivePollType | null) {
  return kind === 'quiz' || kind === 'poll' && (!pollType || pollType === 'choice');
}
function usesText(round: LiveRound) { return round.kind === 'brainstorm' || round.kind === 'word-cloud' || round.kind === 'qa' || round.pollType === 'text'; }

function emptyModule(blockId: string, kind: LiveKind, prompt: string): LiveModule {
  return { id: '', blockId, kind, prompt, options: isChoice(kind) ? ['', ''] : [], pollType: kind === 'poll' ? 'choice' : undefined,
    correctChoice: null, durationSeconds: null };
}
function ConfigureActivity({ client, blockId, moduleId, kind, prompt, onSave }:
  { client: LiveClient; blockId: string; moduleId: string; kind: LiveKind; prompt: string; onSave: (module: LiveModule) => void }) {
  const [draft, setDraft] = useState(() => emptyModule(blockId, kind, prompt));
  const [shown, setShown] = useState(!moduleId);
  const [pending, setPending] = useState(false);
  const [issue, setIssue] = useState('');
  const task = useRef<AbortController | null>(null);
  useEffect(() => () => task.current?.abort(), [blockId, moduleId]);
  const run = async (action: (signal: AbortSignal) => Promise<void>) => {
    task.current?.abort();
    const request = new AbortController();
    task.current = request;
    setPending(true);
    setIssue('');
    try { await action(request.signal); }
    catch (error) { if (!request.signal.aborted) setIssue(problem(error, 'Aktiviteten kunne ikke gemmes. Prøv igen.')); }
    finally { if (!request.signal.aborted) setPending(false); }
  };
  const edit = () => run(async signal => {
    if (moduleId) {
      try {
        const module = await client.module(moduleId, signal);
        if (signal.aborted) return;
        setDraft({ ...module, blockId });
      } catch (error) {
        if (!(error instanceof LiveRequestError && (error.status === 404 || error.status === 403))) throw error;
        if (signal.aborted) return;
        setDraft(emptyModule(blockId, kind, prompt));
        setIssue('Blokken kan få sin egen aktivitet ved at gemme nye indstillinger her.');
      }
    }
    setShown(true);
  });
  const selectable = isChoice(kind, draft.pollType);
  const valid = Boolean(draft.prompt.trim()) && draft.prompt.trim().length <= livePromptLimit && (!selectable || validLiveChoices(draft.options))
    && (kind !== 'quiz' || draft.correctChoice !== null && draft.correctChoice < draft.options.length
      && (draft.durationSeconds === null || Number.isInteger(draft.durationSeconds) && draft.durationSeconds >= 5 && draft.durationSeconds <= 300));
  const remove = (index: number) => setDraft(value => ({
    ...value,
    options: value.options.filter((_, i) => i !== index),
    correctChoice: value.correctChoice === index ? null : value.correctChoice !== null && value.correctChoice > index ? value.correctChoice - 1 : value.correctChoice,
  }));
  const save = () => run(async signal => {
    const value = await client.saveModule({
      blockId, kind, prompt: draft.prompt.trim(), pollType: kind === 'poll' ? draft.pollType : undefined,
      options: selectable ? draft.options.map(option => option.trim()) : [],
      correctChoice: kind === 'quiz' ? draft.correctChoice : null,
      durationSeconds: kind === 'quiz' ? draft.durationSeconds : null,
    }, signal);
    if (signal.aborted) return;
    setPending(false);
    setShown(false);
    onSave(value);
  });
  return <div className="notion-live-config">
    {!shown && <button type="button" disabled={pending} onClick={() => { void edit(); }}>Redigér {kinds[kind].toLowerCase()}</button>}
    {shown && <form aria-label={'Opsæt ' + kinds[kind]} onSubmit={event => { event.preventDefault(); if (valid && !pending) void save(); }}>
      <label>{kind === 'qa' ? 'Introduktion' : 'Spørgsmål'}<input required maxLength={livePromptLimit} value={draft.prompt}
        onChange={event => setDraft(value => ({ ...value, prompt: event.target.value }))} /></label>
      {kind === 'poll' && <label>Svartype<select value={draft.pollType || 'choice'} onChange={event => setDraft(value => ({
        ...value, pollType: event.target.value as LivePollType, options: value.options.length >= 2 ? value.options : ['', ''],
      }))}>{pollTypes.map(type => <option key={type.id} value={type.id}>{type.title}</option>)}</select></label>}
      {selectable && <fieldset><legend>Svarmuligheder{kind === 'quiz' ? ' – markér facit' : ''}</legend>
        {draft.options.map((option, index) => <div className="notion-live-option-config" key={index}>
          {kind === 'quiz' && <input type="radio" name={'facit-' + blockId} checked={draft.correctChoice === index}
            aria-label={'Svar ' + (index + 1) + ' er facit'} onChange={() => setDraft(value => ({ ...value, correctChoice: index }))} />}
          <input aria-label={'Svar ' + (index + 1)} value={option} required maxLength={liveOptionLimit} onChange={event => setDraft(value => ({
            ...value, options: value.options.map((item, i) => i === index ? event.target.value : item),
          }))} />
          {draft.options.length > 2 && <button type="button" aria-label={'Fjern svar ' + (index + 1)} onClick={() => remove(index)}>Fjern</button>}
        </div>)}
        <button type="button" disabled={draft.options.length >= liveMaxOptions} onClick={() => setDraft(value => ({ ...value, options: [...value.options, ''] }))}>Tilføj svar</button>
      </fieldset>}
      {kind === 'quiz' && <label>Tidsgrænse i sekunder (valgfri)<input type="number" min={5} max={300}
        value={draft.durationSeconds ?? ''} onChange={event => setDraft(value => ({ ...value, durationSeconds: event.target.value === '' ? null : Number(event.target.value) }))} /></label>}
      <div className="notion-live-actions">
        <button type="submit" className="notion-live-primary" disabled={pending || !valid}>{pending ? 'Gemmer…' : 'Gem aktivitet'}</button>
        {moduleId && <button type="button" onClick={() => setShown(false)}>Annullér</button>}
      </div>
    </form>}
    {issue && <p role="alert">{issue}</p>}
  </div>;
}

function useSecondsLeft(deadlineUtc: string | null) {
  const read = () => deadlineUtc ? Math.max(0, Math.ceil((Date.parse(deadlineUtc) - Date.now()) / 1000)) : null;
  const [remaining, setRemaining] = useState(read);
  useEffect(() => {
    setRemaining(read());
    if (!deadlineUtc) return;
    const timer = window.setInterval(() => setRemaining(read()), 500);
    return () => window.clearInterval(timer);
  }, [deadlineUtc]);
  return remaining;
}

function RoundAnswer({ runtime, session, round }: { runtime: Runtime; session: LiveSnapshot; round: LiveRound }) {
  const [choice, setChoice] = useState<number | null>(round.yourChoice);
  const [text, setText] = useState('');
  const remaining = useSecondsLeft(round.deadlineUtc);
  const disabled = runtime.busy || runtime.offline || !round.open || remaining === 0;
  const multiple = round.kind === 'qa' || round.kind === 'brainstorm';
  const maxLength = round.kind === 'word-cloud' ? 60 : 280;
  const submitted = round.hasAnswered ? <p className="notion-live-answered" role="status">✓ {multiple ? 'Dit bidrag' : 'Dit svar'} er gemt.</p> : null;
  if (round.hasAnswered && !multiple) return submitted;
  if (!round.open || remaining === 0) return <>{submitted}<p>Svarene er lukket.</p></>;
  const textRound = usesText(round);
  return <>{submitted}<form className="notion-live-answer" onSubmit={event => {
    event.preventDefault();
    if (disabled) return;
    void runtime.command([session.id, 'rounds', round.id, 'answer'], textRound ? { text: text.trim() } : { choice })
      .then(saved => { if (saved && multiple) setText(''); });
  }}>
    {remaining !== null && <span className="notion-live-timer">{remaining} sek.</span>}
    {textRound ? <label>{round.kind === 'qa' ? 'Dit spørgsmål' : round.kind === 'brainstorm' ? 'Din idé' : round.kind === 'word-cloud' ? 'Dit ord' : 'Dit svar'}
      <textarea value={text} maxLength={maxLength} rows={2} required disabled={disabled} onChange={event => setText(event.target.value)} />
    </label> :
      <fieldset><legend>Vælg dit svar</legend>{round.options.map((option, index) =>
        <label className="notion-live-answer-option" key={index}><input type="radio" name={'svar-' + round.id} disabled={disabled}
          checked={choice === index} onChange={() => setChoice(index)} />{option}</label>)}</fieldset>}
    <button type="submit" className="notion-live-primary" disabled={disabled || (textRound ? !text.trim() : choice === null)}>
      {round.kind === 'qa' ? 'Send spørgsmål' : round.kind === 'brainstorm' ? 'Send idé' : 'Send svar'}
    </button>
  </form></>;
}

function Ranking({ snapshot }: { snapshot: LiveSnapshot }) {
  return snapshot.rankingVisible && Boolean(snapshot.ranking?.length) ? <section className="notion-live-ranking">
    <h3>Stilling</h3><ol>{snapshot.ranking!.map((entry, index) => <li key={index}>{entry.label}<strong>{entry.score} point</strong></li>)}</ol>
  </section> : null;
}
function RoundResults({ round, session, runtime, participant }:
  { round: LiveRound; session: LiveSnapshot; runtime: Runtime; participant: boolean }) {
  const entries = participant && (round.kind === 'qa' || round.kind === 'brainstorm')
    ? round.entries.filter(entry => entry.approved) : visibleEntries(round);
  return <>
    {round.showResults && round.counts && <ul className="notion-live-results">
      {round.options.map((option, index) => {
        const count = round.counts?.[index] ?? 0;
        const total = round.totalAnswers ?? 0;
        const percentage = total > 0 ? Math.round(count * 100 / total) : 0;
        return <li key={index}><span>{option}{round.correctChoice === index && ' · Facit'}</span><span>{count} ({percentage} %)</span>
          <progress max={100} value={percentage} aria-label={option + ' – ' + percentage + ' %'} /></li>;
      })}
    </ul>}
    {entries.length > 0 && (round.kind === 'word-cloud' ? <ul className="notion-live-cloud" aria-label="Ordsky">
      {wordCloud(entries).map(word => <li key={word.id} aria-label={word.text + ' · ' + word.count + ' svar'}
        style={{ fontSize: 1 + Math.min(1.5, (word.count - 1) * .3) + 'em' }}>{word.text}</li>)}
    </ul> : <ul className="notion-live-entries">
      {entries.map(entry => <li key={entry.id}><span>{entry.text}{participant && entry.isMine && <small> · Dit bidrag</small>}</span>{(round.kind === 'qa' || round.kind === 'brainstorm') &&
        (participant ? <button type="button" aria-pressed={entry.yourVote} disabled={runtime.busy || runtime.offline || !round.open}
          onClick={() => { void runtime.command([session.id, 'rounds', round.id, 'entries', entry.id, 'vote'], { voted: !entry.yourVote }); }}>Stem · {entry.votes}</button>
          : <span>{entry.votes} stemmer</span>)}</li>)}
    </ul>)}
    {round.showResults && round.kind === 'quiz' && <Ranking snapshot={session} />}
  </>;
}

function Moderation({ runtime, session, round }: { runtime: Runtime; session: LiveSnapshot; round: LiveRound }) {
  const approve = (entry: LiveEntry) => runtime.command([session.id, 'rounds', round.id, 'entries', entry.id, 'moderate'], { approved: !entry.approved });
  if (!usesText(round)) return null;
  const cloud = round.kind === 'word-cloud';
  return <details className="notion-live-moderation"><summary>{cloud ? 'Administrér ord' : 'Moderér svar'} ({round.entries.length})</summary>
    {round.entries.length === 0 ? <p>{cloud ? 'Der er endnu ingen ord.' : 'Der er endnu ingen svar at godkende.'}</p> :
      <ul className="notion-live-entries">{round.entries.map(entry => <li key={entry.id}>
        <span>{entry.text}</span><button type="button" disabled={runtime.busy || runtime.offline}
          onClick={() => { void approve(entry); }}>{entry.approved ? 'Skjul' : cloud ? 'Vis igen' : 'Godkend'}</button>
      </li>)}</ul>}
  </details>;
}

export function LiveActivity({ blockId, moduleId, kind, prompt, editable, presenting, activePresentation, participant, publishedRevision, onSave }:
  { blockId: string; moduleId: string; kind: LiveKind; prompt: string; editable: boolean; presenting: boolean;
    activePresentation: boolean; participant: boolean; publishedRevision?: number; onSave: (module: LiveModule) => void }) {
  const runtime = useLiveCourse();
  const snapshot = runtime?.snapshot;
  const [focused, setFocused] = useState(false);
  const focusDialog = useRef<HTMLDialogElement>(null);
  const focusScreen = useRef<HTMLDivElement>(null);
  const returnFocus = useRef<HTMLElement | null>(null);
  const nativeFullscreen = useRef(false);
  const nativeExitedAt = useRef(0);
  const validEdition = snapshot?.publishedRevision === publishedRevision && (!presenting || activePresentation);
  const round = validEdition ? activeRoundForBlock(snapshot ?? null, blockId, moduleId) : null;
  const showHost = Boolean((presenting || focused) && validEdition && snapshot?.mayManage && moduleId);
  const mayFocus = Boolean(validEdition && snapshot?.mayManage && round);
  const controlsDisabled = runtime?.busy || runtime?.offline;
  const control = (action: string) => runtime && snapshot && round
    ? runtime.command([snapshot.id, 'rounds', round.id, 'control'], { action }) : Promise.resolve(false);
  const ranked = round?.kind === 'quiz' && round.showResults;
  const closeFocus = () => {
    void leaveFullscreen(focusScreen.current);
    setFocused(false);
    requestAnimationFrame(() => returnFocus.current?.isConnected && returnFocus.current.focus({ preventScroll: true }));
  };
  const openFocus = () => {
    returnFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    flushSync(() => setFocused(true));
    focusDialog.current?.showModal();
    void enterFullscreen(focusScreen.current);
  };
  useEffect(() => {
    if (!focused) return;
    const element = focusScreen.current;
    const changed = () => {
      const active = document.fullscreenElement === element;
      if (nativeFullscreen.current && !active) nativeExitedAt.current = performance.now();
      nativeFullscreen.current = active;
    };
    document.addEventListener('fullscreenchange', changed);
    return () => {
      document.removeEventListener('fullscreenchange', changed);
      void leaveFullscreen(element);
      nativeFullscreen.current = false;
    };
  }, [focused]);
  useEffect(() => {
    if (focused && !mayFocus) closeFocus();
  }, [focused, mayFocus]);
  const hostControls = showHost && runtime && snapshot && <div className="notion-live-actions">
    {(!round || !round.open) && <button type="button" className="notion-live-primary" disabled={controlsDisabled}
      onClick={() => { void runtime.command([snapshot.id, 'rounds'], { moduleId, blockId }); }}>{round ? 'Start ny runde' : 'Åbn for svar'}</button>}
    {round?.open && <button type="button" disabled={controlsDisabled} onClick={() => { void control('close'); }}>Luk for svar</button>}
    {round && <button type="button" disabled={controlsDisabled} onClick={() => { void control(round.showResults ? 'hide' : 'show'); }}>{round.showResults ? 'Skjul resultater' : 'Vis resultater'}</button>}
    {ranked && <button type="button" disabled={controlsDisabled} onClick={() => {
      void runtime.command([snapshot.id, 'ranking'], { visible: !snapshot.rankingVisible });
    }}>{snapshot.rankingVisible ? 'Skjul stilling' : 'Vis stilling'}</button>}
  </div>;
  return <section className="notion-live-activity" contentEditable={false} id={!presenting ? 'live-aktivitet-' + blockId : undefined}
    aria-label={kinds[kind]}>
    <header><span className="notion-live-kind">{kinds[kind] || 'Liveaktivitet'}</span>{round && <span>{round.open ? 'Åben for svar' : 'Lukket'}{round.totalAnswers !== null && ' · ' + round.totalAnswers + ' svar'}</span>}
      {mayFocus && <button type="button" onClick={openFocus}>Fuld skærm</button>}
    </header>
    <p className="notion-live-prompt">{round?.prompt ?? (prompt || 'Aktiviteten mangler et spørgsmål')}</p>
    {editable && runtime && <ConfigureActivity client={runtime.client} blockId={blockId} moduleId={moduleId} kind={kind} prompt={prompt} onSave={onSave} />}
    {hostControls}
    {round && runtime && snapshot && <>
      {participant && !presenting && <RoundAnswer key={round.id} runtime={runtime} session={snapshot} round={round} />}
      <RoundResults round={round} session={snapshot} runtime={runtime} participant={participant && !presenting} />
      {!presenting && snapshot.mayManage && <Moderation runtime={runtime} session={snapshot} round={round} />}
    </>}
    {!editable && !round && !showHost && <p className="notion-live-note">Afventer underviseren.</p>}
    {showHost && runtime?.issue && <p role="alert">{runtime.issue}</p>}
    {focused && round && runtime && snapshot && <dialog ref={focusDialog} className="notion-live-focus" aria-label={kinds[kind] + ' i fuld skærm'}
      onKeyDown={event => event.stopPropagation()} onCancel={event => {
        event.preventDefault(); event.stopPropagation();
        if (document.fullscreenElement === focusScreen.current) { void leaveFullscreen(focusScreen.current); return; }
        if (performance.now() - nativeExitedAt.current > 300) closeFocus();
      }}>
      <div ref={focusScreen} className="notion-live-focus-screen">
        <header className="notion-live-focus-header"><span className="notion-live-kind">{kinds[kind]}</span>
          <span>{round.open ? 'Åben for svar' : 'Lukket'}{round.totalAnswers !== null && ' · ' + round.totalAnswers + ' svar'}</span>
          <button type="button" autoFocus onClick={closeFocus}>Luk fuld skærm</button>
        </header>
        <div className="notion-live-focus-content"><h2>{round.prompt}</h2>
          <RoundResults round={round} session={snapshot} runtime={runtime} participant={false} />
        </div>
        <footer>{hostControls}{runtime.offline && <p role="status">Forbindelsen blev afbrudt. LUP forsøger at forbinde igen.</p>}
          {runtime.issue && <p role="alert">{runtime.issue}</p>}
        </footer>
      </div>
    </dialog>}
  </section>;
}

export function LivePresenterHeader({ revision, courseId, canStart, allowHost }:
  { revision?: number; courseId?: string | null; canStart: boolean; allowHost: boolean }) {
  const runtime = useLiveCourse();
  if (!runtime || !allowHost && !runtime.snapshot?.mayManage) return null;
  const { snapshot, busy, offline, issue, command } = runtime;
  const start = () => revision === undefined ? Promise.resolve(false)
    : command(['start'], { publishedRevision: revision, courseId: courseId || undefined });
  return <div className="notion-live-host-toolbar">
    {snapshot ? <><span className="notion-live-status"><i />Live</span><ReactionTotals snapshot={snapshot} />
      {snapshot.mayManage && <button type="button" disabled={busy || offline} onClick={() => { void command([snapshot.id, 'stop']); }}>Afslut live</button>}</>
      : <button type="button" className="notion-live-primary" disabled={!canStart || busy || offline}
        title={!canStart ? 'Gem og udgiv forløbet, før du går live.' : undefined}
        onClick={() => { void start(); }}>Start live</button>}
    {(!snapshot && !canStart) && <span>Gem og udgiv siden først.</span>}
    {snapshot && snapshot.publishedRevision !== revision && <span>Live bruger en anden udgave. Genindlæs forløbet.</span>}
    {offline && <span role="status">Forbinder igen…</span>}
    {issue && <span role="alert">{issue}</span>}
  </div>;
}

export function LiveSessionHistory() {
  const data = useLiveCourse();
  const client = data?.client;
  const [open, setOpen] = useState(false);
  const [history, setHistory] = useState<LiveHistory[] | null>(null);
  const [issue, setIssue] = useState('');
  useEffect(() => {
    if (!open || !client) return;
    const request = new AbortController();
    setHistory(null);
    setIssue('');
    void client.history(request.signal).then(rows => {
      if (!request.signal.aborted) setHistory(rows);
    }).catch(error => {
      if (!request.signal.aborted) setIssue(problem(error, 'Sessionsoversigten kunne ikke hentes.'));
    });
    return () => request.abort();
  }, [client, open]);
  if (!client) return null;
  return <details className="notion-live-history" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>Sessionsoversigt</summary>
    {open && (issue ? <p role="alert">{issue}</p> : !history ? <p role="status">Henter sessioner…</p> :
      history.length === 0 ? <p>Siden har endnu ingen afsluttede sessioner.</p> : <ul>
        {history.map(session => <li key={session.id}><details>
          <summary>{new Date(session.startedAtUtc).toLocaleString('da-DK')} · {session.participantCount} har svaret</summary>
          <ol>{session.rounds.map(round => <li key={round.id}><strong>{round.prompt}</strong> · {round.totalAnswers} svar
            {round.counts && <ul>{round.options.map((option, index) => <li key={index}>{option} · {round.counts?.[index] ?? 0}</li>)}</ul>}
          </li>)}</ol>
        </details></li>)}
      </ul>)}
  </details>;
}
