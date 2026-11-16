import { createRoot } from 'react-dom/client';
import type { Root } from 'react-dom/client';
import { KnowledgeCheckRunner } from './knowledge-check-runner';
import type { CharacterHostPresentation } from './knowledge-check-runner';

type Context = {
  distributionId: string;
  attemptId: string | null;
  account: string;
  apiBase: string;
};
type Callbacks = {
  invokeMethodAsync: (method: string, account: string, rejectedAccess: string | null) => Promise<string | null>;
};

const roots = new WeakMap<HTMLElement, Root>();

export function mount(element: HTMLElement, context: Context, callbacks: Callbacks, characterHosts?: CharacterHostPresentation[]) {
  let root = roots.get(element);
  if (!root) {
    root = createRoot(element);
    roots.set(element, root);
  }
  const { distributionId, attemptId, account, apiBase } = context;
  root.render(<KnowledgeCheckRunner key={`${distributionId}:${attemptId ?? ''}:${account}`}
    distributionId={distributionId} attemptId={attemptId} apiBase={apiBase} characterHosts={characterHosts}
    getToken={rejectedAccess => callbacks.invokeMethodAsync('GetKnowledgeCheckToken', account, rejectedAccess ?? null)} />);
}

export function unmount(element: HTMLElement) {
  roots.get(element)?.unmount();
  roots.delete(element);
}
