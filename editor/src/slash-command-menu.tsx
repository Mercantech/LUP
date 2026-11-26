import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { SuggestionMenu } from '@blocknote/core/extensions';
import { SuggestionMenuController, useExtensionState, type DefaultReactSuggestionItem, type SuggestionMenuProps } from '@blocknote/react';
import { searchSlashCommands, slashCategories } from './slash-command-search.mjs';
import './slash-command-menu.css';

type Command = DefaultReactSuggestionItem;

function CommandMenu({ items, loadingState, selectedIndex, onItemClick, categories, category, searching, onCategory }:
  SuggestionMenuProps<Command> & {
    categories: string[];
    category: string | undefined;
    searching: boolean;
    onCategory: (category: string | undefined) => void;
  }) {
  const list = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const container = list.current;
    const option = container?.querySelector<HTMLElement>('[aria-selected="true"]');
    if (!container || !option) return;
    const visible = container.getBoundingClientRect();
    const selected = option.getBoundingClientRect();
    if (selected.top < visible.top) container.scrollTop += selected.top - visible.top;
    if (selected.bottom > visible.bottom) container.scrollTop += selected.bottom - visible.bottom;
  }, [selectedIndex, items]);

  const sections = new Map<string, { item: Command; index: number }[]>();
  items.forEach((item, index) => {
    const group = item.group ?? 'Tekst';
    const section = sections.get(group);
    if (section) section.push({ item, index });
    else sections.set(group, [{ item, index }]);
  });

  return <div className="bn-suggestion-menu notion-command-menu">
    <header className="notion-command-header">
      <kbd aria-hidden="true">/</kbd>
      <strong>Indsæt indhold</strong>
      <span>Skriv for at søge</span>
    </header>
    {!searching && <div className="notion-command-categories" role="group" aria-label="Kategorier">
      <button type="button" aria-pressed={!category} onClick={() => onCategory(undefined)}>Alle</button>
      {categories.map(group => <button key={group} type="button" aria-pressed={category === group}
        onClick={() => onCategory(group)}>{group}</button>)}
    </div>}
    <div ref={list} id="bn-suggestion-menu" className="notion-command-list" role="listbox"
      aria-label="Indsæt indhold" aria-busy={loadingState !== 'loaded'}>
      {[...sections].map(([group, commands]) => <div key={group} className="notion-command-group" role="group" aria-label={group}>
        {!category && <div className="notion-command-label" aria-hidden="true">{group}</div>}
        {commands.map(({ item, index }) => <button key={item.title} type="button" role="option" tabIndex={-1}
          id={'bn-suggestion-menu-item-' + index} className="notion-command-option"
          aria-selected={index === selectedIndex} aria-label={item.title} aria-description={item.subtext}
          onMouseDown={event => event.preventDefault()} onClick={() => onItemClick?.(item)}>
          <span className="notion-command-icon" aria-hidden="true">{item.icon ?? '+'}</span>
          <span className="notion-command-copy">
            <strong>{item.title}</strong>
            {item.subtext && <small>{item.subtext}</small>}
          </span>
        </button>)}
      </div>)}
    </div>
    {items.length === 0 && <p className="notion-command-empty" role="status">
      {loadingState === 'loaded' ? 'Ingen værktøjer matcher din søgning.' : 'Henter værktøjer…'}
    </p>}
    <footer className="notion-command-footer" aria-hidden="true">
      <span><kbd>↑</kbd><kbd>↓</kbd> Navigér</span>
      <span><kbd>↵</kbd> Vælg</span>
      <span><kbd>Esc</kbd> Luk</span>
    </footer>
  </div>;
}

export function SlashCommandMenu({ commands }: { commands: Command[] }) {
  const query = useExtensionState(SuggestionMenu, {
    selector: state => state?.show && state.triggerCharacter === '/' ? state.query : null,
  });
  const [category, setCategory] = useState<string>();
  const searching = Boolean(query?.trim());
  const visibleCategory = searching ? undefined : category;
  const categories = useMemo(() => slashCategories.filter(group => commands.some(item => item.group === group)), [commands]);

  useEffect(() => {
    if (query === null) setCategory(undefined);
  }, [query]);

  const getItems = useCallback(async (value: string): Promise<Command[]> =>
    searchSlashCommands(commands, value, category), [commands, category]);
  const renderMenu = useCallback((props: SuggestionMenuProps<Command>) =>
    <CommandMenu {...props} categories={categories} category={visibleCategory} searching={searching}
      onCategory={setCategory} />, [categories, visibleCategory, searching]);

  // A category change starts keyboard selection at the top of the new list.
  return <SuggestionMenuController key={category ?? 'all'} triggerCharacter="/" getItems={getItems}
    suggestionMenuComponent={renderMenu} />;
}
