// Robot Rally browser helpers: storage, synthesized sound, confetti, drag & drop shim, stage scaling, keys.
window.rally = (() => {
  // ---------- storage ----------
  const store = {
    get: k => { try { return localStorage.getItem(k); } catch { return null; } },
    set: (k, v) => { try { localStorage.setItem(k, v); } catch { } },
    remove: k => { try { localStorage.removeItem(k); } catch { } },
  };

  // ---------- sound (Web Audio, no files) ----------
  let ctx = null;
  let muted = store.get('rr-muted') === '1';
  function ac() {
    if (!ctx) {
      const C = window.AudioContext || window.webkitAudioContext;
      if (!C) return null;
      ctx = new C();
    }
    if (ctx.state === 'suspended') ctx.resume();
    return ctx;
  }
  // Browsers only allow audio after a user gesture; unlock on the first one.
  ['pointerdown', 'keydown'].forEach(ev => window.addEventListener(ev, () => ac(), { capture: true }));

  function tone(freq, dur, { type = 'sine', vol = 0.2, to = null, delay = 0, attack = 0.008 } = {}) {
    const c = ac(); if (!c || muted) return;
    const t = c.currentTime + delay;
    const o = c.createOscillator(), g = c.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    if (to) o.frequency.exponentialRampToValueAtTime(to, t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(c.destination);
    o.start(t); o.stop(t + dur + 0.05);
  }
  function noise(dur, { vol = 0.2, delay = 0, freq = 1000, to = null } = {}) {
    const c = ac(); if (!c || muted) return;
    const t = c.currentTime + delay;
    const buf = c.createBuffer(1, Math.floor(c.sampleRate * dur), c.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    const s = c.createBufferSource(); s.buffer = buf;
    const f = c.createBiquadFilter(); f.type = 'bandpass'; f.Q.value = 0.8;
    f.frequency.setValueAtTime(freq, t);
    if (to) f.frequency.exponentialRampToValueAtTime(to, t + dur);
    const g = c.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f).connect(g).connect(c.destination);
    s.start(t); s.stop(t + dur);
  }
  const seq = (notes, opts) => notes.forEach(([f, d, at]) => tone(f, d, { ...opts, delay: at }));
  const N = { C4: 262, D4: 294, E4: 330, F4: 349, G4: 392, A4: 440, B4: 494, C5: 523, D5: 587, E5: 659, F5: 698, G5: 784, A5: 880, C6: 1047 };

  const sounds = {
    pick: () => tone(660, 0.06, { type: 'square', vol: 0.05 }),
    drop: () => tone(420, 0.11, { to: 900, vol: 0.16 }),
    trash: () => tone(420, 0.22, { type: 'sawtooth', to: 70, vol: 0.07 }),
    deny: () => { tone(170, 0.12, { type: 'square', vol: 0.07 }); tone(150, 0.16, { type: 'square', vol: 0.07, delay: 0.14 }); },
    click: () => tone(900, 0.04, { type: 'triangle', vol: 0.08 }),
    run: () => seq([[N.C5, .1, 0], [N.E5, .1, .08], [N.G5, .16, .16]], { type: 'triangle', vol: 0.16 }),
    step: () => { tone(110, 0.09, { type: 'square', vol: 0.035 }); tone(1400, 0.03, { type: 'triangle', vol: 0.035 }); },
    turn: () => tone(650, 0.09, { to: 1050, vol: 0.08 }),
    bonk: () => { tone(190, 0.32, { type: 'square', to: 45, vol: 0.17 }); noise(0.22, { vol: 0.35, freq: 350 }); tone(1200, 0.08, { type: 'triangle', vol: 0.06, delay: 0.05, to: 2400 }); },
    fail: () => seq([[N.G4, .22, 0], [370, .22, .24], [N.F4, .22, .48], [N.E4, .7, .72]], { type: 'triangle', vol: 0.15 }),
    dizzy: () => { for (let i = 0; i < 6; i++) tone(500 + (i % 2) * 300, 0.12, { type: 'sine', vol: 0.08, delay: i * 0.1 }); },
    win: () => {
      seq([[N.C5, .12, 0], [N.E5, .12, .12], [N.G5, .12, .24], [N.C6, .5, .36]], { type: 'square', vol: 0.06 });
      seq([[N.E5, .5, .36], [N.G5, .5, .36]], { type: 'triangle', vol: 0.06 });
    },
    levelup: () => { noise(0.45, { vol: 0.14, freq: 300, to: 5000 }); tone(400, 0.45, { to: 1700, vol: 0.08, delay: 0.08 }); },
    whoosh: () => noise(0.35, { vol: 0.1, freq: 600, to: 3500 }),
    pop: () => { tone(280, 0.13, { to: 1300, vol: 0.2 }); tone(1800, 0.05, { type: 'triangle', vol: 0.05, delay: 0.1 }); },
    count: () => tone(660, 0.2, { type: 'square', vol: 0.09 }),
    go: () => { tone(1320, 0.55, { type: 'square', vol: 0.08 }); tone(880, 0.55, { type: 'square', vol: 0.06 }); },
    trick: () => { tone(200, 0.16, { to: 950, vol: 0.15 }); tone(950, 0.22, { to: 300, vol: 0.12, delay: 0.16 }); },
    newblock: () => seq([[N.G5, .08, 0], [988, .08, .08], [1175, .08, .16], [1568, .35, .24]], { type: 'sine', vol: 0.14 }),
    finish: () => {
      seq([[N.C5, .14, 0], [N.C5, .14, .15], [N.C5, .14, .3], [N.E5, .45, .45], [N.D5, .14, .95], [N.E5, .14, 1.1], [N.G5, .9, 1.25]], { type: 'square', vol: 0.06 });
      seq([[N.C4, .5, .45], [N.G4, .9, 1.25]], { type: 'triangle', vol: 0.08 });
    },
    podium: () => {
      const m = [[N.G4, .15], [N.C5, .15], [N.E5, .15], [N.G5, .3], [N.E5, .15], [N.G5, .6],
                 [N.A4, .15], [N.D5, .15], [N.F5, .15], [N.A5, .3], [N.F5, .15], [N.A5, .6],
                 [N.G5, .15], [N.G5, .15], [N.G5, .15], [N.C6, .9]];
      let t = 0;
      m.forEach(([f, d]) => { tone(f, d * 0.95, { type: 'square', vol: 0.05, delay: t }); t += d; });
      seq([[N.C4, 1.2, 0], [N.F4, 1.2, 1.2], [N.G4, .6, 2.4], [N.C4, .9, 2.85]], { type: 'triangle', vol: 0.09 });
      for (let i = 0; i < 8; i++) noise(0.05, { vol: 0.08, freq: 6000, delay: i * 0.45 });
    },
  };

  function sfx(name) { try { sounds[name]?.(); } catch { } }
  function setMuted(m) { muted = !!m; store.set('rr-muted', muted ? '1' : '0'); }
  function isMuted() { return muted; }

  // ---------- confetti ----------
  const confettiColors = ['#ef3b3b', '#ffcf2e', '#2f7cf6', '#3dbb5f', '#ff8a1f', '#a259ff', '#ffffff'];
  function confetti(count = 140, x = 0.5, y = 0.45) {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) count = Math.min(count, 25);
    const layer = document.createElement('div');
    layer.className = 'confetti-layer';
    for (let i = 0; i < count; i++) {
      const p = document.createElement('i');
      const a = Math.random() * Math.PI * 2;
      const v = 150 + Math.random() * 450;
      p.style.left = (x * 100) + 'vw';
      p.style.top = (y * 100) + 'vh';
      p.style.background = confettiColors[i % confettiColors.length];
      p.style.setProperty('--dx', (Math.cos(a) * v) + 'px');
      p.style.setProperty('--dy', (Math.sin(a) * v * 0.7 - 220) + 'px');
      p.style.setProperty('--r', (Math.random() * 1440 - 720) + 'deg');
      p.style.animationDelay = (Math.random() * 0.12) + 's';
      if (i % 3 === 0) p.style.borderRadius = '50%';
      layer.appendChild(p);
    }
    document.body.appendChild(layer);
    setTimeout(() => layer.remove(), 3800);
  }

  // ---------- drag & drop shim ----------
  // Firefox needs setData() to start a drag; Blazor can't call it, so we do it here.
  document.addEventListener('dragstart', e => {
    if (!e.target?.closest?.('[draggable=true]')) return;
    try { e.dataTransfer.setData('text/plain', 'block'); e.dataTransfer.effectAllowed = 'copyMove'; } catch { }
    document.body.classList.add('dragging');
  }, true);
  document.addEventListener('dragenter', e => {
    const t = e.target?.closest?.('.drop-target');
    if (t) t.classList.add('drag-over');
  }, true);
  document.addEventListener('dragleave', e => {
    const t = e.target?.closest?.('.drop-target');
    if (t && !t.contains(e.relatedTarget)) t.classList.remove('drag-over');
  }, true);
  const endDrag = () => {
    document.body.classList.remove('dragging');
    document.querySelectorAll('.drag-over').forEach(x => x.classList.remove('drag-over'));
  };
  document.addEventListener('drop', endDrag, true);
  document.addEventListener('dragend', endDrag, true);

  // ---------- projector stage scaling ----------
  function fitStage() {
    const s = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
    document.documentElement.style.setProperty('--stage-scale', s.toFixed(4));
  }
  window.addEventListener('resize', fitStage);
  fitStage();

  // ---------- admin keys ----------
  let keyRef = null;
  const keys = ['Escape', ' ', 'ArrowRight', 'ArrowLeft', 'PageDown', 'PageUp'];
  window.addEventListener('keydown', e => {
    if (e.target?.closest?.('input, textarea, select')) return;
    if (e.key === 'f' || e.key === 'F') {
      if (!keyRef) return;
      if (document.fullscreenElement) document.exitFullscreen?.(); else document.documentElement.requestFullscreen?.();
      return;
    }
    if (!keyRef || !keys.includes(e.key)) return;
    if (e.key === ' ' && e.target?.closest?.('button')) e.target.blur();
    e.preventDefault();
    keyRef.invokeMethodAsync('OnKey', e.key).catch(() => { });
  });
  function listenKeys(ref) { keyRef = ref; }
  function stopKeys() { keyRef = null; }

  return { store, sfx, setMuted, isMuted, confetti, listenKeys, stopKeys, fitStage };
})();
