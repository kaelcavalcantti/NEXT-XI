"""Renders top-down frames of a headless match trace (debug aid, not part of the game)."""
import csv, sys
from PIL import Image, ImageDraw

def render(trace, out, frames):
    rows = list(csv.reader(open(trace)))
    tiles = []
    for fi in frames:
        r = rows[min(fi, len(rows) - 1)]
        W, H, s = 105, 68, 6
        im = Image.new("RGB", ((W + 6) * s, (H + 6) * s + 14), (34, 110, 50))
        d = ImageDraw.Draw(im)
        ox, oy = 3 * s, 3 * s
        P = lambda x, z: (ox + (x + W / 2) * s, oy + (H / 2 - z) * s)
        d.rectangle([P(-52.5, 34), P(52.5, -34)], outline="white", width=2)
        d.line([P(0, 34), P(0, -34)], fill="white", width=2)
        for side in (-1, 1):
            x0, x1 = side * 52.5, side * (52.5 - 16.5)
            d.rectangle([P(min(x0, x1), 20.16), P(max(x0, x1), -20.16)], outline="white", width=2)
            d.line([P(side * 52.5, 3.66), P(side * 52.5, -3.66)], fill="yellow", width=5)
        t, ph, bx, by, bz, own = r[:6]
        rest = r[6:]
        for i in range(0, len(rest), 6):
            pid, team, x, z, act, intent = rest[i:i + 6]
            x, z = float(x), float(z)
            c = (220, 30, 40) if team == "0" else (40, 80, 230)
            cx, cy = P(x, z)
            d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=c, outline="white" if pid == own else None, width=2)
            d.text((cx - 6, cy + 8), intent[:5], fill="white")
        bx, bz = float(bx), float(bz)
        cx, cy = P(bx, bz)
        d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill="white", outline="black")
        d.text((5, im.height - 13), f"t={t} {ph} ballY={by}", fill="white")
        tiles.append(im)
    cols = 2
    rows_n = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (tiles[0].width * cols, tiles[0].height * rows_n))
    for i, tl in enumerate(tiles):
        sheet.paste(tl, ((i % cols) * tl.width, (i // cols) * tl.height))
    sheet.save(out)

if __name__ == "__main__":
    render(sys.argv[1], sys.argv[2], [int(x) for x in sys.argv[3].split(",")])
