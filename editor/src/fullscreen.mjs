/** @param {HTMLElement|null} element */
export async function enterFullscreen(element) {
  if (!element?.requestFullscreen) return false;
  try {
    await element.requestFullscreen();
    return element.ownerDocument.fullscreenElement === element;
  } catch {
    return false;
  }
}

/** @param {HTMLElement|null} element */
export async function leaveFullscreen(element) {
  const document = element?.ownerDocument;
  if (!element || !document?.fullscreenElement || !element.contains(document.fullscreenElement)) return false;
  try {
    await document.exitFullscreen();
    return true;
  } catch {
    return false;
  }
}
