export function currentPageTitle() {
    const main = document.getElementById('main-content');
    const notebookTitle = main?.querySelector('.notion-toolbar-title')?.textContent;
    const heading = main?.querySelector('h1')?.textContent;
    const title = document.title.replace(/\s*[·|–]\s*LUP\s*$/i, '');
    return (notebookTitle?.trim() || heading?.trim() || (title.trim() === 'LUP' ? '' : title.trim()))
        .replace(/[\u0000-\u001f\u007f-\u009f]/g, ' ');
}
