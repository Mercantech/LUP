import React, { lazy, Suspense, useEffect, useState } from 'react';
import { useViewport } from './use-viewport';

const Runner = lazy(() => import('./knowledge-check-runner').then(module => ({ default: module.KnowledgeCheckRunner })));
type TokenProvider = (rejectedAccess?: string) => Promise<string | null>;
type Option = { id: string; title: string; holdId: string | null };

interface Props {
  publicationId: string;
  blockId: string;
  pageId: string;
  holdId?: string | null;
  apiBase: string;
  getToken: TokenProvider;
  display: 'draft' | 'published' | 'preview';
  onChange?: (id: string) => void;
}

export function NotionKnowledgeCheck({ publicationId, blockId, pageId, holdId, apiBase, getToken, display, onChange }: Props) {
  const [container, nearViewport] = useViewport<HTMLElement>();
  const [activated, setActivated] = useState(false);
  useEffect(() => { if (nearViewport) setActivated(true); }, [nearViewport]);
  return <section ref={container} className="notion-knowledge-check" contentEditable={false} aria-label="Videnstjek">
    {display !== 'published' && <header><strong>Videnstjek</strong></header>}
    {display === 'draft' && <Picker pageId={pageId} holdId={holdId} apiBase={apiBase} getToken={getToken}
      publicationId={publicationId} onChange={onChange} />}
    {display === 'preview' && <p>Eleven kan gennemføre videnstjekket på den udgivne side.</p>}
    {display === 'published' && (publicationId
      ? (nearViewport || activated) && <Suspense fallback={<p role="status">Henter videnstjek…</p>}>
        <Runner distributionId={publicationId} apiBase={apiBase} getToken={getToken} inline pageId={pageId} blockId={blockId} />
      </Suspense>
      : <p>Der er endnu ikke valgt et videnstjek.</p>)}
  </section>;
}

function Picker({ pageId, holdId, apiBase, getToken, publicationId, onChange }:
  Pick<Props, 'pageId' | 'holdId' | 'apiBase' | 'getToken' | 'publicationId' | 'onChange'>) {
  const [options, setOptions] = useState<Option[]>([]);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    const load = async () => {
      try {
        const url = apiBase.replace(/\/+$/, '') + '/notion/pages/' + encodeURIComponent(pageId) + '/knowledge-checks';
        const token = await getToken();
        if (!token) throw new Error('missing_token');
        const fetchOptions = (access: string) => fetch(url, { signal: controller.signal, headers: { authorization: 'Bearer ' + access } });
        let response = await fetchOptions(token);
        if (response.status === 401) {
          const renewed = await getToken(token);
          if (renewed && renewed !== token) response = await fetchOptions(renewed);
        }
        if (!response.ok) throw new Error('knowledge_checks_unavailable');
        const items: Option[] = await response.json();
        if (!controller.signal.aborted) {
          setOptions(items);
          setState('ready');
        }
      } catch {
        if (!controller.signal.aborted) setState('error');
      }
    };
    void load();
    return () => controller.abort();
  }, [pageId, apiBase, getToken, reload]);

  const selected = options.some(item => item.id === publicationId);
  const createUrl = '/videnstjek/rediger/ny' + (holdId ? '?hold=' + encodeURIComponent(holdId) : '');
  return <div className="notion-knowledge-picker">
    {state === 'loading' && <p role="status">Henter udgivelser…</p>}
    {state === 'error' && <p role="alert">Udgivelserne kunne ikke hentes.</p>}
    <label>Udgivelse
      <select value={publicationId} disabled={state !== 'ready' || !onChange} onChange={event => onChange?.(event.target.value)}>
        <option value="">Vælg et videnstjek</option>
        {publicationId && !selected && <option value={publicationId}>Valgt videnstjek er ikke på listen</option>}
        {options.map(item => <option key={item.id} value={item.id}>{item.title} · {item.holdId ? 'Dette hold' : 'Fri øvelse'}</option>)}
      </select>
    </label>
    {state === 'ready' && !options.length && <p>Der er ingen åbne udgivelser til denne notesbog endnu.</p>}
    <div className="notion-block-actions">
      <a href={createUrl} target="_blank" rel="noopener noreferrer">Opret videnstjek <span className="notion-knowledge-new-tab">(ny fane)</span></a>
      <button type="button" disabled={state === 'loading'} onClick={() => { setState('loading'); setReload(value => value + 1); }}>Genindlæs udgivelser</button>
    </div>
  </div>;
}
