import { Fragment, useEffect, useId, useMemo, useRef, useState } from 'react';
import type { KeyboardEvent, MouseEvent, ReactNode } from 'react';

type Question = { id: string; prompt: string; kind: 'single' | 'multiple' | 'boolean' | 'short'; options: string[] };
type Answer = { questionId: string; selectedOptions: number[]; text: string | null };
type Review = {
  questionId: string;
  correct: boolean;
  correctOptions: number[];
  acceptedAnswers: string[];
  explanation?: string | null;
  learnMoreUrl?: string | null;
  optionExplanations?: string[] | null;
  learnMoreTitle?: string | null;
  learnMoreDescription?: string | null;
  comment?: string | null;
};
type Attempt = {
  id: string;
  status: 'started' | 'submitted' | 'graded';
  revision: number;
  questionIndex: number;
  questions: Question[];
  answers: Answer[];
  score: number | null;
  results: Review[] | null;
};
type Publication = {
  id: string;
  title: string;
  description: string;
  topic: string;
  estimatedMinutes: number | null;
  questionCount: number;
  holdId: string | null;
  holdName: string | null;
  maxAttempts: number | null;
  isActive: boolean;
  activeAttemptId: string | null;
  latestAttemptId: string | null;
  attemptsUsed: number;
};
type Draft = Pick<Attempt, 'questionIndex' | 'answers'>;

export type KnowledgeCheckRunnerProps = {
  distributionId: string;
  apiBase: string;
  getToken: (rejectedAccess?: string) => Promise<string | null>;
  inline?: boolean;
  pageId?: string;
  blockId?: string;
  attemptId?: string | null;
};

class KnowledgeCheckError extends Error {
  constructor(readonly status: number, readonly code: string) {
    super(code);
  }
}

function message(error: unknown, fallback: string) {
  if (!(error instanceof KnowledgeCheckError)) return fallback;
  if (error.status === 401) return 'Log ind igen for at fortsætte.';
  if (error.status === 403 || error.status === 404) return 'Videnstjekket er ikke tilgængeligt for dig her.';
  if (error.code === 'attempt_limit') return 'Du har brugt dine forsøg på denne udgivelse.';
  if (error.code === 'incomplete_attempt') return 'Besvar alle spørgsmål, før du afleverer.';
  if (error.code === 'invalid_answer') return 'Et af svarene kunne ikke gemmes. Gennemgå dine svar og prøv igen.';
  if (error.code === 'publication_closed') return 'Denne udgivelse er lukket for nye besvarelser.';
  return fallback;
}

function makeApi(props: KnowledgeCheckRunnerProps) {
  const base = props.apiBase.replace(/\/+$/, '');
  const root = `${base}/knowledge-checks/publications/${encodeURIComponent(props.distributionId)}`;
  async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const url = new URL(root + path, window.location.origin);
    if (props.pageId && props.blockId) {
      url.searchParams.set('pageId', props.pageId);
      url.searchParams.set('blockId', props.blockId);
    }
    const send = (token: string) => fetch(url, {
      ...init,
      cache: 'no-store',
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${token}`,
        ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      },
    });
    const token = await props.getToken();
    if (!token) throw new KnowledgeCheckError(401, 'unauthorized');
    let response = await send(token);
    if (response.status === 401) {
      const refreshed = await props.getToken(token);
      if (refreshed && refreshed !== token) response = await send(refreshed);
    }
    if (!response.ok) {
      const failure = await response.json().catch(() => null);
      throw new KnowledgeCheckError(response.status, typeof failure?.error === 'string' ? failure.error : '');
    }
    return response.json() as Promise<T>;
  }
  const attemptPath = (id: string) => `/attempts/${encodeURIComponent(id)}`;
  return {
    publication: (signal?: AbortSignal) => request<Publication>('', { signal }),
    attempt: (id: string, signal?: AbortSignal) => request<Attempt>(attemptPath(id), { signal }),
    start: (requestId: string) => request<Attempt>('/attempts', {
      method: 'POST', body: JSON.stringify({ requestId }),
    }),
    save: (attempt: Attempt, draft: Draft) => request<Attempt>(attemptPath(attempt.id), {
      method: 'PUT', body: JSON.stringify({ ...draft, revision: attempt.revision }),
    }),
    submit: (attempt: Attempt) => request<Attempt>(attemptPath(attempt.id) + '/submit', {
      method: 'POST', body: JSON.stringify({ revision: attempt.revision }),
    }),
  };
}

type CheckApi = ReturnType<typeof makeApi>;

function endpoint(distributionId: string) {
  return '/videnstjek/' + encodeURIComponent(distributionId);
}

export function KnowledgeCheckRunner(props: KnowledgeCheckRunnerProps) {
  const { distributionId, apiBase, pageId, blockId, attemptId, inline = false } = props;
  const tokenGetter = useRef(props.getToken);
  tokenGetter.current = props.getToken;
  const api = useMemo(() => makeApi({
    distributionId, apiBase, pageId, blockId, getToken: rejected => tokenGetter.current(rejected),
  }), [distributionId, apiBase, pageId, blockId]);
  const [publication, setPublication] = useState<Publication | null>(null);
  const [attempt, setAttempt] = useState<Attempt | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [starting, setStarting] = useState(false);
  const [reload, setReload] = useState(0);
  const requestId = useRef<string | null>(null);
  const headingId = useId();
  const openUrl = endpoint(distributionId);

  useEffect(() => {
    const abort = new AbortController();
    setLoading(true);
    setError('');
    setPublication(null);
    setAttempt(null);
    requestId.current = null;

    async function load() {
      try {
        const item = await api.publication(abort.signal);
        const recent = item.isActive || item.activeAttemptId !== item.latestAttemptId ? item.latestAttemptId : null;
        const resume = attemptId || (item.isActive ? item.activeAttemptId : null) || recent;
        const saved = resume ? await api.attempt(resume, abort.signal) : null;
        if (!abort.signal.aborted) {
          setPublication(item);
          setAttempt(saved);
        }
      } catch (failure) {
        if (!abort.signal.aborted) setError(message(failure, 'Videnstjekket kunne ikke hentes. Prøv igen.'));
      } finally {
        if (!abort.signal.aborted) setLoading(false);
      }
    }
    void load();
    return () => abort.abort();
  }, [api, attemptId, reload]);

  async function start() {
    if (starting || !publication) return;
    setStarting(true);
    setError('');
    requestId.current ??= crypto.randomUUID();
    try {
      const next = await api.start(requestId.current);
      requestId.current = null;
      if (practice && attemptId) {
        const target = new URL(window.location.href);
        target.searchParams.set('forsog', next.id);
        window.location.assign(target.href);
        return;
      }
      setAttempt(next);
      setPublication(current => current && next.status === 'started' && current.activeAttemptId !== next.id
        ? { ...current, activeAttemptId: next.id, latestAttemptId: next.id, attemptsUsed: current.attemptsUsed + 1 }
        : current);
    } catch (failure) {
      setError(message(failure, 'Forsøget kunne ikke startes. Prøv igen.'));
    } finally {
      setStarting(false);
    }
  }

  const onAttemptChanged = (next: Attempt) => {
    setAttempt(next);
    if (next.status !== 'started') {
      void api.publication().then(setPublication).catch(() => null);
    }
  };
  const mayStart = publication?.isActive
    && (publication.maxAttempts == null || publication.attemptsUsed < publication.maxAttempts);
  const practice = !inline && publication?.holdId === null;
  const anotherAttemptIsActive = practice && publication?.activeAttemptId && publication.activeAttemptId !== attempt?.id;
  const retryLabel = anotherAttemptIsActive ? 'Fortsæt igangværende forsøg' : 'Nyt forsøg';

  return <section className={`lup-knowledge-player${inline ? ' is-inline' : ''}${practice ? ' is-practice' : ''}`} aria-labelledby={headingId}>
    <header className="lup-knowledge-header">
      <div>
        <h2 id={headingId}>{publication?.title ?? 'Videnstjek'}</h2>
        {publication && <p className="lup-knowledge-meta">
          {[publication.holdName || publication.topic, `${publication.questionCount} spørgsmål`,
            publication.estimatedMinutes ? `Ca. ${publication.estimatedMinutes} min.` : ''].filter(Boolean).join(' · ')}
        </p>}
      </div>
      {inline && publication && attempt?.status !== 'started' &&
        <a className="secondary" href={openUrl}>Åbn på egen side</a>}
    </header>
    {loading && <p role="status">Henter videnstjek…</p>}
    {error && <div className="lup-knowledge-error" role="alert">
      <p>{error}</p>
      {!publication && <button type="button" className="secondary" onClick={() => setReload(current => current + 1)}>Prøv igen</button>}
    </div>}
    {publication && !attempt && <>
      {publication.description && <p className="lup-knowledge-description">{publication.description}</p>}
      {publication.maxAttempts != null && <p className="lup-knowledge-meta">
        {publication.maxAttempts === 1 ? 'Ét forsøg' : `Op til ${publication.maxAttempts} forsøg`}
      </p>}
      {mayStart
        ? <button type="button" className="action" onClick={start} disabled={starting}>{starting ? 'Starter…' : 'Start videnstjek'}</button>
        : <p>Der kan ikke startes et forsøg på denne udgivelse.</p>}
    </>}
    {attempt?.status === 'started' &&
      <AttemptForm key={attempt.id} initialAttempt={attempt} api={api} onAttempt={onAttemptChanged}
        openUrl={inline ? openUrl : undefined} practice={practice} />}
    {attempt && attempt.status !== 'started' &&
      <AttemptReview key={attempt.id} attempt={attempt} practice={practice} actions={
        <>
          {attemptId && !practice ? <a className="secondary" href={openUrl}>Tilbage til videnstjekket</a>
            : mayStart && <button type="button" className="action" onClick={start} disabled={starting}>
              {starting ? 'Starter…' : retryLabel}
            </button>}
          {!attempt.results && <button type="button" className="secondary"
            onClick={() => setReload(current => current + 1)}>Opdatér resultat</button>}
        </>
      } />}
  </section>;
}

function questionOf(attempt: Attempt, index: number) {
  return attempt.questions[Math.max(0, Math.min(index, attempt.questions.length - 1))];
}

function draftOf(attempt: Attempt): Draft {
  return { questionIndex: Math.max(0, Math.min(attempt.questionIndex, attempt.questions.length - 1)), answers: attempt.answers };
}

function answered(answer: Answer) {
  return answer.selectedOptions.length > 0 || Boolean(answer.text?.trim());
}

function DraftStatus({ busy, error, dirty, retry }: { busy: boolean; error: string; dirty: boolean; retry: () => void }) {
  if (error) return <div className="lup-knowledge-error" role="alert">
    <p>{error}</p>
    <button type="button" className="secondary" onClick={retry} disabled={busy}>Prøv igen</button>
  </div>;
  return <p className="lup-knowledge-meta" role="status">{busy || dirty ? 'Gemmer…' : 'Gemt'}</p>;
}

function AttemptForm({ initialAttempt, api, onAttempt, openUrl, practice }: {
  initialAttempt: Attempt; api: CheckApi; onAttempt: (attempt: Attempt) => void; openUrl?: string; practice: boolean;
}) {
  const [draft, setDraft] = useState(() => draftOf(initialAttempt));
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const latest = useRef(initialAttempt);
  const savedDraft = useRef(draft);
  const currentDraft = useRef(draft);
  const timer = useRef<number | null>(null);
  const writer = useRef<Promise<Attempt | null> | null>(null);
  const live = useRef(true);
  const focusNext = useRef(practice);
  const prompt = useRef<HTMLDivElement>(null);
  const fieldId = useId();
  const question = questionOf(initialAttempt, draft.questionIndex);

  useEffect(() => {
    live.current = true;
    return () => {
      live.current = false;
      if (timer.current !== null) clearTimeout(timer.current);
    };
  }, []);

  useEffect(() => {
    if (!focusNext.current) return;
    focusNext.current = false;
    prompt.current?.focus();
  }, [draft.questionIndex, confirming]);

  async function adoptServerAttempt() {
    const next = await api.attempt(latest.current.id);
    if (!live.current) return;
    if (next.status !== 'started') {
      onAttempt(next);
      return;
    }
    latest.current = next;
    const serverDraft = draftOf(next);
    currentDraft.current = savedDraft.current = serverDraft;
    setDraft(serverDraft);
    setConfirming(false);
  }

  async function flush(): Promise<Attempt | null> {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    if (writer.current) return writer.current;
    if (currentDraft.current === savedDraft.current) {
      setSaveError('');
      return latest.current;
    }

    setSaving(true);
    setSaveError('');
    const work = (async () => {
      try {
        while (currentDraft.current !== savedDraft.current) {
          const sent = currentDraft.current;
          const next = await api.save(latest.current, sent);
          latest.current = next;
          savedDraft.current = sent;
          if (next.status !== 'started') {
            if (live.current) onAttempt(next);
            return null;
          }
        }
        return latest.current;
      } catch (failure) {
        if (!live.current) return null;
        if (failure instanceof KnowledgeCheckError && failure.code === 'attempt_changed') {
          try {
            await adoptServerAttempt();
            setSaveError('Forsøget er blevet opdateret. De senest gemte svar er hentet.');
            return null;
          } catch (refreshError) {
            setSaveError(message(refreshError, 'De senest gemte svar kunne ikke hentes. Prøv igen.'));
            return null;
          }
        }
        setSaveError(message(failure, 'Svaret eller fremdriften kunne ikke gemmes. Prøv igen.'));
        return null;
      }
    })();
    writer.current = work;
    try { return await work; }
    finally {
      writer.current = null;
      if (live.current) setSaving(false);
    }
  }

  function update(next: Draft, wait = 0) {
    currentDraft.current = next;
    setDraft(next);
    setSubmitError('');
    if (timer.current !== null) clearTimeout(timer.current);
    timer.current = window.setTimeout(() => { void flush(); }, wait);
  }

  function choose(answer: Answer, wait = 0) {
    const previous = currentDraft.current;
    const answers = previous.answers.filter(current => current.questionId !== answer.questionId);
    if (answered(answer)) answers.push(answer);
    update({ ...previous, answers }, wait);
  }

  function move(index: number) {
    if (index === currentDraft.current.questionIndex && !confirming) {
      prompt.current?.focus();
      return;
    }
    focusNext.current = true;
    setConfirming(false);
    update({ ...currentDraft.current, questionIndex: index });
  }

  async function submit() {
    if (submitting) return;
    setSubmitting(true);
    setSubmitError('');
    try {
      const saved = await flush();
      if (!saved) return;
      const result = await api.submit(saved);
      if (live.current) onAttempt(result);
    } catch (failure) {
      if (!live.current) return;
      if (failure instanceof KnowledgeCheckError && failure.code === 'attempt_changed') {
        try {
          await adoptServerAttempt();
          setSubmitError('Forsøget er blevet opdateret. Gennemgå svarene inden aflevering.');
        } catch (refreshError) {
          setSubmitError(message(refreshError, 'De senest gemte svar kunne ikke hentes. Prøv igen.'));
        }
      } else {
        setSubmitError(message(failure, 'Forsøget kunne ikke afleveres. Prøv igen.'));
      }
    } finally {
      if (live.current) setSubmitting(false);
    }
  }

  async function open(event: MouseEvent<HTMLAnchorElement>) {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    const href = event.currentTarget.href;
    if (await flush()) window.location.assign(href);
  }

  if (!question) return <p>Der er ingen spørgsmål i dette videnstjek.</p>;
  const count = initialAttempt.questions.length;
  const answeredIds = new Set(draft.answers.filter(answered).map(answer => answer.questionId));
  const missing = initialAttempt.questions.findIndex(item => !answeredIds.has(item.id));
  const answeredCount = answeredIds.size;
  const selected = draft.answers.find(answer => answer.questionId === question.id);
  const options = question.kind === 'boolean' && question.options.length === 0 ? ['Sandt', 'Falsk'] : question.options;
  const disabled = submitting;
  const hasNext = draft.questionIndex + 1 < count;

  function changeOption(index: number, checked: boolean) {
    if (!question) return;
    const previous = currentDraft.current.answers.find(answer => answer.questionId === question.id)?.selectedOptions ?? [];
    const selectedOptions = question.kind !== 'multiple' ? [index]
      : checked ? [...previous, index] : previous.filter(value => value !== index);
    choose({ questionId: question.id, selectedOptions, text: null });
  }

  function next() {
    if (hasNext) move(draft.questionIndex + 1);
    else {
      focusNext.current = true;
      setConfirming(true);
      void flush();
    }
  }

  function keyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (!question || disabled || confirming || event.defaultPrevented || event.repeat || event.nativeEvent.isComposing
        || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    if (!(event.target instanceof HTMLElement) || event.target.closest(
      'button, a, textarea, select, [contenteditable], input:not([type=radio]):not([type=checkbox])',
    )) return;
    if (event.key === 'Enter') {
      event.preventDefault();
      next();
      return;
    }
    const letter = event.key.toLowerCase();
    if (question.kind === 'short' || !/^[a-z]$/.test(letter)) return;
    const index = letter.charCodeAt(0) - 97;
    if (index >= options.length) return;
    event.preventDefault();
    const picked = currentDraft.current.answers.find(answer => answer.questionId === question.id)?.selectedOptions;
    changeOption(index, !picked?.includes(index));
  }

  return <>
    <div className="lup-knowledge-topline">
      {practice ? <>
        <span>Spørgsmål {draft.questionIndex + 1} af {count}</span>
        <label htmlFor={fieldId + '-progress'}>{answeredCount} af {count} besvaret</label>
      </> : <label htmlFor={fieldId + '-progress'}>Spørgsmål {draft.questionIndex + 1} af {count}</label>}
      {openUrl && <a className="secondary" href={openUrl} onClick={open}>Åbn på egen side</a>}
    </div>
    <progress className="lup-knowledge-progress" id={fieldId + '-progress'} max={count}
      value={practice ? answeredCount : draft.questionIndex + 1} />
    <div className={practice ? 'lup-knowledge-workspace' : undefined}>
      <div className={practice ? 'lup-knowledge-question' : undefined} onKeyDown={practice ? keyDown : undefined}>
        <div className="lup-knowledge-prompt" tabIndex={-1} ref={prompt}>
          {confirming ? <h3>Aflever videnstjek</h3> : <QuestionText key={question.id} text={question.prompt} copyCode={practice} />}
        </div>
        {confirming ? <>
          <p>{answeredCount} af {count} spørgsmål besvaret.</p>
          {missing !== -1 && <p>Besvar alle spørgsmål, før du afleverer.</p>}
          <div className="lup-knowledge-actions">
            <button type="button" className="secondary" onClick={() => move(missing === -1 ? draft.questionIndex : missing)} disabled={disabled}>
              {missing === -1 ? 'Gennemgå svar' : 'Gå til første ubesvarede'}
            </button>
            <button type="button" className="action" onClick={submit} disabled={disabled || missing !== -1}>
              {submitting ? 'Afleverer…' : 'Aflever'}
            </button>
          </div>
        </> : <>
          {question.kind === 'short'
            ? <label className="lup-knowledge-short">Dit svar
              <textarea rows={3} maxLength={1000} value={selected?.text ?? ''} disabled={disabled}
                onChange={event => choose({ questionId: question.id, selectedOptions: [], text: event.target.value }, 350)}
                onBlur={() => { if (currentDraft.current !== savedDraft.current && !saveError) void flush(); }} />
            </label>
            : <fieldset className="lup-knowledge-options" disabled={disabled}>
              <legend>{question.kind === 'multiple' ? 'Vælg alle korrekte svar' : 'Vælg ét svar'}</legend>
              {options.map((option, index) => {
                const checked = selected?.selectedOptions.includes(index) ?? false;
                const letter = String.fromCharCode(65 + index);
                return <label className={checked ? 'is-selected' : ''} key={index}>
                  <input name={fieldId + '-' + question.id} type={question.kind === 'multiple' ? 'checkbox' : 'radio'} checked={checked}
                    aria-keyshortcuts={practice ? letter : undefined} onChange={event => changeOption(index, event.target.checked)} />
                  {practice && <kbd aria-hidden="true">{letter}</kbd>}
                  <QuestionText text={option} />
                </label>;
              })}
            </fieldset>}
          <div className="lup-knowledge-actions">
            <button type="button" className="secondary" disabled={disabled || draft.questionIndex === 0}
              onClick={() => move(draft.questionIndex - 1)}>Forrige</button>
            <button type="button" className="action" disabled={disabled} onClick={next} aria-keyshortcuts={practice ? 'Enter' : undefined}>
              {hasNext ? 'Næste' : 'Gå til aflevering'}
            </button>
          </div>
          {practice && options.length > 1 && question.kind !== 'short' && <p className="lup-knowledge-key-hint">
            <kbd>A</kbd>–<kbd>{String.fromCharCode(64 + options.length)}</kbd> vælger svar. <kbd>Enter</kbd> går videre.
          </p>}
        </>}
        {submitError && <p className="lup-knowledge-error" role="alert">{submitError}</p>}
        <DraftStatus busy={saving || submitting} error={saveError} dirty={draft !== savedDraft.current} retry={() => { void flush(); }} />
      </div>
      {practice && <nav className="lup-knowledge-question-nav" aria-label="Spørgsmålsoversigt">
        <h3>Spørgsmålsoversigt</h3>
        <ol>
          {initialAttempt.questions.map((item, index) => {
            const complete = answeredIds.has(item.id);
            const here = !confirming && index === draft.questionIndex;
            return <li key={item.id}>
              <button type="button" aria-current={here ? 'step' : undefined}
                aria-label={`Spørgsmål ${index + 1}, ${complete ? 'besvaret' : 'ubesvaret'}`}
                className={complete ? 'is-answered' : undefined} disabled={disabled} onClick={() => move(index)}>
                {index + 1}{complete && <span aria-hidden="true">✓</span>}
              </button>
            </li>;
          })}
        </ol>
      </nav>}
    </div>
  </>;
}

function CopyableCode({ code }: { code: string }) {
  const [status, setStatus] = useState('');

  async function copy() {
    try {
      await navigator.clipboard.writeText(code);
      setStatus('Koden er kopieret.');
    } catch {
      setStatus('Koden kunne ikke kopieres. Markér den for at kopiere manuelt.');
    }
  }

  return <span className="lup-knowledge-code-block">
    <span className="lup-knowledge-code"><code>{code}</code></span>
    <button className="secondary" type="button" onClick={copy}>Kopiér kode</button>
    <span role="status" className="lup-knowledge-copy-status">{status}</span>
  </span>;
}

function QuestionText({ text, copyCode = false }: { text: string; copyCode?: boolean }) {
  let parts = text.split(/\x60{3}[^\r\n\x60]*\r?\n([\s\S]*?)\x60{3}/g);
  if (copyCode && parts.length === 1) {
    // The legacy C# prompts put complete examples directly after the question.
    const example = /^([\s\S]+?[?:])\s+([\s\S]+[;}])(\?)?$/.exec(text);
    const startsWithCode = /^(?:(?:for|foreach|while|if|switch)\s*\(|(?:public\s+)?(?:bool|int|string|var|double|[A-Z]\w*)\s+[A-Za-z_]\w*\s*[=(]|Console\.Write(?:Line)?\s*\()/;
    if (example && startsWithCode.test(example[2])) {
      const [, intro, code, suffix] = example;
      if (!suffix) parts = [intro, code];
      else if (intro.endsWith(':')) parts = [intro.slice(0, -1) + suffix, code];
    }
  }
  return <span className="lup-knowledge-text">
    {parts.map((part, index) => index % 2
      ? copyCode ? <CopyableCode key={index} code={part.trimEnd()} />
        : <span className="lup-knowledge-code" key={index}><code>{part.trimEnd()}</code></span>
      : <Fragment key={index}>{part.split(/\x60([^\x60\r\n]+)\x60/g).map((word, i) =>
        i % 2 ? <code key={i}>{word}</code> : <Fragment key={i}>{word}</Fragment>)}</Fragment>)}
  </span>;
}

function learnMore(value: string | null | undefined): URL | null {
  if (!value) return null;
  try {
    const url = new URL(value, window.location.origin);
    return url.origin === window.location.origin || url.protocol === 'https:' ? url : null;
  } catch {
    return null;
  }
}

function AttemptReview({ attempt, actions, practice }: { attempt: Attempt; actions: ReactNode; practice: boolean }) {
  const [onlyWrong, setOnlyWrong] = useState(false);
  const wrongCount = attempt.results?.filter(result => !result.correct).length ?? 0;

  return <div className="lup-knowledge-result">
    <h3>{attempt.score == null ? 'Afleveret' : `${attempt.score} af ${attempt.questions.length} point`}</h3>
    {attempt.status === 'submitted' && <p>Venter på underviserens vurdering.</p>}
    {attempt.status === 'graded' && !attempt.results && <p>Facit og forklaringer vises, når underviseren frigiver dem.</p>}
    {practice && attempt.results && <div className="lup-knowledge-review-filters" role="group" aria-label="Vis spørgsmål i gennemgangen">
      <button className="secondary" type="button" aria-pressed={!onlyWrong} onClick={() => setOnlyWrong(false)}>Alle spørgsmål</button>
      <button className="secondary" type="button" aria-pressed={onlyWrong} onClick={() => setOnlyWrong(true)}>Kun forkerte ({wrongCount})</button>
    </div>}
    {onlyWrong && wrongCount === 0 && <p role="status">Du havde ingen forkerte svar.</p>}
    {attempt.results && (!onlyWrong || wrongCount > 0) && <ol className="lup-knowledge-review">
      {attempt.questions.map((question, index) => {
        const review = attempt.results?.find(item => item.questionId === question.id);
        if (!review || onlyWrong && review.correct) return null;
        const answer = attempt.answers.find(item => item.questionId === question.id);
        const options = question.kind === 'boolean' && question.options.length === 0 ? ['Sandt', 'Falsk'] : question.options;
        const selection = answer?.text || answer?.selectedOptions.map(index => options[index]).filter(Boolean).join(' · ');
        const expected = review.acceptedAnswers?.length ? review.acceptedAnswers
          : review.correctOptions?.map(index => options[index]).filter(Boolean);
        const link = learnMore(review.learnMoreUrl);
        return <li key={question.id} value={index + 1}>
          <p className={review.correct ? 'lup-knowledge-correct' : 'lup-knowledge-incorrect'}>{review.correct ? 'Korrekt' : 'Forkert'}</p>
          <div className="lup-knowledge-prompt"><QuestionText text={question.prompt} /></div>
          {selection && <p>Dit svar er <QuestionText text={selection} /></p>}
          {expected && expected.length > 0 && <p>Facit er <QuestionText text={expected.join(' · ')} /></p>}
          {review.explanation && <p><QuestionText text={review.explanation} /></p>}
          {review.optionExplanations?.some(Boolean) &&
            <ul className="lup-knowledge-option-explanations" aria-label="Forklaringer til svarmuligheder">
              {options.map((option, index) => {
                const explanation = review.optionExplanations?.[index];
                return explanation && <li key={index}>
                  <strong><QuestionText text={option} /></strong>
                  <p><QuestionText text={explanation} /></p>
                </li>;
              })}
            </ul>}
          {review.comment && <p className="lup-knowledge-feedback">Underviserens kommentar. {review.comment}</p>}
          {link && <div className="lup-knowledge-source">
            <a href={link.href} rel={link.origin === window.location.origin ? undefined : 'noreferrer'}>{review.learnMoreTitle || 'Læs mere'}</a>
            {review.learnMoreDescription && <p>{review.learnMoreDescription}</p>}
          </div>}
        </li>;
      })}
    </ol>}
    <div className="lup-knowledge-actions">{actions}</div>
  </div>;
}
