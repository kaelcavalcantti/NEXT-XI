"""Software rasterizer for previewing procedural meshes (debug aid, not part of the game).
Renders OBJ files exported by tools/NextXI.GeometryPreview from several views with a
z-buffer, back-face culling (Unity convention: clockwise = front) and simple lighting."""
import sys, math
import numpy as np
from PIL import Image, ImageDraw

COLORS = {"skin": (196, 140, 105), "shirt": (200, 16, 46), "shorts": (245, 245, 245), "socks": (200, 16, 46),
          "boots": (25, 25, 30), "hair": (40, 28, 20), "eyes": (20, 20, 25)}

def load(path):
    V, F = [], []
    mat = "skin"
    for line in open(path):
        if line.startswith("v "):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("usemtl"):
            mat = line.split()[1]
        elif line.startswith("f "):
            F.append(([int(x) - 1 for x in line.split()[1:4]], mat))
    return np.array(V), F

def render(path, yaw_deg, size=(360, 560), scale=260, center_y=0.95, cull=True):
    V, F = load(path)
    a = math.radians(yaw_deg)
    # Camera looks along -Z of the rotated frame (viewer in front of the character at +Z, yaw around Y).
    R = np.array([[math.cos(a), 0, -math.sin(a)], [0, 1, 0], [math.sin(a), 0, math.cos(a)]])
    P = V @ R.T
    W, H = size
    sx = W / 2 - P[:, 0] * scale  # viewer at +z looking -z: world +x appears on screen left
    sy = H - 20 - (P[:, 1]) * scale + (center_y - 0.95) * 0
    depth = P[:, 2]
    img = np.zeros((H, W, 3), dtype=np.uint8); img[:] = (34, 40, 52)
    zbuf = np.full((H, W), -1e9)
    light = np.array([0.4, 0.8, 0.6]); light /= np.linalg.norm(light)
    backfaces = 0
    for (i, j, k), mat in F:
        a3, b3, c3 = P[i], P[j], P[k]
        n = np.cross(b3 - a3, c3 - a3)
        ln = np.linalg.norm(n)
        if ln < 1e-12: continue
        n /= ln
        # Unity: cross(b-a, c-a) points to the front side; camera direction is +Z (towards viewer).
        if n[2] <= 0:
            backfaces += 1
            if cull: continue
        x0, y0, x1, y1, x2, y2 = sx[i], sy[i], sx[j], sy[j], sx[k], sy[k]
        minx, maxx = int(max(0, min(x0, x1, x2))), int(min(W - 1, max(x0, x1, x2) + 1))
        miny, maxy = int(max(0, min(y0, y1, y2))), int(min(H - 1, max(y0, y1, y2) + 1))
        if minx > maxx or miny > maxy: continue
        xs, ys = np.meshgrid(np.arange(minx, maxx + 1) + 0.5, np.arange(miny, maxy + 1) + 0.5)
        d = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(d) < 1e-9: continue
        w0 = ((y1 - y2) * (xs - x2) + (x2 - x1) * (ys - y2)) / d
        w1 = ((y2 - y0) * (xs - x2) + (x0 - x2) * (ys - y2)) / d
        w2 = 1 - w0 - w1
        m = (w0 >= -1e-4) & (w1 >= -1e-4) & (w2 >= -1e-4)
        if not m.any(): continue
        z = w0 * depth[i] + w1 * depth[j] + w2 * depth[k]
        sub = zbuf[miny:maxy + 1, minx:maxx + 1]
        upd = m & (z > sub)
        sub[upd] = z[upd]
        shade = 0.35 + 0.65 * max(0.0, float(np.dot(n, light)))
        col = np.array(COLORS.get(mat, (255, 0, 255))) * shade
        img[miny:maxy + 1, minx:maxx + 1][upd] = col.astype(np.uint8)
    return Image.fromarray(img), backfaces

if __name__ == "__main__":
    out = sys.argv[1]
    files = sys.argv[2:]
    tiles = []
    for f in files:
        for yaw in (0, 90, 35):
            im, _ = render(f, yaw)
            ImageDraw.Draw(im).text((4, 4), f.split("/")[-1].replace(".obj", "") + f" yaw{yaw}", fill=(255, 255, 255))
            tiles.append(im)
    cols = 6
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (tiles[0].width * cols, tiles[0].height * rows))
    for n, t in enumerate(tiles):
        sheet.paste(t, ((n % cols) * t.width, (n // cols) * t.height))
    sheet.save(out)
