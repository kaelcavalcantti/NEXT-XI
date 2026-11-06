// Plano provisório (usado apenas durante o desenvolvimento).
export function placeholder(id) {
  return {
    async create(ctx) {
      return {
        update(lt) {
          const o = ctx.overlay; o.clearRect(0, 0, ctx.W, ctx.H);
          o.fillStyle = '#000'; o.fillRect(0, 0, ctx.W, ctx.H);
          o.fillStyle = '#888'; o.font = `${40 * ctx.scale}px Typewriter`; o.textAlign = 'center';
          o.fillText(`${id}  ${lt.toFixed(2)}s`, ctx.W / 2, ctx.H / 2);
          return { overlayDirty: true, grade: { overlayOn: 1 } };
        },
      };
    },
  };
}
