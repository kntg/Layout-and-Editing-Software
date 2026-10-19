"""Data untuk index.html (simulasi 3D) dari hasil validasi Printech: B-campuran lembar 1.

Jalankan dulu Printech-TestData/validasi.ps1, lalu `python buat-data.py` (butuh PyMuPDF, Pillow, NumPy).
Keluaran: sim-data.js berisi garis kiss/die per desain, penempatan, dan artwork lembar tanpa garis potong (JPEG data URI).
"""
import base64, io, json, math, pathlib, re

import fitz
import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
SRC = HERE.parents[1] / "Printech-TestData" / "03-exports" / "validasi"
SCEN, SHEET, STEP, EPS = "B-campuran", 0, 0.5, 0.03

data = json.load(open(SRC / "validasi.json", encoding="utf-8"))
sc = next(s for s in data if s["name"] == SCEN)
sh = sc["sheets"][SHEET]
ds = {d["id"]: d for d in sc["designs"]}


def bez(p0, p1, p2, p3, n):
    t = np.linspace(0, 1, n + 1)[1:, None]
    return (1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t ** 2 * p2 + t ** 3 * p3


def parse(path):
    """Path Printech (M, C, L, Z absolut, mm) -> kontur tertutup (titik terakhir = titik pertama)."""
    conts, cur = [], None
    for cmd, args in re.findall(r"([MCLZ])([^MCLZ]*)", path or ""):
        v = np.array(args.split(), float).reshape(-1, 2)
        if cmd == "M":
            cur = [v[0]]
        elif cmd == "Z":
            if np.linalg.norm(cur[-1] - cur[0]) > 1e-9:
                cur.append(cur[0])
            conts.append(np.vstack(cur))
        elif cmd == "C":
            p0 = cur[-1]
            n = max(1, math.ceil(np.linalg.norm(np.diff(np.vstack([p0, v]), axis=0), axis=1).sum() / STEP))
            cur.extend(bez(p0, *v, n))
        else:
            cur.append(v[0])
    return conts


def rdp(P, eps):
    keep = np.zeros(len(P), bool)
    keep[[0, -1]] = True
    stack = [(0, len(P) - 1)]
    while stack:
        a, b = stack.pop()
        if b <= a + 1:
            continue
        d = P[b] - P[a]
        L = np.hypot(*d)
        seg = P[a + 1:b] - P[a]
        dist = np.abs(d[0] * seg[:, 1] - d[1] * seg[:, 0]) / L if L > 1e-12 else np.hypot(seg[:, 0], seg[:, 1])
        i = int(np.argmax(dist))
        if dist[i] > eps:
            keep[a + 1 + i] = True
            stack += [(a, a + 1 + i), (a + 1 + i, b)]
    return P[keep]


def perim(c):
    return float(np.linalg.norm(np.diff(c, axis=0), axis=1).sum())


used = sorted({p["d"] for p in sh["placements"]})
designs, err = {}, 0.0
for i in used:
    d = ds[i]
    out = {"name": d["name"], "W": d["W"], "H": d["H"], "area": round(d["areaMm2"], 1)}
    for kind in ("kiss", "die"):
        cs = []
        for c in parse(d[kind]):
            s = rdp(c, EPS)
            err = max(err, abs(perim(s) - perim(c)))
            cs.append([round(float(v), 2) for v in s[:-1].ravel()])
        out[kind] = cs
    designs[i] = out

# Artwork cetak: garis potong (stroke 0 1 0 0 K / 1 0 0 0 K) diganti putih supaya tidak ikut tergambar.
doc = fitz.open(SRC / sc["pdf"])
pg = doc[SHEET]
for x in pg.get_contents():
    raw = doc.xref_stream(x)
    doc.update_stream(x, raw.replace(b"0 1 0 0 K", b"0 0 0 0 K").replace(b"1 0 0 0 K", b"0 0 0 0 K"))
zoom = 2400 / pg.rect.height
pix = doc[SHEET].get_pixmap(matrix=fitz.Matrix(zoom, zoom), alpha=False)
jpg = io.BytesIO()
Image.frombytes("RGB", (pix.width, pix.height), pix.samples).save(jpg, "JPEG", quality=85, optimize=True)

sim = {
    "scenario": SCEN, "sheet": SHEET + 1, "sheets": len(sc["sheets"]),
    "stickersAll": sum(len(s["placements"]) for s in sc["sheets"]),
    "W": sc["cw"], "H": sh["h"], "margin": sc["margin"], "gap": sc["gap"], "use": sh["use"],
    "ticket": sc["ticketLines"],
    "sources": sorted({ds[i]["source"] for i in used}),
    "designs": designs, "placements": [[p["d"], p["x"], p["y"], p["rot"]] for p in sh["placements"]],
    "tex": "data:image/jpeg;base64," + base64.b64encode(jpg.getvalue()).decode(),
}
(HERE / "sim-data.js").write_text("window.SIM = " + json.dumps(sim, separators=(",", ":")) + ";\n", encoding="utf-8")
print(f"{len(designs)} desain, {len(sim['placements'])} stiker, selisih keliling RDP maks {err:.3f} mm, "
      f"artwork {pix.width}x{pix.height}, sim-data.js {(HERE / 'sim-data.js').stat().st_size // 1024} KB")
