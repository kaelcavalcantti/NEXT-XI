// Cartelas: título, tela preta do sussurro, decisão do público e encerramento.
import { EVENTS, shotById } from '../timeline.js';
import { smooth, rng, noise1 } from '../lib/util.js';

function setup(ctx) { const o = ctx.overlay; o.setTransform(1, 0, 0, 1, 0, 0); o.clearRect(0, 0, ctx.W, ctx.H); return o; }
function text(o, s, x, y, { font, size, color = '#d8dde0', alpha = 1, spacing = 0, align = 'center', blur = 0, glow = 0 }) {
  if (alpha <= 0) return;
  o.save(); o.globalAlpha = Math.min(1, alpha);
  o.font = `${size}px ${font}`; o.fillStyle = color; o.textAlign = align; o.textBaseline = 'middle';
  if ('letterSpacing' in o) o.letterSpacing = `${spacing}px`;
  if (glow) { o.shadowColor = 'rgba(160,190,200,0.55)'; o.shadowBlur = glow; }
  if (blur) o.filter = `blur(${blur}px)`;
  o.fillText(s, x, y);
  o.restore();
}
const local = (id, ev) => ev - shotById[id].start;

const title = {
  async create(ctx) {
    const r = rng(9); const motes = Array.from({ length: 140 }, () => [r(), r(), r(), r()]);
    return {
      update(lt) {
        const o = setup(ctx), u = ctx.H / 1080;
        o.fillStyle = '#000'; o.fillRect(0, 0, ctx.W, ctx.H);
        const tin = local('title', EVENTS.titleIn), sin = local('title', EVENTS.subtitleIn), tout = local('title', EVENTS.titleOut);
        const a = smooth(tin, tin + 2.2, lt) * (1 - smooth(tout, tout + 1.0, lt));
        const breath = 1 + 0.012 * Math.sin((lt - tin) * Math.PI * 2 / 4.2);
        // partículas de poeira sob uma luz fria
        for (const [x, y, s, k] of motes) {
          const px = ((x + lt * 0.006 * (k - 0.5)) % 1) * ctx.W, py = ((y - lt * 0.004 * s + 1) % 1) * ctx.H;
          o.fillStyle = `rgba(170,190,200,${0.18 * a * k})`; o.fillRect(px, py, 2 * u, 2 * u);
        }
        o.save(); o.translate(ctx.W / 2, ctx.H * 0.47); o.scale(breath, breath);
        text(o, 'A CASA QUE RESPIRA', 0, 0, { font: 'CinzelB', size: 112 * u, alpha: a, spacing: 14 * u, color: '#cfd8d8', blur: (1 - a) * 8 * u, glow: 30 * u });
        o.restore();
        text(o, 'PARTE 1  —  A HERANÇA', ctx.W / 2, ctx.H * 0.6, { font: 'Cormorant', size: 44 * u, alpha: smooth(sin, sin + 1.6, lt) * (1 - smooth(tout, tout + 1.0, lt)), spacing: 10 * u, color: '#8fa0a4' });
        return { overlayDirty: true, grade: { overlayOn: 1, letterbox: 0 } };
      },
    };
  },
};

const black = { async create(ctx) { return { update() { setup(ctx); return { overlayDirty: true, grade: { overlayOn: 0, fade: 1 } }; } }; } };

function badge(o, letter, x, y, s, color) {
  o.save(); o.fillStyle = color; const r = s * 0.2;
  o.beginPath(); o.moveTo(x + r, y); o.lineTo(x + s - r, y); o.quadraticCurveTo(x + s, y, x + s, y + r); o.lineTo(x + s, y + s - r); o.quadraticCurveTo(x + s, y + s, x + s - r, y + s);
  o.lineTo(x + r, y + s); o.quadraticCurveTo(x, y + s, x, y + s - r); o.lineTo(x, y + r); o.quadraticCurveTo(x, y, x + r, y); o.fill();
  o.fillStyle = '#fff'; o.font = `${s * 0.72}px CinzelB`; o.textAlign = 'center'; o.textBaseline = 'middle'; o.fillText(letter, x + s / 2, y + s * 0.54);
  o.restore();
}

const choice = {
  async create(ctx) {
    const freezeT = EVENTS.freeze;
    const dec = await ctx.getShot('decision');
    const self = {
      update(lt) {
        const out = dec.update(freezeT - shotById.decision.start, freezeT) || {};
        self.draw(lt);
        return { ...out, overlayDirty: true, grade: { ...(out.grade || {}), overlayOn: 1, exposure: 0.6, desat: 0.75, fade: 0 } };
      },
      // a cartela é desenhada sobre o quadro congelado da decisão
      draw(lt) {
        const o = setup(ctx), u = ctx.H / 1080, W = ctx.W, H = ctx.H;
        const g = o.createLinearGradient(0, 0, 0, H); g.addColorStop(0, 'rgba(0,0,0,0.55)'); g.addColorStop(0.5, 'rgba(0,0,0,0.78)'); g.addColorStop(1, 'rgba(0,0,0,0.9)');
        o.fillStyle = g; o.globalAlpha = smooth(0, 0.8, lt); o.fillRect(0, 0, W, H); o.globalAlpha = 1;
        const A = (t0) => smooth(t0, t0 + 0.9, lt);
        text(o, 'VOCÊ DECIDE O DESTINO DE GABRIEL.', W / 2, H * 0.2, { font: 'CinzelB', size: 66 * u, alpha: A(0.6), spacing: 6 * u, color: '#e6e9ea', glow: 24 * u });
        text(o, 'COMENTE:', W / 2, H * 0.33, { font: 'Cormorant', size: 44 * u, alpha: A(2.6), spacing: 12 * u, color: '#a9b4b8' });
        const rowA = H * 0.45, rowB = H * 0.56, bx = W * 0.27, bs = 74 * u;
        const aA = A(3.8), aB = A(5.2);
        if (aA > 0) { o.globalAlpha = aA; badge(o, 'A', bx, rowA - bs / 2, bs, '#b3261e'); o.globalAlpha = 1; }
        text(o, 'GABRIEL ENTRA NO PORÃO.', bx + bs + 34 * u, rowA, { font: 'Cinzel', size: 50 * u, alpha: aA, align: 'left', spacing: 3 * u, color: '#e8e2da' });
        if (aB > 0) { o.globalAlpha = aB; badge(o, 'B', bx, rowB - bs / 2, bs, '#1f5fa8'); o.globalAlpha = 1; }
        text(o, 'GABRIEL ENFRENTA SEU DUPLO.', bx + bs + 34 * u, rowB, { font: 'Cinzel', size: 50 * u, alpha: aB, align: 'left', spacing: 3 * u, color: '#e8e2da' });
        text(o, 'A escolha mais votada nos comentários será o rumo da PARTE 2.', W / 2, H * 0.71, { font: 'CormorantI', size: 42 * u, alpha: A(7.2), color: '#c4c9cb' });
        text(o, 'VOCÊS DECIDEM. NÓS CONTINUAMOS A HISTÓRIA.', W / 2, H * 0.8, { font: 'Cinzel', size: 40 * u, alpha: A(9.4), spacing: 6 * u, color: '#d9b26a', glow: 18 * u });
      },
    };
    return self;
  },
};

const end = {
  async create(ctx) {
    return {
      update(lt) {
        const o = setup(ctx), u = ctx.H / 1080;
        o.fillStyle = '#000'; o.fillRect(0, 0, ctx.W, ctx.H);
        const cutAt = local('end_card', EVENTS.finalSound);
        if (lt < cutAt) {
          const a1 = smooth(0.4, 1.8, lt) * (1 - smooth(cutAt - 0.6, cutAt, lt));
          const a2 = smooth(2.4, 3.8, lt) * (1 - smooth(cutAt - 0.6, cutAt, lt));
          const flick = 0.9 + 0.1 * noise1(lt * 12, 3);
          text(o, 'FIM DA PARTE 1.', ctx.W / 2, ctx.H * 0.44, { font: 'CinzelB', size: 78 * u, alpha: a1 * flick, spacing: 12 * u, color: '#d6dbdc', glow: 26 * u });
          text(o, 'A CASA AINDA NÃO TERMINOU DE ACORDAR.', ctx.W / 2, ctx.H * 0.57, { font: 'Cormorant', size: 46 * u, alpha: a2, spacing: 8 * u, color: '#8d9aa0' });
        }
        return { overlayDirty: true, grade: { overlayOn: 1, letterbox: 0 } };
      },
    };
  },
};

export const shots = { title, black_whisper: black, choice_card: choice, end_card: end };
