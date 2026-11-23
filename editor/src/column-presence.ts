import { useCallback, useSyncExternalStore } from 'react';
import { createExtension, type BlockNoteEditor, type ExtensionOptions } from '@blocknote/core';
import type { Awareness } from 'y-protocols/awareness';

type Editor = BlockNoteEditor<any, any, any>;

export const columnPresence = createExtension(({ options }: ExtensionOptions<{ awareness?: Awareness }>) => ({
  key: 'notion-column-presence',
  awareness: options.awareness,
}));

function hasOtherWriter(awareness?: Awareness) {
  if (!awareness) return false;
  for (const [id, state] of awareness.getStates()) {
    if (id !== awareness.clientID && state.user) return true;
  }
  return false;
}

export function columnLayoutShared(editor: Editor) {
  return hasOtherWriter(editor.getExtension(columnPresence)?.awareness);
}

export function useColumnLayoutShared(editor: Editor) {
  const awareness = editor.getExtension(columnPresence)?.awareness;
  const subscribe = useCallback((notify: () => void) => {
    awareness?.on('change', notify);
    return () => awareness?.off('change', notify);
  }, [awareness]);
  return useSyncExternalStore(subscribe, () => hasOtherWriter(awareness), () => false);
}
