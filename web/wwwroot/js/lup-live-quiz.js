(() => {
  const key = code => 'lup-live-player:' + code;
  const cueNotes = {
    question: [392, 523],
    reveal: [523, 659, 784],
    leaderboard: [440, 554, 659],
    finished: [392, 523, 659, 784]
  };
  let audio;
  let soundEnabled = false;

  function playCue(phase) {
    const notes = cueNotes[phase];
    if (!soundEnabled || audio?.state !== 'running' || !Array.isArray(notes)) return;

    notes.forEach((frequency, i) => {
      const start = audio.currentTime + .09 + i * .115;
      const oscillator = audio.createOscillator();
      const envelope = audio.createGain();
      oscillator.type = 'triangle';
      oscillator.frequency.value = frequency;
      oscillator.connect(envelope);
      envelope.connect(audio.destination);
      envelope.gain.setValueAtTime(.001, start);
      envelope.gain.exponentialRampToValueAtTime(.065, start + .02);
      envelope.gain.exponentialRampToValueAtTime(.001, start + .16);
      oscillator.start(start);
      oscillator.stop(start + .17);
    });
  }

  window.lupLive = {
    getPlayerToken(code) {
      try { return sessionStorage.getItem(key(code)); }
      catch { return null; }
    },
    setPlayerToken(code, token) {
      try { sessionStorage.setItem(key(code), token); }
      catch { /* Waiting for or answering a question works in the active tab. */ }
    },
    clearPlayerToken(code) {
      try { sessionStorage.removeItem(key(code)); }
      catch { /* The browser can disable session storage. */ }
    },
    async copy(value) {
      if (!navigator.clipboard?.writeText) return false;
      try { await navigator.clipboard.writeText(value); return true; }
      catch { return false; }
    },
    async fullscreen(element = document.documentElement) {
      try {
        if (document.fullscreenElement) { await document.exitFullscreen(); return true; }
        if (!element.requestFullscreen) return false;
        await element.requestFullscreen();
        return true;
      } catch { return false; }
    },
    async enableSound(enabled) {
      soundEnabled = false;
      if (!enabled || !window.AudioContext) return false;
      try {
        audio ??= new AudioContext();
        await audio.resume();
        soundEnabled = audio.state === 'running';
        playCue('question');
      } catch { /* Audio is optional when the device blocks playback. */ }
      return soundEnabled;
    },
    playCue
  };
})();
