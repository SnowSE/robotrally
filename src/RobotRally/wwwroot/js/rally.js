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
    star: () => { tone(N.E5 * 2, 0.08, { type: 'triangle', vol: 0.12 }); tone(N.A5 * 2, 0.22, { type: 'triangle', vol: 0.12, delay: 0.07 }); tone(N.E5 * 4, 0.12, { type: 'sine', vol: 0.04, delay: 0.12 }); },
    // Bumper cars: short and soft, because a whole room will be doing it at once.
    boop: () => tone(330 + Math.random() * 90, 0.09, { type: 'sine', to: 170, vol: 0.09 }),
    // King of the Hill (kid's own machine only).
    crown: () => seq([[N.G5, .09, 0], [N.C6, .09, .09], [N.E5 * 2, .3, .18]], { type: 'triangle', vol: 0.13 }),
    uncrown: () => seq([[N.E5, .1, 0], [N.C5, .1, .1], [N.G4, .3, .2]], { type: 'triangle', vol: 0.1 }),
    hint: () => seq([[N.E5, .09, 0], [N.A5, .2, .09]], { type: 'sine', vol: 0.12 }),
    fast: () => tone(500, 0.12, { type: 'triangle', to: 1400, vol: 0.1 }),
    slow: () => tone(900, 0.14, { type: 'triangle', to: 380, vol: 0.1 }),
    points: () => { for (let i = 0; i < 5; i++) tone(900 + i * 140, 0.05, { type: 'square', vol: 0.035, delay: i * 0.06 }); },
    champion: () => {
      sounds.finish();
      const fan = [[N.G5, .12], [N.G5, .12], [N.G5, .12], [N.C6, .6]];
      let t = 2.3;
      fan.forEach(([f, d]) => { tone(f, d, { type: 'square', vol: 0.06, delay: t }); tone(f / 2, d, { type: 'triangle', vol: 0.07, delay: t }); t += d + 0.02; });
      for (let i = 0; i < 6; i++) noise(0.4, { vol: 0.05, freq: 2500, to: 300, delay: 0.4 + i * 0.55 });
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

  // A bigger show: several bursts from different spots.
  function fireworks(bursts = 6) {
    for (let i = 0; i < bursts; i++) {
      setTimeout(() => confetti(90, 0.15 + Math.random() * 0.7, 0.2 + Math.random() * 0.4), i * 450);
    }
  }

  // ---------- lobby bumper cars ----------
  // The server sends ~20 frames a second, each stamped with the arena clock. Every screen refresh we draw the
  // cars a little in the past (DELAY), blending between the two frames on either side of that moment. That keeps
  // motion smooth even when frames arrive unevenly. (CSS transitions can't do this: every new frame restarts them,
  // which makes the car stop-start about 20 times a second.)
  // Blazor owns the car elements (one per racer, data-id); this only moves them and plays the bump effects.
  const arena = (() => {
    const W = 1600, H = 900;
    const TIER_EVERY = 10;     // seconds on the hill per crown upgrade (max 3)
    const DELAY = 0.09;        // seconds behind the newest frame: covers one late frame
    const KEEP = 12;           // frames kept per board
    const boards = new Map();  // element -> state
    let looping = false;

    function attach(el, me, sound) {
      if (!el) return;
      const st = { me, sound: !!sound, scale: 1, lastBoop: 0, cars: new Map(), snaps: [], offset: null, offsets: [], pos: new Map(),
                   king: null, kingSince: null, tier: 0, recordHit: false };
      boards.set(el, st);
      const measure = () => {
        st.scale = el.clientWidth / W;
        el.style.setProperty('--arena-scale', st.scale.toFixed(4));
      };
      measure();
      st.ro = new ResizeObserver(measure);
      st.ro.observe(el);
      if (!looping) { looping = true; requestAnimationFrame(loop); }
    }
    function detach(el) {
      const st = el && boards.get(el);
      if (st) { st.ro?.disconnect(); boards.delete(el); }
    }
    function car(el, st, id) {
      let c = st.cars.get(id);
      if (!c || !c.isConnected) {
        c = el.querySelector(`.arena-car[data-id="${CSS.escape(id)}"]`);
        if (c) st.cars.set(id, c);
      }
      return c;
    }

    function frame(el, f) {
      const st = el && boards.get(el);
      if (!st || !f) return;
      const now = performance.now() / 1000;
      // Map arena time to local time with the smallest recent (arrival - arena time): the least-delayed frame.
      // A big change means the arena was parked and resumed (its clock paused), so start over.
      const off = now - f.t;
      if (st.offset !== null && Math.abs(off - st.offset) > 0.5) { st.offsets = []; st.snaps = []; }
      st.offsets.push(off);
      if (st.offsets.length > 40) st.offsets.shift();
      st.offset = Math.min(...st.offsets);
      const cars = new Map();
      for (const [id, x, y, angle] of f.cars) cars.set(id, [x, y, angle]);
      const hill = f.hill || null;
      const last = st.snaps[st.snaps.length - 1];
      if (last && f.t <= last.t) { last.cars = cars; last.hill = hill; } // same moment (e.g. a parked arena re-sent): just refresh it
      else st.snaps.push({ t: f.t, cars, hill });
      if (st.snaps.length > KEEP) st.snaps.shift();

      // Bump effects wait for the drawing to catch up, so the 💥 lands when the cars visibly touch.
      if (f.bumps?.length) setTimeout(() => bumps(el, st, f.bumps), DELAY * 1000);
      if (f.hc?.length) setTimeout(() => hillChanges(el, st, f.hc), DELAY * 1000);
    }

    // ----- King of the Hill -----
    const fmt = s => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;
    const place = (node, st, x, y) => { node.style.left = (x * st.scale).toFixed(1) + 'px'; node.style.top = (y * st.scale).toFixed(1) + 'px'; };

    function toast(el, text, big) {
      el.querySelectorAll('.arena-toast').forEach(x => x.remove());
      const t = document.createElement('div');
      t.className = 'arena-toast' + (big ? ' big' : '');
      t.textContent = text;
      el.appendChild(t);
      setTimeout(() => t.remove(), big ? 2200 : 1600);
    }

    // Runs every screen refresh: crown on the holder, the ticking timer, crown upgrades, and the record.
    function drawHill(el, st, hill, t) {
      const holder = hill?.[0] ?? null;
      const since = hill?.[1] ?? null;
      const ring = el.querySelector('.arena-hill');
      if (holder !== st.king || since !== st.kingSince) {
        if (st.king) car(el, st, st.king)?.classList.remove('king', 't1', 't2', 't3');
        st.king = holder; st.kingSince = since; st.tier = 0; st.recordHit = false;
        if (ring) { ring.dataset.tier = '0'; delete ring.dataset.recordHit; }
      }
      if (!holder) return;
      const c = car(el, st, holder);
      if (c && !c.classList.contains('king')) c.classList.add('king');
      const secs = Math.max(0, t - since);
      const text = fmt(secs);
      for (const x of el.querySelectorAll('.hill-time')) if (x.textContent !== text) x.textContent = text;

      const tier = Math.min(3, Math.floor(secs / TIER_EVERY));
      if (tier > st.tier) {
        st.tier = tier;
        c?.classList.remove('t1', 't2', 't3');
        c?.classList.add('t' + tier);
        if (ring) ring.dataset.tier = String(tier);
        if (holder === st.me) { sfx('star'); toast(el, `👑 ${tier * TIER_EVERY} seconds!`); }
      }
      if (ring && ring.dataset.tier !== String(st.tier)) ring.dataset.tier = String(st.tier); // Blazor redrew it

      const record = parseFloat(el.dataset.record);
      if (!st.recordHit && record > 0 && secs > record) {
        st.recordHit = true;
        if (holder === st.me) { sfx('win'); toast(el, '🏆 New record!', true); }
      }
      if (ring) { if (st.recordHit) ring.dataset.recordHit = ''; else delete ring.dataset.recordHit; }
    }

    // The hill changed hands: everyone sees the knockout; the kids involved get their own moment.
    function hillChanges(el, st, list) {
      if (!el.isConnected) return;
      for (const [holder, prev, by] of list) {
        if (by && prev) {
          const burst = document.createElement('div');
          burst.className = 'hill-burst';
          burst.textContent = 'DETHRONED!';
          place(burst, st, W / 2, H / 2);
          el.appendChild(burst);
          setTimeout(() => burst.remove(), 1300);

          // The crown hops from the old holder to the new one (or just pops off if nobody took it).
          const from = st.pos.get(prev), to = holder && st.pos.get(holder);
          if (from) {
            const crown = document.createElement('div');
            crown.className = 'hill-crown-fly' + (to ? '' : ' lost');
            crown.textContent = '👑';
            place(crown, st, from[0], from[1]);
            el.appendChild(crown);
            if (to) requestAnimationFrame(() => requestAnimationFrame(() => place(crown, st, to[0], to[1])));
            setTimeout(() => crown.remove(), 900);
          }
        }
        if (!st.sound || !st.me) continue;
        if (holder === st.me && by === st.me) {
          sfx('crown'); toast(el, '👑 YOU STOLE THE CROWN!', true); confetti(110, 0.5, 0.4);
        } else if (holder === st.me) {
          sfx('crown'); toast(el, '👑 You rule the hill!');
        } else if (prev === st.me && by) {
          sfx('uncrown'); toast(el, '💥 Knocked off the hill!');
        } else if (by === st.me && prev) {
          sfx('pop'); toast(el, '💥 Nice hit!');
        }
      }
    }

    function bumps(el, st, list) {
      if (!el.isConnected) return;
      for (const [a, b, x, y, strength] of list) {
        const pow = document.createElement('div');
        pow.className = 'arena-pow' + (strength > 400 ? ' big' : '');
        pow.style.transform = `translate(${(x * st.scale).toFixed(1)}px, ${(y * st.scale).toFixed(1)}px)`;
        el.appendChild(pow);
        setTimeout(() => pow.remove(), 600);
        for (const id of [a, b]) {
          const fx = car(el, st, id)?.querySelector('.arena-fx');
          if (fx) { fx.classList.remove('bonk'); void fx.offsetWidth; fx.classList.add('bonk'); }
        }
        const now = performance.now();
        if (st.sound && (a === st.me || b === st.me) && now - st.lastBoop > 180) {
          st.lastBoop = now;
          sfx('boop');
        }
      }
    }

    function draw(el, st, now) {
      const snaps = st.snaps;
      if (!snaps.length) return;
      const t = Math.max(snaps[0].t, Math.min(snaps[snaps.length - 1].t, now - st.offset - DELAY));
      let i = snaps.length - 1;
      while (i > 0 && snaps[i - 1].t > t) i--;
      const a = snaps[Math.max(0, i - 1)], b = snaps[i];
      const k = b.t > a.t ? Math.min(1, Math.max(0, (t - a.t) / (b.t - a.t))) : 1;
      for (const [id, pb] of b.cars) {
        const c = car(el, st, id);
        if (!c) continue;
        const pa = a.cars.get(id) || pb;
        const x = pa[0] + (pb[0] - pa[0]) * k, y = pa[1] + (pb[1] - pa[1]) * k, ang = pa[2] + (pb[2] - pa[2]) * k;
        st.pos.set(id, [x, y]);
        c.style.transform = `translate(${(x * st.scale).toFixed(1)}px, ${(y * st.scale).toFixed(1)}px)`;
        const rot = c.firstElementChild;
        if (rot) rot.style.transform = `rotate(${ang.toFixed(1)}deg)`;
        if (!c.classList.contains('placed')) c.classList.add('placed');
      }
      drawHill(el, st, b.hill, t);
    }

    function loop() {
      if (!boards.size) { looping = false; return; }
      const now = performance.now() / 1000;
      for (const [el, st] of boards) {
        if (!el.isConnected) { detach(el); continue; }
        draw(el, st, now);
      }
      requestAnimationFrame(loop);
    }

    return { attach, detach, frame };
  })();

  // Arrow keys (or WASD) → a 4-bit mask sent to the server only when it changes.
  const drive = (() => {
    const bits = { ArrowUp: 1, KeyW: 1, ArrowDown: 2, KeyS: 2, ArrowLeft: 4, KeyA: 4, ArrowRight: 8, KeyD: 8 };
    let ref = null, mask = 0;
    const send = m => {
      if (m === mask) return;
      mask = m;
      ref?.invokeMethodAsync('Drive', m).catch(() => { });
    };
    const down = e => {
      const b = bits[e.code];
      if (!ref || !b || e.target?.closest?.('input, textarea, select')) return;
      e.preventDefault();
      send(mask | b);
    };
    const up = e => {
      const b = bits[e.code];
      if (!ref || !b) return;
      send(mask & ~b);
    };
    const release = () => send(0);
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    window.addEventListener('blur', release);
    document.addEventListener('visibilitychange', release);
    return {
      start: r => { ref = r; mask = 0; },
      stop: () => { release(); ref = null; },
    };
  })();

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

  return { store, sfx, setMuted, isMuted, confetti, fireworks, arena, drive, listenKeys, stopKeys, fitStage };
})();
