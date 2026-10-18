// NEXT XI — browser preview match engine.
// A compact JavaScript re-implementation of the C# match core (Assets/NextXI/Core/Match):
// the ball physics, kick solver, pitch geometry and xG model are ported 1:1; the AI and the
// rules are simplified (no fouls, cards, offside, substitutions or stamina).
(function (root) {
  'use strict';

  // ---------------------------------------------------------------- constants (PitchSpec, BallPhysics)
  const HL = 52.5, HW = 34, HGW = 3.66, GH = 2.44, GD = 2.0, POST_R = 0.06;
  const PA_DEPTH = 16.5, PA_HALF_W = 20.16, GA_DEPTH = 5.5;
  const R = 0.11, G = 9.81, DRAG = 0.0133, MAGNUS = 0.0032, REST = 0.62, BFRIC = 0.82;
  const ROLL_DEC = 0.45, ROLL_DAMP = 0.24, SPIN_DECAY = 0.35, MAXS = 45, POST_REST = 0.55, NET_DAMP = 0.12;
  const DT = 1 / 60;

  // ---------------------------------------------------------------- vector helpers (plain {x,y,z})
  const V = (x = 0, y = 0, z = 0) => ({ x, y, z });
  const add = (a, b) => V(a.x + b.x, a.y + b.y, a.z + b.z);
  const sub = (a, b) => V(a.x - b.x, a.y - b.y, a.z - b.z);
  const mul = (a, s) => V(a.x * s, a.y * s, a.z * s);
  const dot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
  const cross = (a, b) => V(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  const len = a => Math.sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
  const flat = a => V(a.x, 0, a.z);
  const flen = a => Math.sqrt(a.x * a.x + a.z * a.z);
  const fdist = (a, b) => Math.sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
  const fnorm = a => { const l = flen(a); return l > 1e-6 ? V(a.x / l, 0, a.z / l) : V(); };
  const lerpV = (a, b, t) => V(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
  const clamp = (v, a, b) => v < a ? a : (v > b ? b : v);
  const clamp01 = v => clamp(v, 0, 1);
  const lerp = (a, b, t) => a + (b - a) * t;
  const invLerp = (a, b, v) => Math.abs(b - a) < 1e-9 ? 0 : clamp01((v - a) / (b - a));
  const attr = (a, lo, hi) => lerp(lo, hi, clamp(a, 0, 99) / 99);
  const dirOf = ang => V(Math.cos(ang), 0, Math.sin(ang));
  const angOf = v => Math.atan2(v.z, v.x);
  const wrap = a => { while (a > Math.PI) a -= 2 * Math.PI; while (a < -Math.PI) a += 2 * Math.PI; return a; };
  const turnTo = (cur, target, max) => { const d = wrap(target - cur); return Math.abs(d) <= max ? target : wrap(cur + Math.sign(d) * max); };
  const rotY = (v, a) => { const c = Math.cos(a), s = Math.sin(a); return V(v.x * c - v.z * s, v.y, v.x * s + v.z * c); };

  // Deterministic PRNG (mulberry32) so headless tests are reproducible.
  function makeRng(seed) {
    let s = (seed >>> 0) || 1;
    const next = () => {
      s = (s + 0x6D2B79F5) >>> 0;
      let t = s;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
    return {
      next,
      range: (a, b) => a + (b - a) * next(),
      chance: p => next() < p,
      gauss: () => Math.sqrt(-2 * Math.log(Math.max(1e-7, next()))) * Math.cos(2 * Math.PI * next()),
      pick: arr => arr[Math.floor(next() * arr.length)]
    };
  }

  // ---------------------------------------------------------------- ball physics (port of BallPhysics.cs)
  const newBall = () => ({ p: V(0, R, 0), v: V(), s: V(), held: false });
  const cloneBall = b => ({ p: V(b.p.x, b.p.y, b.p.z), v: V(b.v.x, b.v.y, b.v.z), s: V(b.s.x, b.s.y, b.s.z), held: b.held });

  function ballStep(b, dt) {
    if (b.held) { b.v = V(); b.s = V(); return false; }
    const sp = len(b.v);
    let n = sp * dt > 0.25 ? Math.ceil(sp * dt / 0.25) : 1;
    if (n > 8) n = 8;
    const h = dt / n;
    let hit = false;
    for (let i = 0; i < n; i++) hit = integrate(b, h) || hit;
    if (!isFinite(b.p.x + b.p.y + b.p.z + b.v.x + b.v.y + b.v.z)) { b.p = V(0, R, 0); b.v = V(); b.s = V(); }
    return hit;
  }

  function integrate(b, h) {
    let v = b.v;
    const grounded = b.p.y <= R + 0.005 && Math.abs(v.y) < 0.6;
    if (grounded) {
      let f = V(v.x, 0, v.z);
      const s = flen(f);
      if (s > 1e-4) {
        const ns = Math.max(0, s - (ROLL_DEC + ROLL_DAMP * s) * h);
        f = mul(f, ns / s);
        const bend = mul(cross(V(0, b.s.y, 0), f), MAGNUS * 0.25);
        f = add(f, mul(bend, h));
      }
      v = f;
      b.p = V(b.p.x, R, b.p.z);
      b.s = V(v.z / R, b.s.y * Math.max(0, 1 - 3 * h), -v.x / R);
    } else {
      const s = len(v);
      const acc = add(add(V(0, -G, 0), mul(v, -DRAG * s)), mul(cross(b.s, v), MAGNUS));
      v = add(v, mul(acc, h));
      b.s = mul(b.s, Math.max(0, 1 - SPIN_DECAY * h));
    }
    const l = len(v);
    if (l > MAXS) v = mul(v, MAXS / l);
    const prev = b.p;
    const p = add(b.p, mul(v, h));
    if (p.y < R) {
      p.y = R;
      if (v.y < -0.6) { v = V(v.x * BFRIC, -v.y * REST, v.z * BFRIC); b.s = mul(b.s, 0.6); }
      else v = V(v.x, 0, v.z);
    }
    b.p = p; b.v = v;
    let hit = collideFrame(b, prev, -1);
    hit = collideFrame(b, prev, 1) || hit;
    return hit;
  }

  function collideFrame(b, prev, side) {
    const lineX = side * HL;
    if (Math.abs(b.p.x - lineX) > GD + 1) return false;
    const r = R + POST_R;
    let hit = collideSegment(b, V(lineX, 0, -HGW), V(lineX, GH, -HGW), r);
    hit = collideSegment(b, V(lineX, 0, HGW), V(lineX, GH, HGW), r) || hit;
    hit = collideSegment(b, V(lineX, GH, -HGW), V(lineX, GH, HGW), r) || hit;
    const depthIn = (b.p.x - lineX) * side;
    const wasInside = (prev.x - lineX) * side > 0 && Math.abs(prev.z) < HGW && prev.y < GH;
    const nowMouth = Math.abs(b.p.z) < HGW && b.p.y < GH;
    if (depthIn > 0 && (wasInside || nowMouth)) {
      const p = V(b.p.x, b.p.y, b.p.z), v = V(b.v.x, b.v.y, b.v.z);
      const back = GD - R;
      if (depthIn > back) { p.x = lineX + side * back; if (v.x * side > 0) v.x = -v.x * NET_DAMP * 0.4; v.z *= 0.5; v.y *= 0.5; }
      const damp = Math.max(0, 1 - 2.5 / 120);
      v.x *= damp; v.z *= damp;
      if (Math.abs(p.z) > HGW - R) { p.z = Math.sign(p.z) * (HGW - R); if (v.z * p.z > 0) v.z = -v.z * NET_DAMP; v.x *= 0.6; }
      if (p.y > GH - R) { p.y = GH - R; if (v.y > 0) v.y = -v.y * NET_DAMP; v.x *= 0.6; }
      b.p = p; b.v = v;
    } else if (depthIn > 0 && depthIn < GD) {
      const p = V(b.p.x, b.p.y, b.p.z), v = V(b.v.x, b.v.y, b.v.z);
      if (Math.abs(prev.z) >= HGW && Math.abs(p.z) < HGW + R && p.y < GH) { p.z = Math.sign(prev.z) * (HGW + R); v.z = -v.z * NET_DAMP; v.x *= 0.5; }
      if (prev.y >= GH && p.y < GH + R && Math.abs(p.z) < HGW) { p.y = GH + R; v.y = Math.abs(v.y) * 0.2; v.x *= 0.6; v.z *= 0.6; }
      b.p = p; b.v = v;
    }
    return hit;
  }

  function collideSegment(b, a, c, r) {
    const ac = sub(c, a);
    const t = clamp01(dot(sub(b.p, a), ac) / dot(ac, ac));
    const closest = add(a, mul(ac, t));
    const d = sub(b.p, closest);
    const dist = len(d);
    if (dist >= r || dist < 1e-5) return false;
    const n = mul(d, 1 / dist);
    b.p = add(closest, mul(n, r));
    const vn = dot(b.v, n);
    if (vn < 0) {
      const vt = sub(b.v, mul(n, vn));
      b.v = sub(mul(vt, 0.85), mul(n, vn * POST_REST));
      b.s = mul(b.s, 0.5);
      return true;
    }
    return false;
  }

  // ---------------------------------------------------------------- kick solver (port of KickSolver.cs)
  function rollingDistance(v0, vEnd) {
    const a = ROLL_DEC, c = ROLL_DAMP;
    if (vEnd >= v0) return 0;
    const t = Math.log((v0 + a / c) / (vEnd + a / c)) / c;
    return (v0 + a / c) * (1 - Math.exp(-c * t)) / c - (a / c) * t;
  }
  function groundPassSpeed(distance, arrival) {
    let lo = arrival, hi = 40;
    if (rollingDistance(hi, arrival) < distance) return hi;
    for (let i = 0; i < 30; i++) {
      const mid = (lo + hi) * 0.5;
      if (rollingDistance(mid, arrival) < distance) lo = mid; else hi = mid;
    }
    return (lo + hi) * 0.5;
  }
  function groundPass(from, to, arrival) {
    const d = flat(sub(to, from));
    return mul(fnorm(d), groundPassSpeed(flen(d), arrival));
  }
  function flightStep(b, dt) {
    const v = b.v, s = len(v);
    const acc = add(add(V(0, -G, 0), mul(v, -DRAG * s)), mul(cross(b.s, v), MAGNUS));
    b.v = add(v, mul(acc, dt));
    b.p = add(b.p, mul(b.v, dt));
  }
  function simulateUntilHeight(from, v, h, maxT) {
    const b = { p: from, v, s: V() };
    const dt = 1 / 120;
    let t = 0, last = from, rising = true;
    while (t < maxT) {
      last = b.p;
      flightStep(b, dt);
      t += dt;
      if (b.v.y < 0) rising = false;
      if (!rising && b.p.y <= h) return { p: lerpV(last, b.p, invLerp(last.y, b.p.y, h)), t };
    }
    return { p: b.p, t };
  }
  function lofted(from, to, flightTime, targetHeight = R) {
    const T = Math.max(0.3, flightTime);
    const d = sub(to, from);
    let v = V(d.x / T, (targetHeight - from.y + 0.5 * G * T * T) / T, d.z / T);
    for (let i = 0; i < 10; i++) {
      const r = simulateUntilHeight(from, v, targetHeight, T * 2.5);
      const err = flat(sub(to, r.p));
      const tErr = T - r.t;
      if (flen(err) < 0.15 && Math.abs(tErr) < 0.05) break;
      const tc = Math.max(0.3, r.t);
      v = add(v, mul(err, 1 / tc));
      v.y += tErr * G * 0.5;
    }
    const l = len(v);
    return l > MAXS ? mul(v, MAXS / l) : v;
  }
  function heightAtDistance(from, v, dist) {
    const b = { p: from, v, s: V() };
    const start = flat(from);
    let last = from;
    for (let i = 0; i < 600; i++) {
      last = b.p;
      flightStep(b, 1 / 120);
      const d = flen(sub(flat(b.p), start));
      if (d >= dist) return lerp(last.y, b.p.y, invLerp(flen(sub(flat(last), start)), d, dist));
      if (flen(b.v) < 0.5) return b.p.y;
    }
    return b.p.y;
  }
  function driven(from, to, speed, targetHeight) {
    const f = flat(sub(to, from));
    const dist = flen(f), dir = fnorm(f);
    if (dist < 0.5) return mul(dir, speed);
    let lo = -0.25, hi = 0.9;
    for (let i = 0; i < 20; i++) {
      const mid = (lo + hi) * 0.5;
      const v = add(mul(dir, speed * Math.cos(mid)), V(0, speed * Math.sin(mid), 0));
      if (heightAtDistance(from, v, dist) < targetHeight) lo = mid; else hi = mid;
    }
    const ang = (lo + hi) * 0.5;
    return add(mul(dir, speed * Math.cos(ang)), V(0, speed * Math.sin(ang), 0));
  }

  // ---------------------------------------------------------------- teams and players
  const FORMATIONS = {
    '4-3-3': [['GK', -0.4, 0], ['LB', 0.02, -0.78], ['CB', 0, -0.28], ['CB', 0, 0.28], ['RB', 0.02, 0.78], ['CDM', 0.3, 0], ['CM', 0.5, -0.38], ['CM', 0.5, 0.38], ['LW', 0.85, -0.75], ['ST', 1, 0], ['RW', 0.85, 0.75]],
    '4-4-2': [['GK', -0.4, 0], ['LB', 0.02, -0.78], ['CB', 0, -0.28], ['CB', 0, 0.28], ['RB', 0.02, 0.78], ['LM', 0.48, -0.75], ['CM', 0.42, -0.24], ['CM', 0.42, 0.24], ['RM', 0.48, 0.75], ['ST', 0.95, -0.2], ['ST', 0.95, 0.2]],
    '4-2-3-1': [['GK', -0.4, 0], ['LB', 0.02, -0.78], ['CB', 0, -0.28], ['CB', 0, 0.28], ['RB', 0.02, 0.78], ['CDM', 0.3, -0.22], ['CDM', 0.3, 0.22], ['LM', 0.72, -0.72], ['CAM', 0.68, 0], ['RM', 0.72, 0.72], ['ST', 1, 0]]
  };
  const NUMBERS = { GK: 1, LB: 6, CB: 4, RB: 2, CDM: 5, CM: 8, CAM: 10, LM: 11, RM: 7, LW: 11, RW: 7, ST: 9 };
  const FIRST = ['Caio', 'Davi', 'Enzo', 'Igor', 'Luan', 'Rafael', 'Thiago', 'Vitor', 'Bruno', 'Diego', 'Gabriel', 'Heitor', 'Matheus', 'Pedro', 'Renan', 'Samuel', 'Wesley', 'Yuri', 'André', 'Felipe', 'João', 'Lucas', 'Murilo', 'Otávio'];
  const LAST = ['Alves', 'Barros', 'Campos', 'Dias', 'Esteves', 'Farias', 'Gomes', 'Lima', 'Moura', 'Nunes', 'Pires', 'Rocha', 'Santana', 'Teixeira', 'Vieira', 'Xavier', 'Cardoso', 'Freitas', 'Macedo', 'Prado', 'Queiroz', 'Ribeiro', 'Siqueira', 'Torres'];

  // Fictional clubs from Core/World/DemoClubs.cs.
  const CLUBS = [
    { id: 'APA', name: 'Atlético Porto Alto', formation: '4-3-3', strength: 76, shirt: '#C8102E', shorts: '#FFFFFF', socks: '#C8102E', number: '#FFFFFF', keeper: '#16A34A' },
    { id: 'UNS', name: 'União Serrana', formation: '4-2-3-1', strength: 74, shirt: '#1E3A8A', shorts: '#1E3A8A', socks: '#FFFFFF', number: '#FACC15', keeper: '#F97316' },
    { id: 'ECL', name: 'Esporte Clube Litoral', formation: '4-4-2', strength: 72, shirt: '#FACC15', shorts: '#14532D', socks: '#FACC15', number: '#14532D', keeper: '#7C3AED' },
    { id: 'RCE', name: 'Real Cerrado', formation: '4-3-3', strength: 71, shirt: '#16A34A', shorts: '#FFFFFF', socks: '#16A34A', number: '#FFFFFF', keeper: '#111827' },
    { id: 'SPC', name: 'Sport Capital', formation: '4-4-2', strength: 73, shirt: '#7C3AED', shorts: '#F5F5F5', socks: '#7C3AED', number: '#F5F5F5', keeper: '#FACC15' },
    { id: 'KAT', name: 'Kingsport Athletic', formation: '4-3-3', strength: 82, shirt: '#1D4ED8', shorts: '#FFFFFF', socks: '#1D4ED8', number: '#FFFFFF', keeper: '#F59E0B' },
    { id: 'FCR', name: 'FC Rheinau 1898', formation: '4-2-3-1', strength: 80, shirt: '#DC2626', shorts: '#FFFFFF', socks: '#DC2626', number: '#FFFFFF', keeper: '#111827' },
    { id: 'OMI', name: 'Olympique Mistral', formation: '4-3-3', strength: 78, shirt: '#38BDF8', shorts: '#F5F5F5', socks: '#38BDF8', number: '#0F172A', keeper: '#DC2626' }
  ];
  const AWAY_KIT = c => ({ shirt: '#F5F5F5', shorts: c.shirt, socks: '#F5F5F5', number: c.shirt, keeper: c.keeper });

  function hexLum(hex) {
    const n = parseInt(hex.slice(1), 16);
    return (0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255)) / 255;
  }
  function hexDist(a, b) {
    const x = parseInt(a.slice(1), 16), y = parseInt(b.slice(1), 16);
    return Math.abs((x >> 16) - (y >> 16)) + Math.abs(((x >> 8) & 255) - ((y >> 8) & 255)) + Math.abs((x & 255) - (y & 255));
  }

  function roleOf(pos) {
    if (pos === 'GK') return 'GK';
    if (pos === 'CB') return 'CB';
    if (pos === 'LB' || pos === 'RB') return 'FB';
    if (pos === 'CDM' || pos === 'CM') return 'CM';
    if (pos === 'CAM') return 'AM';
    if (pos === 'LM' || pos === 'RM' || pos === 'LW' || pos === 'RW') return 'W';
    return 'ST';
  }

  function makeAttrs(rng, role, strength) {
    const r = () => rng.range(-6, 6);
    const b = strength;
    const base = { pace: b, accel: b, passing: b - 4, shooting: b - 10, dribbling: b - 4, control: b - 2, defending: b - 10, heading: b - 6, gk: 30, reactions: b - 2 };
    const bias = {
      GK: { pace: -18, accel: -12, shooting: -30, dribbling: -25, defending: -20, gk: b + 2 - 30 },
      CB: { pace: -6, defending: 18, heading: 12, shooting: -12, dribbling: -10, passing: -2 },
      FB: { pace: 6, accel: 4, defending: 10, dribbling: 0, shooting: -8 },
      CM: { passing: 10, control: 6, defending: 4, shooting: 2 },
      AM: { passing: 10, dribbling: 10, shooting: 8, defending: -10 },
      W: { pace: 10, accel: 8, dribbling: 12, shooting: 6, defending: -14 },
      ST: { pace: 4, shooting: 18, heading: 8, dribbling: 4, defending: -18 }
    }[role];
    const a = {};
    for (const k of Object.keys(base)) a[k] = Math.round(clamp(base[k] + (bias[k] || 0) + r(), 20, 97));
    return a;
  }

  class Player {
    constructor(id, team, slot, attrs, name, number) {
      this.id = id; this.team = team;
      this.pos = slot[0]; this.role = roleOf(slot[0]); this.depth = slot[1]; this.lat = slot[2];
      this.a = attrs; this.name = name; this.number = number;
      this.isGK = this.role === 'GK';
      this.p = V(); this.vel = V(); this.facing = 0; this.jump = 0;
      this.target = V(); this.speedF = 0.6;
      this.action = null; this.stun = 0; this.touchCd = 0;
      this.decT = 0; this.holdT = 0; this.runT = 0; this.runTarget = V();
      this.callT = 0; this.queued = null; this.queuedT = 0;
      this.dist = 0; this.goals = 0; this.anim = 0;
      this.maxSpeed = attr(attrs.pace, 6.6, 9.5) * (this.isGK ? 0.9 : 1);
      this.accel = attr(attrs.accel, 4.2, 7.6);
    }
    get speed() { return flen(this.vel); }
    get busy() { return this.action !== null; }
    get canAct() { return this.stun <= 0 && this.action === null; }
    footPoint() { return add(this.p, mul(dirOf(this.facing), 0.45)); }
  }

  function buildTeam(rng, club, kit, index, strengthOffset) {
    const slots = FORMATIONS[club.formation] || FORMATIONS['4-3-3'];
    const used = {};
    const players = slots.map((s, i) => {
      let n = NUMBERS[s[0]];
      while (used[n]) n = n === 1 ? 12 : n + 10 > 99 ? n + 1 : (n < 12 ? n + 12 : n + 1);
      used[n] = true;
      const name = rng.pick(FIRST) + ' ' + rng.pick(LAST);
      return new Player(index * 11 + i, index, s, makeAttrs(rng, roleOf(s[0]), club.strength + strengthOffset), name, n);
    });
    return {
      index, club, kit, players,
      stats: { goals: 0, shots: 0, onTarget: 0, passes: 0, completed: 0, poss: 0, saves: 0, corners: 0, tackles: 0, xg: 0 }
    };
  }

  // ---------------------------------------------------------------- match
  class Match {
    /**
     * opts: { home, away (club ids), halfMinutes, difficulty 0..2, userTeam (0, 1 or -1 for AI only), seed }
     */
    constructor(opts = {}) {
      this.rng = makeRng(opts.seed || 12345);
      this.halfMinutes = opts.halfMinutes || 3;
      this.difficulty = opts.difficulty == null ? 1 : opts.difficulty;
      this.userTeam = opts.userTeam == null ? 0 : opts.userTeam;
      const home = CLUBS.find(c => c.id === opts.home) || CLUBS[0];
      let away = CLUBS.find(c => c.id === opts.away && c.id !== home.id) || CLUBS.find(c => c.id !== home.id);
      const homeKit = { shirt: home.shirt, shorts: home.shorts, socks: home.socks, number: home.number, keeper: home.keeper };
      let awayKit = { shirt: away.shirt, shorts: away.shorts, socks: away.socks, number: away.number, keeper: away.keeper };
      if (hexDist(homeKit.shirt, awayKit.shirt) < 200) awayKit = AWAY_KIT(away);
      const diffOff = [-7, 0, 5][this.difficulty];
      const off = t => (this.userTeam === -1 || t === this.userTeam) ? 0 : diffOff;
      this.teams = [buildTeam(this.rng, home, homeKit, 0, off(0)), buildTeam(this.rng, away, awayKit, 1, off(1))];
      this.players = this.teams[0].players.concat(this.teams[1].players);
      this.ball = newBall();
      this.owner = null; this.holder = null; this.lastTouch = null; this.pass = null; this.lastShot = null;
      this.restart = null; this.phase = 'restart'; this.half = 1;
      this.halfClock = 0; this.time = 0; this.goalTimer = 0;
      this.stoppage = [this.rng.range(1, 3), this.rng.range(2, 4)];
      this.trajVersion = 0; this.lastKickTime = -10;
      this.chaser = [null, null]; this.chasePoint = [V(), V()]; this.chaseTime = [99, 99];
      this.samples = []; this.predFrame = -99; this.frame = 0;
      this.feed = []; this.sfx = [];
      this.input = { mx: 0, mz: 0, sprint: false, presses: [], shootCharge: 0 };
      this.user = null; this.pressHelpT = 0; this.shotSeen = [-10, -10]; this.diveAt = [-10, -10];
      this.userAutopilot = false;
      this.setupKickoff(0, true);
    }

    // ------------------------------------------------ helpers
    attackDir(team) { return (team === 0 ? 1 : -1) * (this.half === 1 ? 1 : -1); }
    ax(team, p) { return p.x * this.attackDir(team); }
    goalOf(team) { return V(this.attackDir(team) * HL, 0, 0); }        // goal the team attacks
    ownGoal(team) { return V(-this.attackDir(team) * HL, 0, 0); }
    inBox(p, side) { return Math.abs(p.z) <= PA_HALF_W && p.x * side >= HL - PA_DEPTH && p.x * side <= HL + 0.5; }
    mates(p) { return this.teams[p.team].players.filter(q => q !== p); }
    opps(team) { return this.teams[1 - team].players; }
    keeper(team) { return this.teams[team].players[0]; }
    minute() {
      const real = this.halfMinutes * 60;
      return (this.half - 1) * 45 + this.halfClock / real * 45;
    }
    clockText() {
      const m = this.minute();
      const base = this.half === 1 ? 45 : 90;
      if (m >= base) return base + '+' + Math.min(9, Math.floor(m - base) + 1);
      return String(Math.floor(m));
    }
    isUser(p) { return this.user === p && !this.userAutopilot; }
    possessionTeam() {
      if (this.owner) return this.owner.team;
      if (this.holder) return this.holder.team;
      if (this.restart) return this.restart.team;
      if (this.pass && this.time - this.pass.time < 3) return this.pass.team;
      return this.lastTouch ? this.lastTouch.team : null;
    }
    nearestOpp(p, pos = p.p) {
      let best = null, bd = 1e9;
      for (const o of this.opps(p.team)) { const d = fdist(o.p, pos); if (d < bd) { bd = d; best = o; } }
      return { p: best, d: bd };
    }
    log(kind, text, team) { this.feed.push({ kind, text, team, min: this.clockText(), t: this.time }); }
    sound(name, v = 1) { this.sfx.push({ name, v }); }

    xg(p, from, header) {
      const side = this.attackDir(p.team);
      const goal = this.goalOf(p.team);
      const dist = fdist(from, goal);
      if (from.x * side > HL) return 0.01;
      const p1 = sub(V(goal.x, 0, -HGW), flat(from)), p2 = sub(V(goal.x, 0, HGW), flat(from));
      const angle = Math.abs(wrap(angOf(p2) - angOf(p1)));
      let x = 0.95 * Math.exp(-dist / 8.5) * (0.35 + 0.65 * clamp01(angle / 0.9));
      if (header) x *= 0.55;
      const toGoal = fnorm(sub(goal, from));
      let block = 1;
      for (const o of this.opps(p.team)) {
        if (o.isGK) continue;
        const rel = flat(sub(o.p, from));
        const along = dot(rel, toGoal);
        if (along < 0.3 || along > dist) continue;
        const perp = flen(sub(rel, mul(toGoal, along)));
        if (perp < HGW * along / Math.max(1, dist) + 0.4) block *= along < 3 ? 0.55 : 0.75;
      }
      let kf = 1;
      const gk = this.keeper(1 - p.team);
      const rel = flat(sub(gk.p, from));
      const along = dot(rel, toGoal);
      const perp = flen(sub(rel, mul(toGoal, Math.max(0, along))));
      kf = along > 0 ? lerp(0.8, 1.6, clamp01(perp / 4)) : 1.8;
      const skill = header ? p.a.heading : (dist > 20 ? p.a.shooting - 6 : p.a.shooting);
      return clamp(x * block * kf * (0.55 + 0.9 * skill / 99), 0.005, 0.95);
    }

    // ------------------------------------------------ restarts
    formationSpot(p, kickoff) {
      const dir = this.attackDir(p.team);
      if (p.isGK) return V(-dir * (HL - 1.5), 0, 0);
      const a = kickoff ? lerp(-44, -6, clamp01(p.depth)) : 0;
      return V(a * dir, 0, p.lat * 27);
    }

    setupKickoff(team, first) {
      this.phase = 'restart';
      this.owner = null; this.holder = null; this.pass = null; this.lastShot = null;
      this.ball = newBall();
      for (const p of this.players) {
        p.p = this.formationSpot(p, true); p.vel = V(); p.action = null; p.stun = 0; p.jump = 0; p.runT = 0;
        p.facing = this.attackDir(p.team) > 0 ? 0 : Math.PI;
        p.target = V(p.p.x, 0, p.p.z);
      }
      const t = this.teams[team].players;
      const dir = this.attackDir(team);
      const taker = t.slice().sort((a, b) => b.depth - a.depth)[0];
      taker.p = V(-dir * 0.6, 0, 0.3); taker.target = taker.p; taker.facing = dir > 0 ? 0 : Math.PI;
      const second = t.filter(q => q !== taker && !q.isGK).sort((a, b) => b.depth - a.depth)[0];
      second.p = V(-dir * 1.0, 0, 6); second.target = second.p;
      this.restart = { type: 'kickoff', team, spot: V(0, R, 0), taker, t: 0 };
      this.lastTouch = null;
      if (!first) this.sound('whistle', 0.7);
      this.switchUserTo(this.userTeam === team ? taker : null);
    }

    beginRestart(type, team, spot) {
      this.phase = 'restart';
      this.owner = null; this.holder = null; this.pass = null; this.lastShot = null;
      this.ball.v = V(); this.ball.s = V(); this.ball.held = false;
      this.ball.p = V(spot.x, R, spot.z);
      const players = this.teams[team].players.filter(q => !q.isGK || type === 'goalkick');
      let taker;
      if (type === 'goalkick') taker = this.keeper(team);
      else if (type === 'corner') taker = players.filter(q => q.role !== 'CB' && q.role !== 'ST').sort((a, b) => b.a.passing - a.a.passing)[0];
      else taker = players.sort((a, b) => fdist(a.p, spot) - fdist(b.p, spot))[0];
      const inward = fnorm(sub(V(0, 0, 0), spot));
      taker.p = type === 'throw' ? V(spot.x, 0, spot.z + Math.sign(spot.z) * 0.35) : sub(flat(spot), mul(inward, 0.6));
      taker.vel = V(); taker.action = null; taker.stun = 0;
      taker.facing = angOf(inward);
      taker.target = taker.p;
      if (type === 'throw') { this.holder = taker; this.ball.held = true; this.ball.p = V(spot.x, 2.0, spot.z); }
      this.restart = { type, team, spot, taker, t: 0, targets: new Map() };
      if (type === 'corner') {
        this.teams[team].stats.corners++;
        this.prepareCornerTargets(team);
      }
      this.sound('whistle', 0.5);
      this.log(type, { throw: 'Lateral', corner: 'Escanteio', goalkick: 'Tiro de meta' }[type], team);
      if (this.userTeam === team) this.switchUserTo(taker);
      else if (this.user && this.userTeam >= 0) this.switchUserTo(this.closestTo(this.userTeam, this.ball.p, true));
    }

    prepareCornerTargets(team) {
      const side = this.attackDir(team);
      const r = this.restart;
      const att = this.teams[team].players.filter(q => !q.isGK && q !== r.taker).sort((a, b) => b.depth - a.depth);
      att.forEach((q, i) => {
        if (i < 6) r.targets.set(q, V(side * (HL - this.rng.range(5, 13)), 0, this.rng.range(-9, 9)));
        else r.targets.set(q, V(side * (HL - 30 - i * 3), 0, this.rng.range(-15, 15)));
      });
      const def = this.teams[1 - team].players.filter(q => !q.isGK).sort((a, b) => a.depth - b.depth);
      def.forEach((q, i) => {
        if (i < 9) r.targets.set(q, V(side * (HL - this.rng.range(2, 11)), 0, this.rng.range(-8, 8)));
        else r.targets.set(q, V(side * (HL - 25), 0, this.rng.range(-10, 10)));
      });
    }

    takeRestart(kind, dir) {
      const r = this.restart;
      if (!r || r.taken) return;
      const p = r.taker;
      const side = this.attackDir(r.team);
      let kick;
      if (r.type === 'corner') {
        let target;
        const inBox = this.teams[r.team].players.filter(q => q !== p && this.inBox(q.p, side));
        if (dir && kind !== 'auto') {
          target = this.pickReceiver(p, dir, 40)?.p || V(side * (HL - 11), 0, 0);
        } else target = inBox.length ? this.rng.pick(inBox).p : V(side * (HL - 9), 0, this.rng.range(-4, 4));
        kick = { kind: 'cross', target: V(target.x, 0, target.z), power: 1, recv: null };
      } else if (r.type === 'throw') {
        const recv = dir && kind !== 'auto' ? this.pickReceiver(p, dir, 25) : this.bestShortOption(p, 25);
        const target = recv ? add(recv.p, mul(recv.vel, 0.6)) : add(p.p, mul(dir || fnorm(V(side, 0, -Math.sign(p.p.z))), 10));
        kick = { kind: 'throw', target, power: 1, recv };
      } else if (r.type === 'goalkick') {
        const recv = dir && kind !== 'auto' ? this.pickReceiver(p, dir, 60) : null;
        const short = recv ? null : this.bestShortOption(p, 30, true);
        if (recv && fdist(recv.p, p.p) < 30 && kind === 'pass') kick = { kind: 'pass', target: recv.p, power: 1, recv };
        else if (short && this.rng.chance(0.5)) kick = { kind: 'pass', target: short.p, power: 1, recv: short };
        else {
          const fwd = recv || this.teams[r.team].players.filter(q => q.depth > 0.6).sort(() => this.rng.next() - 0.5)[0];
          kick = { kind: 'gkkick', target: add(fwd.p, V(side * 4, 0, 0)), power: 1, recv: fwd };
        }
      } else {
        const recv = dir && kind !== 'auto' ? this.pickReceiver(p, dir, 40) : this.teams[r.team].players.filter(q => q !== p && !q.isGK && q.depth < 0.6).sort((a, b) => fdist(a.p, p.p) - fdist(b.p, p.p))[0];
        const r2 = recv || this.teams[r.team].players.filter(q => q !== p && !q.isGK).sort((a, b) => fdist(a.p, p.p) - fdist(b.p, p.p))[0];
        kick = { kind: kind === 'lob' ? 'lofted' : 'pass', target: r2.p, power: 1, recv: r2 };
      }
      r.taken = true;
      this.startKick(p, kick);
    }

    bestShortOption(p, maxD, defendersOnly) {
      let best = null, bu = -1e9;
      for (const q of this.mates(p)) {
        if (q.isGK) continue;
        if (defendersOnly && q.depth > 0.35) continue;
        const d = fdist(q.p, p.p);
        if (d < 5 || d > maxD) continue;
        const space = Math.min(8, this.nearestOpp(q).d);
        const u = space - this.passRisk(p.p, q.p, p.team, 12) * 8 - d * 0.05;
        if (u > bu) { bu = u; best = q; }
      }
      return bu > 2 ? best : null;
    }

    // ------------------------------------------------ user
    closestTo(team, pos, excludeGK) {
      let best = null, bd = 1e9;
      for (const q of this.teams[team].players) {
        if (excludeGK && q.isGK) continue;
        const d = fdist(q.p, pos);
        if (d < bd) { bd = d; best = q; }
      }
      return best;
    }
    switchUserTo(p) {
      if (this.userTeam < 0) { this.user = null; return; }
      if (!p) p = this.closestTo(this.userTeam, this.ball.p, true);
      if (p.isGK) return;
      if (p !== this.user) { this.user = p; this.userSwitchT = this.time; }
    }
    pickReceiver(p, dir, maxD = 45) {
      let best = null, bs = -1e9;
      for (const q of this.mates(p)) {
        const rel = flat(sub(q.p, p.p));
        const d = flen(rel);
        if (d < 3 || d > maxD) continue;
        const c = dot(fnorm(rel), dir);
        if (c < 0.45) continue;
        const s = c * c * c * 2 - d * 0.02 + (q.isGK ? -1 : 0);
        if (s > bs) { bs = s; best = q; }
      }
      return best;
    }

    userDir(p) {
      const m = Math.hypot(this.input.mx, this.input.mz);
      return m > 0.2 ? V(this.input.mx / m, 0, this.input.mz / m) : dirOf(p.facing);
    }

    applyUser(p, dt) {
      const inp = this.input;
      const m = Math.hypot(inp.mx, inp.mz);
      if (m > 0.15) {
        p.target = add(p.p, V(inp.mx / m * 8, 0, inp.mz / m * 8));
        p.speedF = (inp.sprint ? 1 : 0.74) * Math.min(1, m * 1.3);
      } else { p.target = p.p; p.speedF = 0.5; }
      const presses = inp.presses; inp.presses = [];
      const r = this.restart;
      if (this.phase === 'restart') {
        if (r && r.taker === p) {
          if (r.t > 0.5 && !r.taken) {
            for (const e of presses) if (e.kind !== 'switch') { this.takeRestart(e.kind === 'shoot' ? 'lob' : e.kind, this.userDir(p)); break; }
          }
          p.target = p.p;
        }
        return;
      }
      const dir = this.userDir(p);
      const hasBall = this.owner === p;
      const teamHas = this.possessionTeam() === p.team && (this.owner || this.holder || this.pass);
      for (const e of presses) {
        if (hasBall && p.canAct) {
          if (e.kind === 'pass' || e.kind === 'through') this.userPass(p, dir, e.kind === 'through');
          else if (e.kind === 'lob') this.userLob(p, dir);
          else if (e.kind === 'shoot') this.userShot(p, e.power, dir);
        } else if (!hasBall && this.pass && this.pass.recv === p && this.pass.team === p.team && (e.kind === 'pass' || e.kind === 'shoot' || e.kind === 'through')) {
          p.queued = { kind: e.kind, power: e.power || 0.7, dir }; p.queuedT = 1.2;    // first-time action
        } else if (teamHas && !hasBall) {
          if (e.kind === 'pass') p.callT = 1.5;
          if (e.kind === 'through') { p.callT = 1.5; p.callThrough = true; }
          if (e.kind === 'switch') this.switchUserTo(this.nextSwitch(p));
        } else if (!teamHas) {
          if (e.kind === 'pass' || e.kind === 'switch') this.switchUserTo(this.nextSwitch(p));
          else if (e.kind === 'shoot' && p.canAct) this.startSlide(p, m > 0.2 ? dir : fnorm(sub(this.ball.p, p.p)));
          else if (e.kind === 'through' && p.canAct) this.startTackle(p);
          else if (e.kind === 'lob') this.pressHelpT = 2.5;
        }
      }
    }

    nextSwitch(cur) {
      const ball = this.ball.p;
      const cands = this.teams[this.userTeam].players.filter(q => !q.isGK && q !== cur);
      const ownGoal = this.ownGoal(this.userTeam);
      cands.sort((a, b) => (fdist(a.p, ball) + 0.15 * fdist(a.p, ownGoal)) - (fdist(b.p, ball) + 0.15 * fdist(b.p, ownGoal)));
      return cands[0];
    }

    userPass(p, dir, through) {
      const recv = this.pickReceiver(p, dir, through ? 50 : 40);
      if (!recv) {
        const target = add(p.p, mul(dir, through ? 18 : 12));
        this.startKick(p, { kind: through ? 'through' : 'pass', target, power: 1, recv: null, user: true });
        return;
      }
      let target = add(recv.p, mul(recv.vel, 0.5));
      if (through) {
        const side = this.attackDir(p.team);
        const lead = fnorm(add(V(side, 0, 0), mul(fnorm(recv.vel), 0.8)));
        target = add(recv.p, mul(lead, clamp(fdist(recv.p, p.p) * 0.3, 5, 11)));
        target = V(clamp(target.x, -HL + 2, HL - 2), 0, clamp(target.z, -HW + 2, HW - 2));
        recv.runT = 1.5; recv.runTarget = target;
      }
      this.startKick(p, { kind: through ? 'through' : 'pass', target, power: 1, recv, user: true });
      if (this.userTeam >= 0) this.pendingSwitch = recv;
    }

    userLob(p, dir) {
      const side = this.attackDir(p.team);
      const wideFinal = this.ax(p.team, p.p) > 25 && Math.abs(p.p.z) > 12;
      let recv = this.pickReceiver(p, dir, 55);
      let target;
      if (wideFinal) {
        const inBox = this.mates(p).filter(q => this.inBox(q.p, side) && !q.isGK);
        recv = inBox.sort((a, b) => Math.abs(a.p.z) - Math.abs(b.p.z))[0] || null;
        target = recv ? add(recv.p, mul(recv.vel, 0.9)) : V(side * (HL - 10), 0, 0);
        this.startKick(p, { kind: 'cross', target, power: 1, recv, user: true });
      } else {
        target = recv ? add(recv.p, mul(recv.vel, 1.0)) : add(p.p, mul(dir, 30));
        this.startKick(p, { kind: 'lofted', target, power: 1, recv, user: true });
      }
      if (recv) this.pendingSwitch = recv;
    }

    userShot(p, power, dir) {
      const side = this.attackDir(p.team);
      const gk = this.keeper(1 - p.team);
      let z;
      const mz = this.input.mz;
      if (Math.abs(mz) > 0.35) z = Math.sign(mz) * (HGW - 0.55);
      else z = (gk.p.z > p.p.z * 0.15 ? -1 : 1) * (HGW - 0.6);
      const h = power > 0.85 ? 1.9 : power > 0.55 ? 1.1 : 0.35;
      this.startKick(p, { kind: 'shot', target: V(side * HL, h, z), power: clamp(power, 0.25, 1), user: true });
    }

    // ------------------------------------------------ actions
    startKick(p, kick) {
      const durs = { pass: [0.32, 0.12], through: [0.34, 0.13], lofted: [0.45, 0.19], cross: [0.48, 0.2], shot: [0.5, 0.2], clear: [0.42, 0.17], throw: [0.75, 0.42], gkthrow: [0.6, 0.3], gkkick: [0.75, 0.38] };
      const d = durs[kick.kind] || [0.4, 0.15];
      p.action = { type: kick.kind === 'throw' || kick.kind === 'gkthrow' ? 'throw' : 'kick', kick, t: 0, dur: d[0], contact: d[1], done: false };
      if (kick.target) p.facing = turnTo(p.facing, angOf(sub(kick.target, p.p)), 1.2);
    }
    startTackle(p) {
      p.action = { type: 'tackle', t: 0, dur: 0.42, contact: 0.16, done: false, dir: fnorm(sub(this.ball.p, p.p)) };
      p.facing = angOf(p.action.dir);
    }
    startSlide(p, dir) {
      p.action = { type: 'slide', t: 0, dur: 0.9, contact: 0.08, done: false, dir, speed: Math.max(6.5, p.speed + 1.5) };
      p.facing = angOf(dir);
    }
    startDive(p, point, speed, jump) {
      const rel = flat(sub(point, p.p));
      p.action = { type: 'dive', t: 0, dur: 1.05, dir: fnorm(rel), speed, jump, dist: flen(rel), side: Math.sign(wrap(angOf(rel) - p.facing)) || 1 };
    }

    applyKick(p, k) {
      const b = this.ball;
      const from = k.kind === 'header' ? V(b.p.x, b.p.y, b.p.z) : this.holder === p ? V(b.p.x, k.kind === 'throw' ? 2.0 : 1.2, b.p.z) : V(b.p.x, R, b.p.z);
      const dist = k.target ? fdist(from, k.target) : 0;
      const user = !!k.user;
      const passing = p.a.passing;
      const errK = (user ? 0.7 : 1) * (this.userTeam >= 0 && p.team !== this.userTeam ? [1.3, 1, 0.85][this.difficulty] : 1);
      let vel;
      let target = k.target ? V(k.target.x, k.target.y || 0, k.target.z) : from;
      switch (k.kind) {
        case 'pass': case 'through': {
          const sd = (0.025 + 0.07 * (1 - passing / 99)) * errK;
          target = add(from, rotY(sub(flat(target), flat(from)), this.rng.gauss() * sd));
          vel = groundPass(from, target, k.kind === 'through' ? 7 : clamp(5 + dist * 0.12, 6, 9.5));
          if (flen(vel) < 8) vel = mul(fnorm(vel), 8);
          break;
        }
        case 'lofted': case 'cross': case 'gkkick': case 'clear': case 'throw': case 'gkthrow': {
          const scatter = k.kind === 'clear' ? 6 : (dist * (0.03 + 0.09 * (1 - passing / 99)) * errK);
          target = add(target, V(this.rng.gauss() * scatter, 0, this.rng.gauss() * scatter));
          const T = k.kind === 'cross' ? 1.0 + dist / 40 : k.kind === 'throw' ? 0.55 + dist / 30 : k.kind === 'gkthrow' ? 0.5 + dist / 30 : k.kind === 'gkkick' || k.kind === 'clear' ? 1.6 + dist / 45 : 0.9 + dist / 30;
          const h = k.kind === 'cross' ? 1.4 : R;
          vel = lofted(from, V(target.x, h, target.z), T, h);
          break;
        }
        case 'shot': {
          const sh = p.a.shooting;
          const power = k.power;
          const speed = lerp(17, 32, power) * (0.86 + 0.14 * sh / 99);
          const sd = (0.05 + 0.11 * (1 - sh / 99)) * (0.6 + power * 0.7) * errK * (dist > 20 ? 1.3 : 1);
          const dir = rotY(sub(flat(target), flat(from)), this.rng.gauss() * sd);
          target = add(from, dir);
          const th = clamp((k.target.y || 0.4) + this.rng.gauss() * (0.25 + power * power * 0.6) * errK, 0.12, 3.2);
          vel = driven(from, target, speed, th);
          b.s = V(0, this.rng.gauss() * 4, 0);
          break;
        }
        case 'header': {
          vel = k.vel;
          break;
        }
      }
      if (k.kind !== 'shot') b.s = V();
      if (k.kind === 'cross' && k.target) b.s = V(0, -Math.sign(k.target.z - from.z) * 5, 0);
      b.p = from; b.v = vel; b.held = false;
      this.owner = null; this.holder = null;
      p.touchCd = 0.3; p.holdT = 0;
      this.lastTouch = p; this.trajVersion++; this.lastKickTime = this.time;
      const ts = this.teams[p.team].stats;
      if (this.restart) { this.restart = null; this.phase = 'play'; }
      if (k.kind === 'shot' || k.isShot) {
        const xg = this.xg(p, from, k.kind === 'header');
        ts.shots++; ts.xg += xg;
        this.lastShot = { p, team: p.team, time: this.time, xg };
        this.pass = null;
        if (this.shotOnTarget(p.team)) ts.onTarget++;
        this.log('shot', (k.kind === 'header' ? 'Cabeçada de ' : 'Finalização de ') + p.name, p.team);
      } else {
        this.lastShot = null;
        if (k.kind !== 'header' && k.kind !== 'clear') ts.passes++;
        this.pass = { passer: p, recv: k.recv || null, team: p.team, time: this.time, kind: k.kind };
      }
      this.sound(k.kind === 'shot' ? 'shot' : (k.kind === 'throw' || k.kind === 'gkthrow') ? 'throw' : 'kick', clamp(len(vel) / 25, 0.3, 1));
      if (this.pendingSwitch && p === this.user) { /* switch happens when the receiver controls the ball */ }
    }

    shotOnTarget(team) {
      const side = this.attackDir(team);
      const b = cloneBall(this.ball);
      for (let i = 0; i < 120; i++) {
        const prevX = b.p.x;
        ballStep(b, 1 / 40);
        if ((prevX - side * HL) * side < 0 && (b.p.x - side * HL) * side >= 0) return Math.abs(b.p.z) < HGW && b.p.y < GH;
      }
      return false;
    }

    // ------------------------------------------------ main loop
    step(dt = DT) {
      this.frame++;
      this.time += dt;
      if (this.phase === 'halftime' || this.phase === 'fulltime') return;
      this.halfClock += dt;
      if (this.pressHelpT > 0) this.pressHelpT -= dt;

      const endAt = (this.halfMinutes * 60) * (1 + this.stoppage[this.half - 1] / 45);
      if (this.halfClock >= endAt && this.phase !== 'goal') {
        if (this.half === 1) { this.phase = 'halftime'; this.sound('whistle', 1); this.log('half', 'Intervalo'); }
        else { this.phase = 'fulltime'; this.sound('whistle_end', 1); this.log('end', 'Fim de jogo'); }
        this.owner = null; this.holder = null;
        return;
      }

      if (this.phase === 'goal') {
        this.goalTimer -= dt;
        this.celebrate(dt);
        for (const p of this.players) this.movePlayer(p, dt);
        ballStep(this.ball, dt);
        if (this.goalTimer <= 0) this.setupKickoff(this.goalConceder);
        return;
      }

      if (this.owner === null && this.holder === null && this.phase === 'play') this.computeChasers();
      for (const p of this.players) {
        if (this.isUser(p) && this.phase !== 'goal') this.applyUser(p, dt);
        else this.think(p, dt);
      }
      if (this.phase === 'restart') this.restartStep(dt);

      for (const p of this.players) this.movePlayer(p, dt);
      for (const p of this.players) this.updateAction(p, dt);
      this.separate();
      this.updateBall(dt);
      if (this.phase === 'play') this.checkBall();
      const pt = this.possessionTeam();
      if (pt !== null && this.phase === 'play') this.teams[pt].stats.poss += dt;
      if (this.userTeam >= 0) this.autoSwitch();
    }

    continueSecondHalf() {
      if (this.phase !== 'halftime') return;
      this.half = 2; this.halfClock = 0;
      this.setupKickoff(1);
      this.sound('whistle', 1);
    }

    restartStep(dt) {
      const r = this.restart;
      if (!r) return;
      r.t += dt;
      const p = r.taker;
      p.target = p.p; p.vel = V();
      if (r.type !== 'throw') { this.ball.p = V(r.spot.x, R, r.spot.z); this.ball.v = V(); }
      else { this.ball.p = add(p.p, V(0, 2.0, 0)); }
      if (r.taken) return;
      const delay = { kickoff: 1.0, throw: 1.1, corner: 2.2, goalkick: 1.8 }[r.type];
      const userTaking = this.userTeam === r.team && this.user === p && !this.userAutopilot;
      if (r.t > (userTaking ? 7 : delay)) this.takeRestart('auto', null);
    }

    autoSwitch() {
      const u = this.user;
      if (!u) { this.switchUserTo(null); return; }
      if (this.owner && this.owner.team === this.userTeam && this.owner !== u) { this.switchUserTo(this.owner); return; }
      if (this.owner || this.holder || this.phase !== 'play') return;
      const t = this.userTeam;
      if (this.pass && this.pass.team === t && this.pass.recv && this.pass.recv !== u && !this.pass.recv.isGK) { this.switchUserTo(this.pass.recv); return; }
      // Loose ball: hand control to the teammate who will clearly get there first.
      const c = this.chaser[t];
      if (c && c !== u && !c.isGK && this.time - (this.userSwitchT || 0) > 0.8) {
        const ut = this.interceptTime(u);
        if (ut > this.chaseTime[t] + 0.6) this.switchUserTo(c);
      }
    }

    // ------------------------------------------------ prediction / chasing
    computeChasers() {
      if (this.frame - this.predFrame >= 3 || this.predVersion !== this.trajVersion) {
        this.predFrame = this.frame; this.predVersion = this.trajVersion;
        const b = cloneBall(this.ball);
        const s = [];
        for (let i = 1; i <= 45; i++) { ballStep(b, 1 / 15); s.push({ t: i / 15, p: V(b.p.x, b.p.y, b.p.z) }); }
        this.samples = s;
      }
      for (let t = 0; t < 2; t++) {
        let best = null, bt = 1e9, bp = null;
        const side = -this.attackDir(t);
        for (const p of this.teams[t].players) {
          if (p.isGK && !this.inBox(this.ball.p, side) && !(this.samples.length && this.inBox(this.samples[Math.min(14, this.samples.length - 1)].p, side))) continue;
          if (p.stun > 0.6 || (p.action && p.action.type === 'dive')) continue;
          if (this.isUser(p)) continue;
          const r = this.interceptOf(p);
          let tt = r.t;
          if (this.pass && this.pass.recv === p) tt -= 0.4;
          if (tt < bt) { bt = tt; best = p; bp = r.p; }
        }
        this.chaser[t] = best; this.chasePoint[t] = bp || this.ball.p; this.chaseTime[t] = bt;
      }
    }
    interceptOf(p) {
      const reach = p.isGK ? 1.0 : 0.7;
      const maxH = p.isGK ? 2.4 : 2.0;
      const sp = p.maxSpeed * 0.95;
      let last = null;
      for (const s of this.samples) {
        if (s.p.y > maxH) continue;
        if (Math.abs(s.p.x) > HL + 1 || Math.abs(s.p.z) > HW + 1) break;
        const d = Math.max(0, fdist(p.p, s.p) - reach);
        const tt = d / sp + 0.18;
        last = s;
        if (tt <= s.t) return { t: s.t, p: s.p };
      }
      if (!last) return { t: fdist(p.p, this.ball.p) / sp + 3, p: this.ball.p };
      return { t: last.t + fdist(p.p, last.p) / sp, p: last.p };
    }
    interceptTime(p) { return this.samples.length ? this.interceptOf(p).t : fdist(p.p, this.ball.p) / p.maxSpeed; }

    // ------------------------------------------------ AI
    think(p, dt) {
      if (p.callT > 0) p.callT -= dt;
      if (p.runT > 0) p.runT -= dt;
      if (p.isGK) { this.keeperThink(p, dt); return; }
      if (this.phase === 'restart') { this.restartPositioning(p); return; }
      if (this.owner === p) { this.carrierThink(p, dt); return; }
      const team = p.team;
      if (this.owner === null && this.holder === null && this.chaser[team] === p) {
        p.target = this.chasePoint[team]; p.speedF = 1; return;
      }
      const pt = this.possessionTeam();
      if (pt === team) this.supportThink(p, dt); else this.defendThink(p, dt);
    }

    shapeSpot(p, inPoss) {
      const dir = this.attackDir(p.team);
      const bx = this.ball.p.x * dir, bz = this.ball.p.z;
      let defA, fwdA;
      if (inPoss) { defA = clamp(bx - 30, -38, 8); fwdA = clamp(defA + 40, 0, 38); }
      else { defA = clamp(bx - 18, -41, -2); fwdA = defA + 26; }
      const a = defA + clamp(p.depth, 0, 1) * (fwdA - defA);
      const z = p.lat * (inPoss ? 28 : 21) + bz * (inPoss ? 0.2 : 0.38);
      return V(a * dir, 0, clamp(z, -HW + 1.5, HW - 1.5));
    }

    restartPositioning(p) {
      const r = this.restart;
      if (!r) return;
      if (p === r.taker) return;
      if (r.type === 'kickoff') { p.target = p.p; return; }
      let t = r.targets && r.targets.get(p);
      if (!t) {
        t = this.shapeSpot(p, p.team === r.team);
        if (r.type === 'throw' && p.team === r.team) {
          const near = this.teams[r.team].players.filter(q => q !== r.taker && !q.isGK).sort((a, b) => fdist(a.p, r.spot) - fdist(b.p, r.spot));
          const i = near.indexOf(p);
          if (i < 2) t = V(r.spot.x + this.attackDir(r.team) * (i === 0 ? 6 : -4), 0, r.spot.z - Math.sign(r.spot.z) * (i === 0 ? 7 : 10));
        }
        if (r.type === 'goalkick' && p.team !== r.team) {
          const side = -this.attackDir(r.team);
          if (t.x * side > HL - PA_DEPTH - 1) t = V(side * (HL - PA_DEPTH - 2), 0, t.z);
        }
      }
      if (p.team !== r.team && fdist(t, r.spot) < 4) t = add(r.spot, mul(fnorm(sub(t, r.spot)), 4));
      p.target = t; p.speedF = 0.75;
    }

    supportThink(p, dt) {
      const dir = this.attackDir(p.team);
      let t = this.shapeSpot(p, true);
      p.speedF = 0.7;
      const carrier = this.owner && this.owner.team === p.team ? this.owner : null;
      // Runs in behind from forwards and wide players.
      if (carrier && p.runT <= 0 && (p.role === 'ST' || p.role === 'W' || p.role === 'AM') && this.rng.chance(dt * 0.35)) {
        const lastLine = Math.max(...this.opps(p.team).filter(o => !o.isGK).map(o => o.p.x * dir));
        const ax = clamp(Math.max(lastLine + this.rng.range(5, 12), p.p.x * dir + 6), -30, HL - 7);
        p.runTarget = V(ax * dir, 0, clamp(p.p.z + this.rng.range(-8, 8), -HW + 4, HW - 4));
        p.runT = 2.2;
      }
      if (p.runT > 0) { t = p.runTarget; p.speedF = 0.95; }
      else if (carrier) {
        // Offer a short option when the carrier is pressed.
        const d = fdist(p.p, carrier.p);
        if (d < 18 && this.nearestOpp(carrier).d < 4) {
          const away = fnorm(sub(p.p, carrier.p));
          const spot = add(carrier.p, mul(away, 11));
          t = lerpV(t, spot, 0.6);
        }
      }
      if (this.pass && this.pass.recv === p) { t = this.chasePoint[p.team] || t; p.speedF = 1; }
      p.target = V(clamp(t.x, -HL + 1, HL - 1), 0, clamp(t.z, -HW + 1, HW - 1));
      if (fdist(p.p, p.target) > 6) p.speedF = Math.max(p.speedF, 0.85);
    }

    defendThink(p, dt) {
      const team = p.team;
      const goal = this.ownGoal(team);
      const carrier = this.owner || this.holder;
      const ballP = this.ball.p;
      const others = this.teams[team].players.filter(q => !q.isGK && !this.isUser(q));
      const ref = carrier ? carrier.p : ballP;
      others.sort((a, b) => fdist(a.p, ref) - fdist(b.p, ref));
      const userNear = this.user && this.user.team === team && fdist(this.user.p, ref) < 3.5 && !this.userAutopilot;
      const presserIdx = others.indexOf(p);
      const isPresser = presserIdx === 0 && !userNear;
      const isCover = presserIdx === (userNear ? 0 : 1) || (presserIdx === 1 && this.pressHelpT > 0 && this.user && this.user.team === team);
      const helpPress = this.pressHelpT > 0 && this.user && this.user.team === team && presserIdx <= 1;

      if (carrier && (isPresser || helpPress) && !this.holder) {
        const toGoal = fnorm(sub(goal, carrier.p));
        p.target = add(carrier.p, mul(toGoal, fdist(p.p, carrier.p) > 3 ? 1.2 : 0.6));
        p.speedF = fdist(p.p, carrier.p) > 4 ? 1 : 0.65;
        const rate = 1.4 * attr(p.a.defending, 0.5, 1.4) * (this.userTeam >= 0 && carrier.team === this.userTeam ? [0.55, 0.85, 1.15][this.difficulty] : 1);
        if (fdist(p.p, carrier.p) < 1.35 && p.canAct && this.rng.chance(dt * rate)) this.startTackle(p);
        return;
      }
      let t = this.shapeSpot(p, false);
      if (isCover && carrier) {
        const toGoal = fnorm(sub(goal, carrier.p));
        t = lerpV(t, add(carrier.p, mul(toGoal, 6)), 0.6);
      } else {
        // Mark the nearest attacker in our zone, goal-side.
        let mark = null, md = 9;
        for (const o of this.opps(team)) {
          if (o.isGK || o === carrier) continue;
          const d = fdist(o.p, t);
          if (d < md) { md = d; mark = o; }
        }
        if (mark) {
          const toGoal = fnorm(sub(goal, mark.p));
          t = lerpV(t, add(mark.p, mul(toGoal, 1.8)), p.role === 'CB' || p.role === 'FB' ? 0.75 : 0.5);
        }
      }
      p.target = t;
      p.speedF = fdist(p.p, t) > 5 ? 0.9 : 0.55;
    }

    passRisk(from, to, team, speed) {
      const d = fdist(from, to);
      const dir = fnorm(sub(to, from));
      let risk = 0;
      for (const o of this.opps(team)) {
        const rel = flat(sub(o.p, from));
        const along = dot(rel, dir);
        if (along < 0.5 || along > d + 1.5) continue;
        const perp = flen(sub(rel, mul(dir, along)));
        if (perp < 0.85) { risk = 1; break; }            // standing in the lane: the ball hits him
        const tBall = along / speed;
        const tOpp = Math.max(0, perp - 0.8) / (o.maxSpeed * 0.85) + 0.22;
        if (tOpp < tBall) risk = Math.max(risk, clamp01((tBall - tOpp) / 0.4 + 0.35));
      }
      return risk;
    }

    carrierThink(p, dt) {
      p.holdT += dt;
      if (p.busy) return;
      const dir = this.attackDir(p.team);
      const goal = this.goalOf(p.team);
      if (p.queued) { return; }
      p.decT -= dt;
      const press = this.nearestOpp(p);
      if (p.decT > 0 && press.d > 1.6) {
        p.target = add(p.p, mul(p.dribDir || V(dir, 0, 0), 6));
        return;
      }
      const vsUser = this.userTeam >= 0 && p.team !== this.userTeam;
      p.decT = this.rng.range(0.2, 0.38) + (vsUser ? [0.25, 0.08, 0][this.difficulty] : 0);
      const ax = this.ax(p.team, p.p);
      let best = { u: -1e9 };
      const consider = o => { o.u += this.rng.next() * 0.06; if (o.u > best.u) best = o; };

      // Shot
      const dGoal = fdist(p.p, goal);
      if (ax > HL - 32 && Math.abs(p.p.z) < 25) {
        const xg = this.xg(p, p.p, false);
        let u = xg * 7;
        if (dGoal > 25) u -= 0.25;
        if (xg > 0.035) consider({ kind: 'shot', u: u + 0.12, xg });
      }
      // Passes
      for (const q of this.mates(p)) {
        if (q.isGK && ax > -25) continue;
        let tgt = add(q.p, mul(q.vel, 0.45));
        if (q.runT > 0) {
          // Through ball: into the run's path, a few metres ahead of the runner.
          tgt = add(q.p, mul(fnorm(sub(q.runTarget, q.p)), clamp(fdist(q.runTarget, q.p), 3, 9)));
        }
        const d = fdist(p.p, tgt);
        if (d < 5 || d > 48 || (q.runT > 0 && d > 32)) continue;
        const prog = this.ax(p.team, tgt) - ax;
        const space = Math.min(9, this.nearestOpp(q, tgt).d);
        const ground = d < 32;
        const risk = ground ? this.passRisk(p.p, tgt, p.team, (groundPassSpeed(d, 7) + 7) * 0.5) : (space < 3 ? 0.6 : 0.2);
        let u = 0.18 + prog * 0.018 + space * 0.035 - risk * 1.3 - (ground ? 0 : 0.12) - d * 0.003;
        const tax = this.ax(p.team, tgt);
        if (tax > HL - PA_DEPTH && Math.abs(tgt.z) < PA_HALF_W) u += 0.22;
        if (q.runT > 0 && risk < 0.4) u += 0.12;
        if (q.callT > 0 && this.isUser(q)) u += 0.8;
        if (press.d < 1.8) u += 0.1;
        consider({ kind: q.runT > 0 ? 'through' : ground ? 'pass' : 'lofted', u, target: tgt, recv: q });
      }
      // Cross from wide areas.
      if (ax > HL - 28 && Math.abs(p.p.z) > 13) {
        const inBox = this.mates(p).filter(q => !q.isGK && this.inBox(q.p, dir));
        for (const q of inBox) {
          const space = Math.min(6, this.nearestOpp(q).d);
          consider({ kind: 'cross', u: 0.3 + space * 0.06, target: add(q.p, mul(q.vel, 0.9)), recv: q });
        }
      }
      // Dribble
      let ahead = 99;
      const fwd = fnorm(lerpV(V(dir, 0, 0), fnorm(sub(goal, p.p)), ax > 20 ? 0.8 : 0.3));
      for (const o of this.opps(p.team)) {
        const rel = flat(sub(o.p, p.p));
        const along = dot(rel, fwd);
        if (along < 0 || along > 10) continue;
        if (flen(sub(rel, mul(fwd, along))) < 2.5 + along * 0.3) ahead = Math.min(ahead, along);
      }
      let dribDir = fwd;
      if (press.p && press.d < 4) {
        const away = fnorm(sub(p.p, press.p.p));
        dribDir = fnorm(add(fwd, mul(away, 0.8)));
      }
      if (Math.abs(p.p.z) > HW - 4) dribDir = fnorm(add(dribDir, V(0, 0, -Math.sign(p.p.z) * 0.6)));
      const dribU = 0.15 + Math.min(ahead, 10) * 0.035 + (p.a.dribbling - 65) * 0.006 - (press.d < 1.6 ? 0.35 : 0) + (ax < -20 ? -0.1 : 0) + (ahead > 9 ? 0.15 : 0);
      consider({ kind: 'dribble', u: dribU, dir: dribDir });
      // Clearance when in trouble in our own third.
      if (ax < -30 && press.d < 2.2) consider({ kind: 'clear', u: 0.25, target: V(dir * this.rng.range(0, 20), 0, this.rng.range(-25, 25)) });

      if (best.kind === 'shot') {
        const gk = this.keeper(1 - p.team);
        const z = (gk.p.z > p.p.z * 0.2 ? -1 : 1) * (HGW - this.rng.range(0.5, 1.4));
        const h = this.rng.chance(0.6) ? 0.3 : this.rng.range(0.8, 1.9);
        this.startKick(p, { kind: 'shot', target: V(goal.x, h, z), power: dGoal < 14 ? this.rng.range(0.55, 0.8) : this.rng.range(0.75, 0.95) });
      } else if (best.kind === 'pass' || best.kind === 'through' || best.kind === 'lofted' || best.kind === 'cross') {
        this.startKick(p, { kind: best.kind, target: best.target, power: 1, recv: best.recv });
      } else if (best.kind === 'clear') {
        this.startKick(p, { kind: 'clear', target: best.target, power: 1 });
      } else {
        p.dribDir = best.dir;
        p.target = add(p.p, mul(best.dir, 6));
        p.speedF = ahead > 6 ? 0.95 : 0.7;
      }
    }

    keeperThink(gk, dt) {
      const team = gk.team;
      const side = -this.attackDir(team);
      const goal = V(side * HL, 0, 0);
      gk.speedF = 0.6;
      if (this.holder === gk) {
        if (this.phase === 'restart') return;
        gk.holdT -= dt;
        gk.target = gk.p;
        gk.facing = turnTo(gk.facing, side > 0 ? Math.PI : 0, 6 * dt);
        if (gk.holdT <= 0 && gk.canAct) this.distribute(gk);
        return;
      }
      if (this.owner === gk) {
        if (gk.busy) return;
        gk.holdT += dt;
        if (gk.holdT < 0.6 && this.nearestOpp(gk).d > 6) { gk.target = gk.p; return; }
        const short = this.bestShortOption(gk, 30, true);
        if (short && this.nearestOpp(gk).d > 3) this.startKick(gk, { kind: 'pass', target: short.p, power: 1, recv: short });
        else this.startKick(gk, { kind: 'clear', target: V(-side * this.rng.range(5, 20), 0, this.rng.range(-20, 20)), power: 1 });
        return;
      }
      if (gk.busy || this.phase === 'restart' && this.restart && this.restart.taker === gk) return;
      const b = this.ball;
      // 1. Shot reaction.
      if (this.lastShot && this.lastShot.team !== team && this.owner === null && b.v.x * side > 6) {
        if (this.shotSeen[team] !== this.trajVersion) { this.shotSeen[team] = this.trajVersion; this.shotSeenT = this.time; this.reactNoise = Math.abs(this.rng.gauss()) * 0.07; }
        const vsUser = this.userTeam >= 0 && team !== this.userTeam;
        const reaction = attr(gk.a.gk, 0.3, 0.12) * (vsUser ? [1.35, 1, 0.85][this.difficulty] : 1) + this.reactNoise;
        if (this.time - this.shotSeenT >= reaction && this.time - this.diveAt[team] > 1.2) {
          const c = this.predictCrossing(gk.p.x, side);
          if (c) {
            const misread = this.rng.gauss() * 0.35 * clamp01(len(b.v) / 25) * attr(gk.a.gk, 1, 0.35);
            const cz = c.p.z + misread;
            const lateral = cz - gk.p.z;
            const diveMax = attr(gk.a.gk, 3.6, 5.6);
            const standing = Math.abs(lateral) < 0.5 && c.p.y < 1.9;
            if (!standing && Math.abs(lateral) < 4.8 && c.t > 0.06) {
              const travel = Math.max(0, Math.abs(lateral) - 0.85);
              const speed = Math.min(diveMax, Math.max(2.5, travel / Math.max(0.1, c.t - 0.16)));
              const jump = c.p.y > 1.4 ? 1.0 : c.p.y > 0.7 ? 0.5 : 0;
              this.startDive(gk, V(gk.p.x - side * 0.25, 0, gk.p.z + Math.sign(lateral) * (travel + 0.15)), speed, jump);
              this.diveAt[team] = this.time;
              this.sound('dive', 0.6);
              return;
            }
            gk.target = V(gk.p.x, 0, clamp(cz, -HGW, HGW)); gk.speedF = 1;
            return;
          }
        }
        return;
      }
      // 2. Loose ball in the box: claim it.
      const ballInBox = this.inBox(b.p, side);
      if (this.owner === null && this.holder === null && ballInBox && this.chaser[team] === gk) {
        gk.target = this.chasePoint[team]; gk.speedF = 1; return;
      }
      // 3. One-on-one: narrow the angle.
      const carrier = this.owner;
      if (carrier && carrier.team !== team) {
        const dGoal = fdist(carrier.p, goal);
        if (dGoal < 20 && ballInBox && this.nearestOpp(carrier).d > 3) {
          gk.target = add(goal, mul(fnorm(sub(carrier.p, goal)), Math.min(dGoal - 2.5, 7)));
          gk.speedF = 0.95;
          if (fdist(gk.p, b.p) < 1.5 && gk.canAct && this.rng.chance(dt * 4)) this.startSlide(gk, fnorm(sub(b.p, gk.p)));
          return;
        }
      }
      // 4. Positioning on the ball-goal line.
      const toB = flat(sub(b.p, goal));
      const dist = flen(toB);
      let depth = clamp(dist * 0.09, 0.6, 3.5);
      const bax = this.ax(team, b.p);
      if (bax > 0) depth = lerp(3.5, 12, clamp01(bax / 30));
      let t = add(goal, mul(fnorm(toB), depth));
      t = V(clamp(t.x * side, HL - 16, HL - 0.3) * side, 0, clamp(t.z, -HGW - 0.5, HGW + 0.5));
      gk.target = t;
      gk.speedF = fdist(gk.p, t) > 3 ? 0.8 : 0.45;
    }

    predictCrossing(planeX, side) {
      const b = cloneBall(this.ball);
      let t = 0;
      for (let i = 0; i < 150; i++) {
        const prev = b.p;
        ballStep(b, 1 / 60);
        t += 1 / 60;
        if ((prev.x - planeX) * side < 0 && (b.p.x - planeX) * side >= 0) {
          return { p: lerpV(prev, b.p, invLerp(prev.x * side, b.p.x * side, planeX * side)), t };
        }
      }
      return null;
    }

    distribute(gk) {
      const dir = this.attackDir(gk.team);
      let best = null, bu = -1e9;
      for (const q of this.mates(gk)) {
        const d = fdist(q.p, gk.p);
        if (d < 8 || d > 60) continue;
        const space = Math.min(10, this.nearestOpp(q).d);
        const u = space * 0.1 + (d < 30 ? 0.2 : 0) + this.rng.next() * 0.2 - (d > 30 && space < 5 ? 0.5 : 0);
        if (u > bu) { bu = u; best = q; }
      }
      if (best && fdist(best.p, gk.p) < 30) this.startKick(gk, { kind: 'gkthrow', target: best.p, power: 1, recv: best });
      else {
        const fwd = best || this.mates(gk).find(q => q.role === 'ST');
        this.startKick(gk, { kind: 'gkkick', target: add(fwd.p, V(dir * 4, 0, 0)), power: 1, recv: fwd });
      }
    }

    celebrate(dt) {
      const scorer = this.goalScorer;
      for (const p of this.players) {
        if (p.team === this.goalTeam && !p.isGK && scorer) {
          const corner = V(Math.sign(scorer.p.x || 1) * (HL - 4), 0, Math.sign(scorer.p.z || 1) * (HW - 4));
          p.target = p === scorer ? corner : lerpV(p.p, scorer.p, 0.5);
          p.speedF = p === scorer ? 0.8 : 0.6;
        } else { p.target = p.p; p.speedF = 0.3; }
      }
    }

    // ------------------------------------------------ kinematics
    movePlayer(p, dt) {
      if (p.stun > 0) p.stun -= dt;
      if (p.touchCd > 0) p.touchCd -= dt;
      if (p.queuedT > 0) { p.queuedT -= dt; if (p.queuedT <= 0) p.queued = null; }
      const a = p.action;
      if (a && a.type === 'dive') {
        const k = a.t < 0.35 ? 1 : Math.max(0, 1 - (a.t - 0.35) / 0.3);
        p.vel = mul(a.dir, a.speed * k);
        p.p = add(p.p, mul(p.vel, dt));
        p.jump = a.jump * Math.sin(Math.min(1, a.t / 0.6) * Math.PI);
        return;
      }
      if (a && a.type === 'slide') {
        const k = Math.max(0, 1 - a.t / 0.75);
        p.vel = mul(a.dir, a.speed * k);
        p.p = add(p.p, mul(p.vel, dt));
        this.clampToArena(p);
        return;
      }
      let maxS = p.maxSpeed * p.speedF;
      if (a) maxS *= a.type === 'throw' ? 0 : 0.4;
      if (p.stun > 0) maxS *= 0.15;
      if (this.owner === p) maxS *= attr(p.a.dribbling, 0.84, 0.95);
      if (this.holder === p) maxS = Math.min(maxS, 2);
      const to = flat(sub(p.target, p.p));
      const d = flen(to);
      const desired = d > 0.05 ? mul(to, Math.min(maxS, d * 2.2) / d) : V();
      const dv = sub(desired, p.vel);
      const dvl = flen(dv);
      const braking = flen(desired) < p.speed - 0.1;
      const maxDv = (braking ? 1.9 : 1) * p.accel * dt;
      p.vel = dvl > maxDv ? add(p.vel, mul(dv, maxDv / dvl)) : desired;
      if (this.owner === p) {
        // With the ball, the body can only turn so fast: velocity follows the facing.
        const sp = p.speed;
        const want = sp > 0.3 ? angOf(p.vel) : p.facing;
        p.facing = turnTo(p.facing, want, attr(p.a.dribbling, 5, 9) / (1 + sp * 0.12) * dt);
        const along = Math.max(0, dot(p.vel, dirOf(p.facing)));
        p.vel = mul(dirOf(p.facing), Math.min(sp, along + 0.4 * sp));
      } else {
        const sp = p.speed;
        let want = p.facing;
        if (a && a.type === 'kick' && a.kick.target) want = angOf(sub(a.kick.target, p.p));
        else if (sp > 1.2) want = angOf(p.vel);
        else if (this.phase !== 'goal') want = angOf(sub(this.ball.p, p.p));
        p.facing = turnTo(p.facing, want, attr(p.a.accel, 7, 11) * dt);
      }
      p.p = add(p.p, mul(p.vel, dt));
      p.dist += p.speed * dt;
      p.anim += p.speed * dt;
      if (p.jump > 0) p.jump = Math.max(0, p.jump - dt * 2);
      this.clampToArena(p);
    }
    clampToArena(p) {
      p.p = V(clamp(p.p.x, -HL - 3, HL + 3), 0, clamp(p.p.z, -HW - 3, HW + 3));
    }

    separate() {
      const ps = this.players;
      for (let i = 0; i < ps.length; i++) {
        for (let j = i + 1; j < ps.length; j++) {
          const a = ps[i], b = ps[j];
          const dx = b.p.x - a.p.x, dz = b.p.z - a.p.z;
          const d2 = dx * dx + dz * dz;
          if (d2 > 0.49 || d2 < 1e-8) continue;
          const d = Math.sqrt(d2), push = (0.7 - d) * 0.5;
          const nx = dx / d, nz = dz / d;
          const wa = a === this.owner ? 0.3 : b === this.owner ? 1.7 : 1;
          const wb = 2 - wa;
          a.p = V(a.p.x - nx * push * wa, 0, a.p.z - nz * push * wa);
          b.p = V(b.p.x + nx * push * wb, 0, b.p.z + nz * push * wb);
        }
      }
    }

    updateAction(p, dt) {
      const a = p.action;
      if (!a) return;
      a.t += dt;
      if (!a.done && a.t >= (a.contact || 0)) {
        if (a.type === 'kick' || a.type === 'throw') {
          a.done = true;
          const ok = this.owner === p || this.holder === p || (this.restart && this.restart.taker === p);
          if (ok) this.applyKick(p, a.kick);
        } else if (a.type === 'tackle') {
          a.done = true;
          this.resolveTackle(p, false);
        }
      }
      if (a.type === 'slide' && a.t > 0.08 && a.t < 0.6 && !a.done) this.resolveTackle(p, true);
      if (a.t >= a.dur) {
        p.action = null;
        if (a.type === 'dive') { p.stun = 0.45; p.jump = 0; }
        if (a.type === 'slide') p.stun = a.done ? 0.2 : 0.5;
      }
    }

    resolveTackle(p, slide) {
      const a = p.action;
      const o = this.owner;
      const reachPt = slide ? add(p.p, mul(a.dir, 0.7)) : p.p;
      const ballD = fdist(reachPt, this.ball.p);
      if (o && o.team !== p.team) {
        const near = fdist(o.p, reachPt) < (slide ? 1.3 : 1.45) || ballD < (slide ? 1.0 : 1.1);
        if (!near) { if (!slide) p.stun = 0.25; return; }
        a.done = true;
        const def = p.a.defending, drb = o.a.dribbling;
        let chance = clamp(0.5 + (def - drb) * 0.009 + (slide ? 0.1 : 0), 0.15, 0.88);
        if (this.isUser(p)) chance += 0.08;
        if (this.isUser(o)) chance -= [0.15, 0.06, 0][this.difficulty];
        this.teams[p.team].stats.tackles++;
        if (this.rng.chance(chance)) {
          this.owner = null;
          o.stun = slide ? 0.9 : 0.4;
          o.decT = 0.3;
          this.lastTouch = p;
          this.pass = null;
          this.trajVersion++;
          if (!slide && this.rng.chance(0.55)) { this.giveBall(p); }
          else {
            const dir = slide ? a.dir : fnorm(add(fnorm(sub(this.ball.p, p.p)), V(this.rng.range(-0.5, 0.5), 0, this.rng.range(-0.5, 0.5))));
            this.ball.v = add(mul(dir, slide ? 7 : 4.5), V(0, 0.5, 0));
            p.touchCd = 0.15;
          }
          this.sound('tackle', 0.7);
        } else {
          p.stun = slide ? 0.7 : 0.5;
          if (slide) { p.action.dur = Math.max(a.t + 0.4, a.dur); }
        }
      } else if (!o && !this.holder && ballD < 1.0 && this.ball.p.y < 0.6) {
        a.done = true;
        this.ball.v = add(mul(slide ? a.dir : fnorm(sub(this.ball.p, p.p)), slide ? 7 : 4), V(0, 0.4, 0));
        this.lastTouch = p; this.trajVersion++; this.pass = null; this.lastShot = null;
        p.touchCd = 0.2;
        this.sound('kick', 0.4);
      }
    }

    giveBall(p) {
      this.owner = p; this.holder = null;
      p.holdT = 0; p.decT = this.rng.range(0.1, 0.25);
      this.lastTouch = p;
      this.ball.held = false;
      const pass = this.pass;
      if (pass && pass.team === p.team && pass.passer !== p) {
        if (pass.kind !== 'header' && pass.kind !== 'clear') this.teams[p.team].stats.completed++;
        this.lastPasser = pass.passer;
      } else if (!pass || pass.team !== p.team) this.lastPasser = null;
      this.pass = null; this.lastShot = null;
      if (p.queued && this.isUser(p)) {
        const q = p.queued; p.queued = null;
        if (q.kind === 'shoot') this.userShot(p, q.power, q.dir);
        else this.userPass(p, q.dir, q.kind === 'through');
      }
    }

    updateBall(dt) {
      const b = this.ball;
      if (this.phase === 'restart') return;
      if (this.holder) {
        const h = this.holder;
        b.held = true;
        b.p = add(add(h.p, mul(dirOf(h.facing), 0.3)), V(0, 1.15 + h.jump, 0));
        b.v = V();
        return;
      }
      if (this.owner) {
        const o = this.owner;
        const ahead = 0.42 + Math.min(0.35, o.speed * 0.045);
        const fp = add(o.p, mul(dirOf(o.facing), ahead));
        const k = Math.min(1, 18 * dt);
        b.p = V(lerp(b.p.x, fp.x, k), R, lerp(b.p.z, fp.z, k));
        b.v = V(o.vel.x, 0, o.vel.z);
        b.s = V(b.v.z / R, 0, -b.v.x / R);
        return;
      }
      const hit = ballStep(b, dt);
      if (hit) { this.sound('post', 1); this.log('post', 'Na trave!', this.lastTouch ? this.lastTouch.team : null); }
      this.checkTouches();
    }

    checkTouches() {
      const b = this.ball;
      let best = null, bd = 1e9, bmode = null;
      for (const p of this.players) {
        if (p.touchCd > 0) continue;
        const diving = p.action && p.action.type === 'dive';
        if (p.stun > 0 && !diving) continue;
        if (p.action && (p.action.type === 'kick' || p.action.type === 'throw')) continue;
        // The kicker's opponents cannot touch a ball that has only just left the boot (it is past them).
        if (this.time - this.lastKickTime < 0.1 && this.lastTouch && p.team !== this.lastTouch.team) continue;
        const side = -this.attackDir(p.team);
        const gkHands = p.isGK && this.inBox(b.p, side) && this.phase === 'play';
        const d = fdist(p.p, b.p);
        let mode = null;
        if (diving) {
          const body = p.p;
          const reach = 1.4;
          if (d < reach && b.p.y < 0.6 + p.jump + 1.2) mode = 'hands';
        } else if (gkHands) {
          if (d < 1.0 && b.p.y < 2.5 + p.jump) mode = 'hands';
        } else if (b.p.y < 0.95) {
          if (d < (p.isGK ? 0.9 : 0.75)) mode = 'foot';
        } else if (b.p.y < 2.25 && !p.isGK) {
          if (d < 0.6) mode = 'head';
        }
        if (mode && d < bd) { bd = d; best = p; bmode = mode; }
      }
      if (!best) return;
      if (bmode === 'hands') this.keeperTouch(best);
      else if (bmode === 'head') this.headerTouch(best);
      else this.footTouch(best);
    }

    keeperTouch(gk) {
      const b = this.ball;
      const speed = len(b.v);
      const side = -this.attackDir(gk.team);
      const shot = this.lastShot && this.lastShot.team !== gk.team && b.v.x * side > 3;
      const skill = gk.a.gk;
      const diving = gk.action && gk.action.type === 'dive';
      if (shot) {
        const stretch = diving ? clamp01((fdist(gk.p, b.p) - 0.6) / 0.8) : 0;
        const vsUser = this.userTeam >= 0 && gk.team !== this.userTeam;
        const flight = this.time - this.lastShot.time;
        let pSave = 0.58 + 0.4 * skill / 99 - Math.max(0, speed - 20) * 0.015 - stretch * 0.2 - (b.p.y > 1.9 ? 0.08 : 0) + clamp((flight - 0.45) * 0.45, 0, 0.25);
        if (vsUser) pSave += [-0.12, 0, 0.06][this.difficulty];
        pSave = clamp(pSave, 0.08, 0.95);
        if (!this.rng.chance(pSave)) { gk.touchCd = 0.5; return; }
        this.teams[gk.team].stats.saves++;
        this.sound('save', 1);
        this.lastTouch = gk; this.trajVersion++;
        const catchP = clamp(0.25 + skill / 140 - Math.max(0, speed - 15) * 0.04 - stretch * 0.3, 0.05, 0.9);
        if (this.rng.chance(catchP)) {
          this.catchBall(gk);
          this.log('save', 'Defesa de ' + gk.name, gk.team);
        } else {
          const zs = Math.sign(b.p.z - gk.p.z) || 1;
          if (this.rng.chance(0.5)) b.v = V(side * this.rng.range(0.5, 3), this.rng.range(2, 5), zs * this.rng.range(5, 10));   // pushed wide, often behind for a corner
          else b.v = V(-side * this.rng.range(3, 7), this.rng.range(1, 4), zs * this.rng.range(1, 6));                      // parried back into play
          b.s = V();
          gk.touchCd = 0.6;
          this.lastShot = null; this.pass = null;
          this.log('save', 'Espalmou ' + gk.name, gk.team);
        }
        return;
      }
      // Crosses and loose balls.
      const own = this.lastTouch && this.lastTouch.team === gk.team && this.lastTouch !== gk && b.p.y < 0.6;
      if (own) { this.footTouch(gk); return; }   // no handling back passes
      if (this.rng.chance(b.p.y > 1.2 ? 0.85 : 0.95)) { this.catchBall(gk); return; }
      b.v = V(-side * 5, 3, this.rng.range(-4, 4));
      this.lastTouch = gk; this.trajVersion++; gk.touchCd = 0.5; this.pass = null;
    }

    catchBall(gk) {
      this.holder = gk; this.owner = null;
      this.ball.held = true; this.ball.v = V(); this.ball.s = V();
      gk.holdT = this.rng.range(1.0, 2.2);
      gk.action = null; gk.stun = 0.15; gk.jump = 0;
      this.lastTouch = gk; this.pass = null; this.lastShot = null;
      this.trajVersion++;
    }

    headerTouch(p) {
      const b = this.ball;
      const side = this.attackDir(p.team);
      const goal = this.goalOf(p.team);
      const dGoal = fdist(p.p, goal);
      let vel;
      let isShot = false;
      if (dGoal < 14 && this.ax(p.team, p.p) > HL - 16) {
        const z = this.rng.range(-HGW + 0.4, HGW - 0.4);
        const sd = 0.08 + 0.12 * (1 - p.a.heading / 99);
        const tgt = add(p.p, rotY(sub(V(goal.x, 0, z), flat(p.p)), this.rng.gauss() * sd));
        vel = driven(V(b.p.x, b.p.y, b.p.z), V(tgt.x, 0, tgt.z), lerp(11, 16, p.a.heading / 99), this.rng.range(0.2, 1.8));
        isShot = true;
      } else if (this.ax(p.team, p.p) < -20) {
        const dir = fnorm(V(side, 0, this.rng.range(-0.7, 0.7)));
        vel = add(mul(dir, 12), V(0, 6, 0));
      } else {
        const mate = this.mates(p).filter(q => !q.isGK && fdist(q.p, p.p) < 20 && fdist(q.p, p.p) > 4).sort((a, c) => this.ax(p.team, c.p) - this.ax(p.team, a.p))[0];
        const dir = mate ? fnorm(sub(mate.p, p.p)) : fnorm(V(side, 0, 0));
        vel = add(mul(dir, 9), V(0, 3.5, 0));
      }
      p.jump = 0.35;
      this.sound('header', 0.7);
      this.applyKick(p, { kind: 'header', vel, isShot });
    }

    footTouch(p) {
      const b = this.ball;
      const rel = len(sub(b.v, p.vel));
      const ctrl = p.a.control;
      const blockShot = this.lastShot && this.lastShot.team !== p.team;
      let pc = rel < 9 ? 0.97 : clamp(1 - (rel - 9) * attr(ctrl, 0.075, 0.035), 0.2, 0.95);
      if (blockShot) pc *= 0.35;
      if (b.p.y > 0.5) pc *= 0.85;
      if (this.rng.chance(pc)) {
        b.v = V(p.vel.x, 0, p.vel.z);
        b.p = V(b.p.x, R, b.p.z);
        const passer = this.pass && this.pass.team !== p.team ? this.pass.passer : null;
        if (passer) this.log('int', 'Interceptação de ' + p.name, p.team);
        this.giveBall(p);
        this.sound('touch', 0.35);
      } else {
        const out = fnorm(add(mul(fnorm(b.v), -0.4), V(this.rng.range(-1, 1), 0, this.rng.range(-1, 1))));
        b.v = add(mul(out, Math.min(9, rel * 0.35 + 1.5)), V(0, this.rng.range(0, 2.5), 0));
        p.touchCd = 0.35;
        this.lastTouch = p; this.trajVersion++;
        if (this.pass && this.pass.team !== p.team) this.pass = null;
        if (blockShot) { this.lastShot = null; this.log('block', 'Bloqueio de ' + p.name, p.team); }
        this.sound('touch', 0.5);
      }
    }

    // ------------------------------------------------ rules
    checkBall() {
      const b = this.ball.p;
      if (Math.abs(b.x) > HL + R) {
        const side = Math.sign(b.x);
        if (Math.abs(b.z) < HGW && b.y < GH) { this.goal(side); return; }
        const defending = this.attackDir(0) === side ? 1 : 0;
        const attacking = 1 - defending;
        const last = this.lastTouch ? this.lastTouch.team : attacking;
        if (last === defending) this.beginRestart('corner', attacking, V(side * (HL - 0.4), R, Math.sign(b.z || 1) * (HW - 0.4)));
        else this.beginRestart('goalkick', defending, V(side * (HL - 5.5), R, Math.sign(b.z || 1) * 4));
        return;
      }
      if (Math.abs(b.z) > HW + R) {
        const last = this.lastTouch ? this.lastTouch.team : 0;
        this.beginRestart('throw', 1 - last, V(clamp(b.x, -HL + 1, HL - 1), R, Math.sign(b.z) * HW));
      }
    }

    goal(side) {
      const team = this.attackDir(0) === side ? 0 : 1;
      const scorer = this.lastTouch;
      const own = scorer && scorer.team !== team;
      this.teams[team].stats.goals++;
      if (scorer && !own) scorer.goals++;
      this.goalTeam = team; this.goalConceder = 1 - team; this.goalScorer = own ? null : scorer;
      this.phase = 'goal'; this.goalTimer = 4.2;
      this.owner = null; this.holder = null; this.pass = null; this.lastShot = null;
      const assist = !own && this.lastPasser && this.lastPasser.team === team && this.lastPasser !== scorer ? this.lastPasser : null;
      this.log('goal', (own ? 'Gol contra de ' : 'GOL! ') + (scorer ? scorer.name : '') + (assist ? ' (assist. ' + assist.name + ')' : ''), team);
      this.sound('goal', 1);
      this.lastGoal = { team, scorer, own, minute: this.clockText() };
    }
  }

  const api = { Match, CLUBS, FORMATIONS, constants: { HL, HW, HGW, GH, GD, R, PA_DEPTH, PA_HALF_W, GA_DEPTH }, V, ballStep, newBall, cloneBall, lofted, driven, groundPass, groundPassSpeed, rollingDistance, makeRng, hexLum };
  root.NXEngine = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
