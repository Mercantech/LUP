export type LiveKind = 'poll' | 'brainstorm' | 'word-cloud' | 'qa' | 'quiz';
export type LivePollType = 'choice' | 'mood' | 'scale' | 'text';

export const liveKinds: { kind: LiveKind; title: string; description: string; icon: string }[] = [
  { kind: 'poll', title: 'Afstemning', description: 'Stil et spørgsmål og saml svar under præsentationen', icon: '▥' },
  { kind: 'brainstorm', title: 'Brainstorm', description: 'Saml holdets idéer', icon: '✦' },
  { kind: 'word-cloud', title: 'Ordsky', description: 'Vis ord fra holdets svar', icon: '◌' },
  { kind: 'qa', title: 'Spørgsmål', description: 'Saml spørgsmål, som holdet kan stemme på', icon: '?' },
  { kind: 'quiz', title: 'Livequiz', description: 'Stil et quizspørgsmål med facit', icon: '✓' },
];

export interface LiveModuleInput {
  blockId: string;
  kind: LiveKind;
  prompt: string;
  pollType?: LivePollType;
  options: string[];
  correctChoice: number | null;
  durationSeconds: number | null;
}
export interface LiveModule extends LiveModuleInput { id: string }

export interface LiveEntry {
  id: string;
  text: string;
  approved: boolean;
  votes: number;
  yourVote: boolean;
  isMine: boolean;
  moderated?: boolean;
}
export interface LiveRound {
  id: string;
  moduleId: string;
  blockId: string;
  kind: LiveKind;
  pollType?: LivePollType | null;
  prompt: string;
  options: string[];
  open: boolean;
  showResults: boolean;
  deadlineUtc: string | null;
  hasAnswered: boolean;
  yourChoice: number | null;
  yourText: string | null;
  counts: number[] | null;
  correctChoice: number | null;
  entries: LiveEntry[];
  totalAnswers: number | null;
}
export interface LiveSnapshot {
  id: string;
  publishedRevision: number;
  slideIndex: number;
  revision: number;
  mayManage: boolean;
  reactions: { emoji: string; count: number }[];
  round: LiveRound | null;
  rankingVisible?: boolean;
  ranking?: { label: string; score: number }[];
}
export interface LiveHistory {
  id: string;
  startedAtUtc: string;
  endedAtUtc: string | null;
  publishedRevision: number;
  participantCount: number;
  rounds: { id: string; blockId: string; kind: LiveKind; prompt: string; options: string[]; totalAnswers: number; counts: number[] | null }[];
}
export type LiveResponse = { session: LiveSnapshot | null };

export interface LiveClient {
  read(signal: AbortSignal): Promise<LiveResponse>;
  command(segments: string[], body: object, signal: AbortSignal): Promise<LiveResponse>;
  module(id: string, signal: AbortSignal): Promise<LiveModule>;
  saveModule(input: LiveModuleInput, signal: AbortSignal): Promise<LiveModule>;
  history(signal: AbortSignal): Promise<LiveHistory[]>;
}

export class LiveRequestError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

function messageFor(status: number, code: unknown) {
  switch (code) {
    case 'publication_changed': return 'Forløbet er udgivet i en ny version. Genindlæs siden for at starte live.';
    case 'presentation_already_active': return 'Der kører allerede en livepræsentation for dette forløb.';
    case 'presentation_ended': return 'Livepræsentationen er afsluttet.';
    case 'round_closed': return 'Underviseren har lukket for svar.';
    case 'round_limit': return 'Præsentationen har nået grænsen for antal runder.';
    case 'answer_limit': return 'Der kan ikke sendes flere svar i denne runde.';
    case 'quiz_already_answered': return 'Dit quizsvar er allerede gemt.';
    case 'invalid_answer': return 'Kontrollér dit svar, og prøv igen.';
  }
  if (status === 401) return 'Log ind igen for at deltage live.';
  if (status === 403) return 'Du har ikke adgang til denne handling.';
  if (status === 409) return 'Livevisningen er ændret. Vent på den nye visning, og prøv igen.';
  if (status === 429) return 'Vent et øjeblik, før du prøver igen.';
  if (status === 400) return 'Kontrollér spørgsmålet og svarmulighederne.';
  return 'LUP kunne ikke hente livevisningen. Prøv igen.';
}

export function createLiveClient({
  apiBase, pageId, getToken,
}: { apiBase: string; pageId: string; getToken: (rejectedAccess?: string) => Promise<string> }): LiveClient {
  const root = apiBase.replace(/\/+$/, '') + '/notion/pages/' + encodeURIComponent(pageId) + '/live';
  async function request<T>(segments: string[], signal: AbortSignal, body?: object): Promise<T> {
    const url = [root, ...segments.map(encodeURIComponent)].join('/');
    const token = (await getToken()).replace(/^Bearer /i, '');
    const send = (access: string) => {
      if (signal.aborted) throw new DOMException('Livevisning lukket', 'AbortError');
      return fetch(url, {
        method: body === undefined ? 'GET' : 'POST', signal, cache: 'no-store',
        headers: { Authorization: 'Bearer ' + access, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    };
    let response = await send(token);
    if (response.status === 401 && !signal.aborted) {
      const refreshed = (await getToken(token)).replace(/^Bearer /i, '');
      if (refreshed && refreshed !== token) response = await send(refreshed);
    }
    if (!response.ok) {
      const details: { message?: unknown; error?: unknown } = await response.json().catch(() => ({}));
      const message = typeof details.message === 'string' ? details.message : messageFor(response.status, details.error);
      throw new LiveRequestError(response.status, message);
    }
    return response.json() as Promise<T>;
  }
  return {
    read: signal => request([], signal),
    command: (segments, body, signal) => request(segments, signal, body),
    module: (id, signal) => request(['modules', id], signal),
    saveModule: (input, signal) => request(['modules'], signal, input),
    history: signal => request(['history'], signal),
  };
}
