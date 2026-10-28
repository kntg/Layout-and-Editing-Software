"""Validasi garis potong (A) dan nesting (B) Printech dari hasil validasi.ps1.

Pemakaian: python validasi.py [folder hasil validasi.ps1]   (default: 03-exports/validasi)
Geometri artwork diambil dari file asli di 01-input, bukan dari Printech: PyMuPDF untuk PDF/AI, piksel untuk PNG/JPG
(aturan sama dengan Printech: artwork = alpha > 16, jadi gambar opak = seluruh gambar), bentuk SVG dari isi file. Garis potong dan
posisi stiker dari validasi.json; PDF export dicek ulang supaya yang dinilai sama dengan yang dikirim ke CorelDRAW.
Kriteria: 02-expected/validasi-garis-potong-susunan.md. Exit code 0 = semua lulus, 1 = ada yang gagal.
Laporan dan gambar bukti ditulis ke 04-evidence.
"""
import collections
import datetime
import functools
import json
import math
import pathlib
import re
import sys

import fitz  # PyMuPDF
import numpy as np
from PIL import Image, ImageDraw
from scipy.spatial import cKDTree

ROOT = pathlib.Path(__file__).parent
IN = ROOT / "01-input"
RUN = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "03-exports" / "validasi"
EVID = ROOT / "04-evidence"
DAY = datetime.date.today().isoformat()
PT = 25.4 / 72      # mm per point
STEP = 0.05         # jarak titik sampel garis (mm)
MAX_IN = 0.3        # aturan implementasi: garis tidak lebih dekat dari (offset - 0,3 mm) ke artwork
TOL = 0.15          # galat kurva Potrace dan mask 6 px/mm yang diterima di atas MAX_IN (keputusan D3, 2026-10-27)
SIMPLE = {"pdf-single-page.pdf", "pdf-multi-page.pdf p1", "pdf-multi-page.pdf p2", "ai-compatible.ai", "svg-vector.svg",
          "png-152dpi.png", "png-147dpi.png", "jpg-low-resolution.jpg", "pdf-large-a3.pdf", "pdf-oversize.pdf", "jpg-large-24mp.jpg"}
GROUP = {"sederhana": SIMPLE,
         "tepi tidak beraturan": {"png-rough-edge.png", "pdf-irregular-shapes.pdf p1", "pdf-multi-page.pdf p3", "pdf-irregular-shapes.pdf p3"},
         "berlubang": {"png-transparent.png", "pdf-irregular-shapes.pdf p2"},
         "elemen berdekatan": {"pdf-irregular-shapes.pdf p4"}}

report, failed = [], 0
sys.stdout.reconfigure(encoding="utf-8")


def out(s=""):
    report.append(s)
    print(s)


def verdict(tag, ok, text):
    global failed
    failed += not ok
    out(f"{tag} {'LULUS' if ok else 'GAGAL'}: {text}")


def f2(v):
    return f"{v:.2f}".replace(".", ",")


def f3(v):
    return f"{v:.3f}".replace(".", ",")


def pc(v):
    return f"{v:.1%}".replace(".", ",")


# ---------- geometri ----------

def bez(p0, c1, c2, p3, n):
    t = np.linspace(0, 1, n + 1)[1:, None]
    return (1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * c1 + 3 * (1 - t) * t ** 2 * c2 + t ** 3 * p3


@functools.lru_cache(maxsize=None)
def parse(path):
    """Path garis potong Printech (M, C, L, Z absolut, mm) -> (kontur tertutup N x 2, jumlah node, jumlah garis lurus)."""
    conts, cur, nodes, straight = [], [], 0, 0
    for cmd, args in re.findall(r"([MCLZ])([^MCLZ]*)", path):
        v = np.array(args.split(), float).reshape(-1, 2)
        if cmd == "M":
            cur = [v[0]]
        elif cmd == "Z":
            if np.linalg.norm(cur[-1] - cur[0]) > 1e-9:
                cur.append(cur[0])
            conts.append(np.vstack(cur))
        else:
            p0 = cur[-1]
            n = max(1, math.ceil(np.linalg.norm(np.diff(np.vstack([p0, v]), axis=0), axis=1).sum() / STEP))
            cur.extend(bez(p0, *v, n) if cmd == "C" else p0 + (v[0] - p0) * np.linspace(0, 1, n + 1)[1:, None])
            nodes += 1
            straight += cmd == "L"
    return tuple(conts), nodes, straight


def flatten(items, scale=1.0):
    """Item get_drawings PyMuPDF -> daftar subpath (N x 2), titik rapat tiap <= STEP mm setelah dikali scale."""
    subs, cur = [], None
    for it in items:
        if it[0] == "re":
            r = it[1]
            seq = [r.tl, r.tr, r.br, r.bl, r.tl]
        elif it[0] == "qu":
            q = it[1]
            seq = [q.ul, q.ur, q.lr, q.ll, q.ul]
        else:
            seq = list(it[1:])
        pts = np.array([tuple(p) for p in seq], float)
        if it[0] == "c":
            body = bez(*pts, max(1, math.ceil(np.linalg.norm(np.diff(pts, axis=0), axis=1).sum() * scale / STEP)))
        elif it[0] == "l":
            body = pts[0] + (pts[1] - pts[0]) * np.linspace(0, 1, max(1, math.ceil(np.linalg.norm(pts[1] - pts[0]) * scale / STEP)) + 1)[1:, None]
        else:
            body = pts[1:]
        if cur is None or np.linalg.norm(cur[-1] - pts[0]) > 1e-6:
            if cur is not None:
                subs.append(np.vstack(cur))
            cur = [pts[0]]
        cur.extend(body)
    if cur is not None:
        subs.append(np.vstack(cur))
    return [s * scale for s in subs]


def segs(conts):
    return np.vstack([c[:-1] for c in conts]), np.vstack([c[1:] for c in conts])


def tree(A, B, step=0.01):
    """KD-tree titik-titik segmen A-B yang dirapatkan tiap <= 0,01 mm (galat jarak <= 0,005 mm)."""
    n = np.maximum(1, np.ceil(np.linalg.norm(B - A, axis=1) / step).astype(int))
    i = np.repeat(np.arange(len(A)), n)
    t = (np.arange(n.sum()) - np.repeat(np.cumsum(n) - n, n)) / np.repeat(n, n)
    return cKDTree(np.r_[A[i] + (B - A)[i] * t[:, None], B])


def seg_dist(P, A, B):
    """Jarak terdekat setiap titik P ke kumpulan segmen A-B."""
    return tree(A, B).query(P)[0]


def inside(P, conts):
    """Titik P di dalam kontur (aturan even-odd)."""
    res = np.zeros(len(P), bool)
    for c in conts:
        a, b = c[:-1], c[1:]
        dy = np.where(b[:, 1] == a[:, 1], 1e-300, b[:, 1] - a[:, 1])
        for i in range(0, len(P), 2000):
            x, y = P[i:i + 2000, 0, None], P[i:i + 2000, 1, None]
            cross = ((a[:, 1] > y) != (b[:, 1] > y)) & (x < (b[:, 0] - a[:, 0]) * (y - a[:, 1]) / dy + a[:, 0])
            res[i:i + 2000] ^= cross.sum(1) % 2 == 1
    return res


def area(c):
    x, y = c[:, 0], c[:, 1]
    return abs(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1))) / 2


def rounded(w, h, r, n=90):
    pts = []
    for cx, cy, a in ((w - r, r, -90), (w - r, h - r, 0), (r, h - r, 90), (r, r, 180)):
        pts += [(cx + r * math.cos(math.radians(a + 90 * i / n)), cy + r * math.sin(math.radians(a + 90 * i / n))) for i in range(n + 1)]
    return np.array(pts + pts[:1])


@functools.lru_cache(maxsize=None)
def raster_edges(source):
    """Tepi piksel artwork (koordinat piksel) dan ukuran gambar. Aturan latar sama dengan Printech."""
    im = np.array(Image.open(IN / source).convert("RGBA"))
    fg = im[..., 3] > 16  # gambar opak: seluruh gambar (latar tidak dibuang otomatis)
    p = np.pad(fg, 1)
    ys, xs = np.nonzero(p[1:-1, 1:] != p[1:-1, :-1])
    ys2, xs2 = np.nonzero(p[1:, 1:-1] != p[:-1, 1:-1])
    A = np.r_[np.c_[xs, ys], np.c_[xs2, ys2]].astype(float)
    B = np.r_[np.c_[xs, ys + 1], np.c_[xs2 + 1, ys2]].astype(float)
    return A, B, fg.shape[1], fg.shape[0]


@functools.lru_cache(maxsize=None)
def pdf_art(source, page):
    """Subpath artwork vektor halaman PDF asli (point) dan ukuran halaman (point)."""
    pg = fitz.open(IN / source)[page]
    subs = []
    for dr in pg.get_drawings():
        s = flatten(dr["items"], PT)
        if dr.get("closePath") and s:
            s[-1] = np.vstack([s[-1], s[-1][:1]])
        subs += [x / PT for x in s]
    return subs, pg.rect.width, pg.rect.height


def art(d):
    """Segmen batas artwork asli dalam koordinat kotak stiker (mm)."""
    fx, fy, fw, fh = d["full"]
    ext = pathlib.Path(d["source"]).suffix.lower()
    if ext in (".pdf", ".ai"):
        subs, W, H = pdf_art(d["source"], d["page"])
        return segs([s * [fw / W, fh / H] + [fx, fy] for s in subs])
    if ext == ".svg":  # svg-vector.svg: persegi panjang 100 x 50, rx 8, memenuhi seluruh gambar
        return segs([rounded(100, 50, 8) * [fw / 100, fh / 50] + [fx, fy]])
    A, B, w, h = raster_edges(d["source"])
    k = np.array([fw / w, fh / h])
    return A * k + [fx, fy], B * k + [fx, fy]


def place(P, p, d):
    """Titik kotak stiker -> lembar, sama dengan Nest.Xf / Export.Place: putar searah jarum jam, lalu geser."""
    c, s = {0: (1, 0), 90: (0, 1), 180: (-1, 0), 270: (0, -1)}[p["rot"]]
    tx, ty = {0: (0, 0), 90: (d["H"], 0), 180: (d["W"], d["H"]), 270: (0, d["W"])}[p["rot"]]
    return np.c_[P[:, 0] * c - P[:, 1] * s + p["x"] + tx, P[:, 0] * s + P[:, 1] * c + p["y"] + ty]


def bbox(P):
    return np.r_[P.min(0), P.max(0)]


# ---------- A. garis potong ----------

def measure(d):
    """Jarak garis kiss/die ke artwork asli, node, panjang, dan apakah die membungkus kiss."""
    A, B = art(d)
    res = {}
    for kind in ("kiss", "die"):
        if not d[kind]:
            continue
        conts, nodes, straight = parse(d[kind])
        P = np.vstack(conts)
        dist = seg_dist(P, A, B)
        res[kind] = dict(conts=conts, n=len(conts), nodes=nodes, straight=straight, off=d[kind + "Cut"][1], sm=d[kind + "Cut"][2],
                         dmin=dist.min(), p50=float(np.median(dist)), p95=float(np.percentile(dist, 95)), dmax=dist.max(),
                         length=sum(np.linalg.norm(np.diff(c, axis=0), axis=1).sum() for c in conts), P=P)
    if "kiss" in res and "die" in res:
        k, e = res["kiss"], res["die"]
        gap = seg_dist(k["P"], *segs(e["conts"]))
        # Garis kiss yang keluar dari die pasti melewati garis die, jadi cukup cek titik awal tiap kontur kiss dan titik
        # yang dekat (< 0,05 mm) dengan garis die.
        idx = np.r_[np.cumsum([0] + [len(c) for c in k["conts"][:-1]]), np.nonzero(gap < 0.05)[0]]
        out_ = ~inside(k["P"][idx], e["conts"]) & (gap[idx] > 1e-3)
        res["wrap"] = dict(outside=int(out_.sum()), gap=gap.min())
    return res


def hole_left(d, kind):
    """Jari-jari lubang asli dikurangi (offset + halus). Lubang die ikut tertutup bila lubang kiss tertutup (die membungkus
    kiss). None = desain tanpa lubang asli."""
    _, _, r = hole_center(d)
    if r is None:
        return None
    cuts = [d["dieCut"]] + ([d["kissCut"]] if d["kissCut"][0] else []) if kind == "die" else [d["kissCut"]]
    return min(r - (c[1] + c[2]) for c in cuts)


def expected_parts(name, kind, d):
    """Jumlah kontur yang benar menurut aturan offset + halus: bagian berjarak < 2 x (offset + halus) menyatu, dan lubang
    asli yang tetap terbuka mendapat garis dalam (donat, sejak 2026-10-21).
    None = celah/lubang terlalu dekat ke ambang (selisih < 0,5 mm), tidak dinilai."""
    cut, left = d[kind + "Cut"], hole_left(d, kind)
    if left is not None:
        return None if abs(left) < 0.5 else 2 if left > 0 else 1
    if not name.endswith("shapes.pdf p4"):
        return 1
    t = 2 * (cut[1] + cut[2])
    gaps = [4, 15]  # celah antar lingkaran di make-testdata.py
    return None if any(abs(g - t) < 0.5 for g in gaps) else 1 + sum(g > t for g in gaps)


def hole_center(d):
    """Titik tengah lubang (koordinat kotak) untuk desain berlubang, lebar celah yang menghubungkannya ke luar, dan
    jari-jari lubang asli (mm) bila lubangnya tertutup penuh oleh artwork."""
    fx, fy, fw, fh = d["full"]
    if d["name"] == "pdf-irregular-shapes.pdf p2":
        return np.array([[fx + fw / 2, fy + fh / 2]]), 5.0, None  # cincin C, celah 5 mm: rongganya bukan lubang asli
    if d["name"] == "png-transparent.png":
        return np.array([[fx + fw * 500 / 1000, fy + fh * 300 / 600]]), None, fw * 80 / 1000  # lubang asli Ø 160 px
    return None, None, None


@functools.lru_cache(maxsize=None)
def box_source(source, page):
    """Desain kotak (masukan 2026-10-23): artwork (alpha > 16, aturan Printech) memenuhi kotaknya, yaitu semua piksel tepi
    terisi dan isinya >= 99,5 %. PDF/AI dirender PyMuPDF dengan latar transparan pada 6 px/mm (resolusi mask Printech)."""
    ext = pathlib.Path(source).suffix.lower()
    if ext == ".svg":
        return False  # svg-vector.svg: persegi panjang bersudut bulat (rx 8)
    if ext in (".pdf", ".ai"):
        pix = fitz.open(IN / source)[page].get_pixmap(matrix=fitz.Matrix(6 * PT, 6 * PT), alpha=True)
        fg = np.frombuffer(pix.samples, np.uint8).reshape(pix.h, pix.w, pix.n)[..., -1] > 16
    else:
        fg = np.array(Image.open(IN / source).convert("RGBA"))[..., 3] > 16
    ys, xs = np.nonzero(fg)
    if not len(xs):
        return False
    f = fg[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    return bool(f[0].all() and f[-1].all() and f[:, 0].all() and f[:, -1].all() and f.mean() >= 0.995)


def lim_max(name, x):
    """Batas jarak terjauh garis ke artwork: offset + 0,5. Desain kotak: sudut garis kotak berjarak offset x akar 2."""
    return x["off"] * (math.sqrt(2) if name in BOXES else 1) + 0.5


data = json.loads((RUN / "validasi.json").read_text(encoding="utf-8-sig"))
SC = {s["name"]: s for s in data}
BOXES = {d["name"] for sc in data if sc["name"].startswith("A-") for d in sc.get("designs", []) if box_source(d["source"], d["page"])}
out(f"Validasi garis potong, nesting, dan export Printech, {DAY}. Data: {RUN}")
out("Kriteria: 02-expected/validasi-garis-potong-susunan.md. Jarak dalam mm.")
out()
out("A. GARIS POTONG")
M = {}  # (skenario, nama desain) -> metrik
rows = []
for sc in data:
    if not sc["name"].startswith("A-") or "designs" not in sc:
        continue
    for d in sc["designs"]:
        m = M[sc["name"], d["name"]] = measure(d)
        for kind in ("kiss", "die"):
            if kind in m:
                x = m[kind]
                rows.append((sc["name"], d["name"], kind, x))
out(f"{'skenario':15} {'desain':30} {'garis':5} {'offset':>6} {'halus':>5} {'kontur':>6} {'node':>5} {'min':>6} {'median':>6} {'p95':>6} {'maks':>6}")
for scn, name, kind, x in rows:
    out(f"{scn:15} {name:30} {kind:5} {f2(x['off']):>6} {f2(x['sm']):>5} {x['n']:>6} {x['nodes']:>5} {f2(x['dmin']):>6} {f2(x['p50']):>6} {f2(x['p95']):>6} {f2(x['dmax']):>6}")
out()

# A1 offset sesuai nilai yang dipilih (bentuk sederhana)
bad = [(s, n, k, x) for s, n, k, x in rows if n in SIMPLE and s in ("A-standar", "A-offset-kecil", "A-offset-besar")
       and not (abs(x["p50"] - x["off"]) <= 0.25 and x["dmax"] <= lim_max(n, x))]
sel = [(n, x) for s, n, k, x in rows if n in SIMPLE and s in ("A-standar", "A-offset-kecil", "A-offset-besar")]
dev = [x["p50"] - x["off"] for n, x in sel]
verdict("A1", not bad, f"offset sesuai nilai pada {len(sel)} garis bentuk sederhana (offset 1/2/3 kiss, 3/4/6 die): median - offset "
        f"{f2(min(dev))} s.d. {f2(max(dev))} (batas ±0,25), maks - harapan terbesar {f2(max(x['dmax'] - lim_max(n, x) + 0.5 for n, x in sel))} "
        "(batas 0,50; harapan = offset, desain kotak: offset x akar 2 di sudut)"
        + "".join(f"; {s} {n} {k}: median {f2(x['p50'])}, maks {f2(x['dmax'])}" for s, n, k, x in bad))

# A2 Halus memengaruhi karakter kontur
hal = [0, 0.5, 1, 2]
msgs, ok, red = [], True, None
for name in ("png-rough-edge.png", "pdf-irregular-shapes.pdf p1"):
    for kind in ("kiss", "die"):
        xs = [M[f"A-halus-{h:g}", name][kind] for h in hal]
        length, nodes, dmax = [x["length"] for x in xs], [x["nodes"] for x in xs], [x["dmax"] for x in xs]
        good = all(a > b for a, b in zip(length, length[1:])) and nodes[2] < nodes[0] and nodes[3] < nodes[0]
        if name == "png-rough-edge.png" and kind == "kiss":
            red = 1 - nodes[2] / nodes[0]
        ok &= good
        msgs.append(f"{name} {kind} halus 0/0,5/1/2: node {'/'.join(map(str, nodes))}, panjang {'/'.join(f2(v) for v in length)}, "
                    f"jarak maks {'/'.join(f2(v) for v in dmax)}{'' if good else ' <- tidak sesuai'}")
verdict("A2a", ok, "Halus mengubah kontur: makin besar Halus, garis makin pendek (lekukan kecil ditutup), node berkurang. " + "; ".join(msgs))
# Target turun >= 60% hanya untuk logo kasar seperti rough.jpg (selfcheck S1); logo bergelombang ini dicatat saja (keputusan D5, 2026-10-27).
out(f"A2b INFO: node kiss logo kasar bergelombang turun {red:.0%} dari Halus 0 ke Halus 1 (target >= 60% hanya untuk rough.jpg di selfcheck S1)")

# A3 die membungkus kiss
wr = [(s, n, M[s, n]["wrap"]) for s, n in M if "wrap" in M[s, n]]
badw = [(s, n, w) for s, n, w in wr if w["outside"] > 0]
verdict("A3", not badw, f"die membungkus kiss pada {len(wr)} desain x pengaturan (termasuk kiss halus 3 + die halus 0): titik kiss di luar die "
        f"{sum(w['outside'] for _, _, w in wr)}, jarak kiss-die terkecil {f2(min(w['gap'] for _, _, w in wr))}"
        + "".join(f"; {s} {n}: {w['outside']} titik di luar" for s, n, w in badw))

# A4 jarak minimum ke artwork >= offset - 0,3, dengan toleransi galat kurva TOL. Garis di bawah offset - 0,3 tetap dilaporkan.
worst = sorted(rows, key=lambda r: r[3]["dmin"] - (r[3]["off"] - MAX_IN))
badm = [r for r in rows if r[3]["dmin"] < r[3]["off"] - MAX_IN - TOL]
near = [r for r in rows if r[3]["dmin"] < r[3]["off"] - MAX_IN]
s0, n0, k0, x0 = worst[0]
verdict("A4", not badm, f"jarak garis ke artwork tidak kurang dari offset - 0,3 mm, toleransi galat kurva 0,15 mm, pada {len(rows)} garis. "
        f"Paling dekat: {s0} {n0} {k0} {f2(x0['dmin'])} mm (offset {f2(x0['off'])}, batas {f2(x0['off'] - MAX_IN)}, dengan toleransi {f2(x0['off'] - MAX_IN - TOL)}); "
        f"{len(near)} garis di bawah offset - 0,3 mm, terdalam {f2(max([x['off'] - MAX_IN - x['dmin'] for _, _, _, x in near], default=0))} mm"
        + (f"; {len(badm)} garis di bawah batas toleransi: " + ", ".join(f"{s} {n} {k} {f2(x['dmin'])}/{f2(x['off'] - MAX_IN - TOL)}" for s, n, k, x in badm[:12]) if badm else ""))

# A5 reduksi node tidak mengubah bentuk: tanpa sudut/garis lurus, jumlah kontur dan lubang benar, bentuk sederhana dalam pita offset.
# Desain kotak memang 4 garis lurus (masukan 2026-10-23); garisnya dinilai di A7.
straight = sum(x["straight"] for s, n, k, x in rows if n not in BOXES)
cont_bad, hole_bad, hole_txt = [], [], []
for (s, n), m in M.items():
    d = next(d for d in SC[s]["designs"] if d["name"] == n)
    for kind in ("kiss", "die"):
        if kind not in m:
            continue
        e = expected_parts(n, kind, d)
        if e is not None and m[kind]["n"] != e:
            cont_bad.append(f"{s} {n} {kind}: {m[kind]['n']} kontur, harapan {e}")
        c, gapw, hole_r = hole_center(d)
        if c is not None:
            t = 2 * (m[kind]["off"] + m[kind]["sm"])
            if hole_r is not None:  # lubang asli: tertutup (tengahnya di dalam garis) bila jari-jari < offset + halus
                left = hole_left(d, kind)
                want = True if left < -0.5 else False if left > 0.5 else None
            else:
                want = True if gapw < t - 0.5 else False if gapw > t + 0.5 else None
            got = bool(inside(c, m[kind]["conts"])[0])
            if want is not None and got != want:
                hole_bad.append(f"{s} {n} {kind}")
            if s == "A-standar" or want is False:
                hole_txt.append(f"{s} {n} {kind}: lubang {'tidak dipotong' if got else 'dipotong, ada garis dalam' if hole_r else 'ikut dikelilingi garis (celah terbuka)'}")
band = [(s, n, k, x) for s, n, k, x in rows if n in SIMPLE and not (x["off"] - MAX_IN - TOL <= x["dmin"] and x["dmax"] <= lim_max(n, x))]
verdict("A5", straight == 0 and not cont_bad and not hole_bad and not band,
        f"bentuk tetap benar setelah reduksi node: {straight} garis lurus/sudut di {sum(n not in BOXES for _, n, _, _ in rows)} garis bukan kotak; "
        f"jumlah kontur salah {len(cont_bad)}; lubang salah {len(hole_bad)}; "
        f"bentuk sederhana keluar pita [offset - 0,3 - toleransi 0,15; offset + 0,5, kotak: offset x akar 2 + 0,5]: {len(band)} (batas bawah pita = A4"
        + (": " + ", ".join(f"{s} {n} {k} {f2(x['dmin'])}-{f2(x['dmax'])}" for s, n, k, x in band) if band else "") + "). "
        + "; ".join(cont_bad + hole_bad + hole_txt))

# A6 opsi kiss/die sesuai pilihan operator (data + warna garis di PDF export)
def strokes(pdf):
    res = {"kiss": [], "die": [], "lain": []}
    for i, pg in enumerate(fitz.open(pdf)):
        for dr in pg.get_drawings():
            if dr["type"] != "s":
                continue
            col = dr["color"] or (0, 0, 0)
            kind = "kiss" if col[0] > 0.5 and col[1] < 0.5 and col[2] > 0.3 else "die" if col[0] < 0.3 and col[2] > 0.5 else "lain"
            res[kind].append((i, flatten(dr["items"], PT)))
    return res

opt = []
for s, want in (("A-standar", (1, 1)), ("A-hanya-kiss", (1, 0)), ("A-hanya-die", (0, 1))):
    sc = SC[s]
    n = sum(len(sh["placements"]) for sh in sc["sheets"])
    st = strokes(RUN / sc["pdf"])
    data_ok = all(bool(d["kiss"]) == bool(want[0]) and bool(d["die"]) == bool(want[1]) for d in sc["designs"])
    opt.append((s, data_ok and len(st["kiss"]) == n * want[0] and len(st["die"]) == n * want[1] and not st["lain"],
                f"{s}: {n} stiker, garis magenta {len(st['kiss'])}, cyan {len(st['die'])}, warna lain {len(st['lain'])}"))
none = SC["A-tanpa-garis"]["error"]
opt.append(("A-tanpa-garis", none != "tanpa error", f"keduanya mati: '{none}'"))
verdict("A6", all(o[1] for o in opt), "garis sesuai pilihan: " + "; ".join(o[2] for o in opt))

# A7 desain kotak (masukan 2026-10-23): garis = kotak artwork ± offset, 4 garis lurus, Halus tidak dipakai. Die minimal
# 1 piksel (1/6 mm) di luar kiss.
box_err, box_bad = [], []
for (s, n), m in M.items():
    if n not in BOXES:
        continue
    d = next(d for d in SC[s]["designs"] if d["name"] == n)
    ab = bbox(np.r_[art(d)])
    for kind in ("kiss", "die"):
        if kind not in m:
            continue
        x = m[kind]
        off = x["off"] if kind == "kiss" or not d["kissCut"][0] else max(x["off"], d["kissCut"][1] + 1 / 6)
        err = float(np.abs(bbox(x["P"]) - (ab + [-off, -off, off, off])).max())
        box_err.append(err)
        if not (x["n"] == 1 and x["nodes"] == x["straight"] == 3 and err <= 0.15):
            box_bad.append(f"{s} {n} {kind}: {x['n']} kontur, {x['nodes']} segmen ({x['straight']} lurus), selisih {f2(err)}")
verdict("A7", bool(box_err) and not box_bad, f"desain kotak ({', '.join(sorted(BOXES))}): garis = kotak artwork ± offset dengan 4 garis "
        f"lurus pada {len(box_err)} garis (termasuk Halus 0-2), selisih terbesar {f2(max(box_err, default=0))} mm (batas 0,15)"
        + "".join("; " + b for b in box_bad))
out()


# ---------- ekspor = data yang dinilai ----------

def export_check(sc):
    """Garis di PDF export sama dengan garis validasi.json setelah posisi/rotasi; artwork di tempat yang sama."""
    ds = {d["id"]: d for d in sc["designs"]}
    doc = fitz.open(RUN / sc["pdf"])
    st = strokes(RUN / sc["pdf"])
    worst, count_bad = 0.0, []
    for i, sh in enumerate(sc["sheets"]):
        for kind in ("kiss", "die"):
            want = [bbox(np.vstack([place(c, p, ds[p["d"]]) for c in parse(ds[p["d"]][kind])[0]])) for p in sh["placements"] if ds[p["d"]][kind]]
            got = [bbox(np.vstack(s)) for pg, s in st[kind] if pg == i]
            if len(want) != len(got):
                count_bad.append(f"lembar {i + 1} {kind}: {len(got)} garis, harapan {len(want)}")
                continue
            if want:
                G = np.array(got)
                worst = max(worst, max(np.abs(G - w).max(1).min() for w in want))
        # artwork: gambar (PNG/JPG/SVG) dan path vektor (PDF/AI)
        page = doc[i]
        imgs = [np.r_[im["bbox"][:2], im["bbox"][2:]] * PT for im in page.get_image_info()]
        fills = [bbox(np.vstack(flatten(dr["items"], PT))) for dr in page.get_drawings() if dr["type"] in ("f", "fs")]
        wi, wf = [], []
        for p in sh["placements"]:
            d = ds[p["d"]]
            if d["vector"]:
                subs, W, H = pdf_art(d["source"], d["page"])
                fx, fy, fw, fh = d["full"]
                for s in subs:
                    wf.append(bbox(place(s * [fw / W, fh / H] + [fx, fy], p, d)))
            else:
                x, y, w, h = d["bleedRect"] if d["bleedImage"] else d["full"]
                wi.append(bbox(place(np.array([[x, y], [x + w, y + h]]), p, d)))
        for name, want, got in (("gambar", wi, imgs), ("path artwork", wf, fills)):
            if len(want) != len(got):
                count_bad.append(f"lembar {i + 1} {name}: {len(got)}, harapan {len(want)}")
            elif want:
                G = np.array(got)
                worst = max(worst, max(np.abs(G - w).max(1).min() for w in want))
    return worst, count_bad


out("E. PDF EXPORT = DATA YANG DINILAI")
ex = {s["name"]: export_check(s) for s in data if "designs" in s}
eb = {k: v for k, v in ex.items() if v[1] or v[0] > 0.05}
verdict("E1", not eb, f"{len(ex)} PDF export: garis potong dan artwork berada tepat di posisi/rotasi data (selisih kotak terbesar "
        f"{f3(max(v[0] for v in ex.values()))} mm, batas 0,05); jumlah garis/gambar/path sesuai"
        + "".join(f"; {k}: {', '.join(v[1]) or f'selisih {f3(v[0])}'}" for k, v in eb.items()))
out()

# ---------- B. nesting ----------

out("B. NESTING")


def outer(d):
    return parse(d["die"] or d["kiss"])[0]


def fits(d, sc):
    b = bbox(np.vstack(outer(d)))
    w, h = b[2] - b[0], b[3] - b[1]
    fw, fh = sc["cw"] - 2 * sc["margin"], sc["ch"] - 2 * sc["margin"]
    return (w <= fw and h <= fh) or (h <= fw and w <= fh)


def sheet_geo(sc, sh):
    ds = {d["id"]: d for d in sc["designs"]}
    return [(p, ds[p["d"]], [place(c, p, ds[p["d"]]) for c in outer(ds[p["d"]])]) for p in sh["placements"]]


def pair_dist(ca, cb, tb):
    """Jarak terdekat dua stiker (kumpulan kontur, tb = KD-tree kontur b); 0 bila salah satu di dalam yang lain."""
    if inside(np.array([c[0] for c in ca]), cb).any() or inside(np.array([c[0] for c in cb]), ca).any():
        return 0.0
    return tb.query(np.vstack(ca))[0].min()


summary = {}
for sc in data:
    if not sc["name"].startswith("B-"):
        continue
    ds = sc["designs"]
    placed = {d["id"]: 0 for d in ds}
    for sh in sc["sheets"]:
        for p in sh["placements"]:
            placed[p["d"]] += 1
    big = sum(d["copies"] for d in ds if not fits(d, sc))
    count_bad = [f"{d['name']}: {placed[d['id']]} dari {d['copies']}" for d in ds if placed[d["id"]] != (d["copies"] if fits(d, sc) else 0)]
    pairs, near, coll, gaps_bad, nn, margin_min, margin_bad, rots, use_rows = 0, 0, 0, [], [], 1e9, [], {0: 0, 90: 0, 180: 0, 270: 0}, []
    fill_area = (sc["cw"] - 2 * sc["margin"]) * (sc["ch"] - 2 * sc["margin"])
    for i, sh in enumerate(sc["sheets"]):
        geo = sheet_geo(sc, sh)
        boxes = [bbox(np.vstack(c)) for _, _, c in geo]
        trees = [tree(*segs(c)) for _, _, c in geo]
        best = [1e9] * len(geo)
        for a in range(len(geo)):
            for b in range(a + 1, len(geo)):
                ba, bb = boxes[a], boxes[b]
                if ba[0] > bb[2] + 5 or bb[0] > ba[2] + 5 or ba[1] > bb[3] + 5 or bb[1] > ba[3] + 5:
                    continue
                dd = pair_dist(geo[a][2], geo[b][2], trees[b])
                pairs += 1
                near += dd < 5
                best[a], best[b] = min(best[a], dd), min(best[b], dd)
                if dd <= 1e-3:
                    coll += 1
                if dd < sc["gap"]:
                    gaps_bad.append(f"lembar {i + 1}: {geo[a][1]['name']} - {geo[b][1]['name']} {f2(dd)}")
        nn += [v for v in best if v < 1e9]
        area_sum = 0.0
        for p, d, conts in geo:
            rots[p["rot"]] += 1
            P = np.vstack(conts)
            m = min(P[:, 0].min(), P[:, 1].min(), sc["cw"] - P[:, 0].max(), sh["h"] - P[:, 1].max())
            need = sc["margin"]
            if d["bleedImage"]:  # warna bleed melebar dari garis kiss (tanpa kiss: die) sejauh bleed
                K = np.vstack([place(c, p, d) for c in parse(d["kiss"] or d["die"])[0]])
                m2 = min(K[:, 0].min(), K[:, 1].min(), sc["cw"] - K[:, 0].max(), sh["h"] - K[:, 1].max()) - d["bleed"]
                m = min(m, m2)
            margin_min = min(margin_min, m)
            if m < need - 1e-6:
                margin_bad.append(f"lembar {i + 1} {d['name']} {f2(m)}")
            area_sum += sum(area(c) for c in conts)
        use_rows.append((i + 1, len(geo), area_sum / ((sc["cw"] - 2 * sc["margin"]) * (sh["h"] - 2 * sc["margin"])), sh["use"], sh["bad"]))
    total = sum(placed.values())
    want_sheets = math.ceil(sum(sum(area(c) for c in outer(d)) * placed[d["id"]] for d in ds) / fill_area - 1e-9)
    summary[sc["name"]] = dict(total=total, sheets=len(sc["sheets"]), big=big, pairs=pairs, near=near, coll=coll, gaps_bad=gaps_bad,
                               nn=nn, margin_min=margin_min, margin_bad=margin_bad, rots=rots, use=use_rows, count_bad=count_bad,
                               want_sheets=want_sheets, last=sc.get("lastSheet"), tooBig=sc["tooBig"], requested=sum(d["copies"] for d in ds))
    nnt = f"min {f2(min(nn))}, median {f2(float(np.median(nn)))}, maks {f2(max(nn))}" if nn else "- (tidak ada tetangga < 5 mm)"
    out(f"{sc['name']}: {total} stiker di {len(sc['sheets'])} lembar (diminta {sum(d['copies'] for d in ds)}, tidak muat {sc['tooBig']}); "
        f"pasangan dicek {pairs}, berdekatan < 5 mm {near}; jarak ke tetangga terdekat {nnt}; margin terkecil {f2(margin_min)}; "
        f"rotasi 0/90/180/270: {'/'.join(str(rots[r]) for r in (0, 90, 180, 270))}")
    for n, c, ui, pr, badc in use_rows:
        out(f"   lembar {n}: {c} pcs, pemakaian bahan dihitung ulang {pc(ui)}, Printech {pc(pr)}, ditandai merah oleh Printech {badc}")
out()

S = summary
verdict("B1", all(not v["count_bad"] and v["total"] + v["tooBig"] == v["requested"] and v["tooBig"] == v["big"] for v in S.values()),
        "jumlah stiker: " + "; ".join(f"{k} {v['total']} + tidak muat {v['tooBig']} = diminta {v['requested']}"
                                      + (f" ({', '.join(v['count_bad'])})" if v["count_bad"] else "") for k, v in S.items()))
verdict("B2", all(v["coll"] == 0 for v in S.values()), f"tabrakan: {sum(v['coll'] for v in S.values())} dari {sum(v['pairs'] for v in S.values())} "
        f"pasangan stiker yang dicek ({sum(v['near'] for v in S.values())} pasangan berjarak < 5 mm)")
allnn = np.array(sum((v["nn"] for v in S.values()), []))
verdict("B3", all(not v["gaps_bad"] for v in S.values()), f"jarak antar garis potong terluar >= 2 mm: terkecil {f2(allnn.min())}, median jarak ke "
        f"tetangga terdekat {f2(float(np.median(allnn)))}, maks {f2(allnn.max())}" + "".join(f"; {g}" for v in S.values() for g in v["gaps_bad"][:10]))
verdict("B4", all(not v["margin_bad"] for v in S.values()), f"margin 10 mm (stiker dengan bleed: garis + bleed): terkecil "
        f"{f2(min(v['margin_min'] for v in S.values()))}" + "".join(f"; {g}" for v in S.values() for g in v["margin_bad"][:10]))

rot_txt, rot_ok = [], True
rsc = SC["B-rotasi"]
st = strokes(RUN / rsc["pdf"])
doc = fitz.open(RUN / rsc["pdf"])
pg = doc[0]
images = pg.get_image_info()
fills = [flatten(dr["items"], PT) for dr in pg.get_drawings() if dr["type"] in ("f", "fs")]
ds = {d["id"]: d for d in rsc["designs"]}
by = {}
for p in rsc["sheets"][0]["placements"]:
    d = ds[p["d"]]
    box = bbox(place(np.array([[0, 0], [d["W"], d["H"]]]), p, d))
    inbox = lambda P: box[0] - 1 <= P[:, 0].mean() <= box[2] + 1 and box[1] - 1 <= P[:, 1].mean() <= box[3] + 1
    K = np.vstack([np.vstack(s) for pgi, s in st["kiss"] if inbox(np.vstack(s))])
    if d["vector"]:
        A, B = segs([s for f in fills for s in f if inbox(np.vstack(f))])
    else:
        im = next(im for im in images if inbox(np.array([im["bbox"][:2], im["bbox"][2:]]) * PT))
        a, b, c, dd, e, f = im["transform"]
        Ae, Be, w, h = raster_edges(d["source"])
        fx, fy, fw, fh = d["full"]
        rx, ry, rw, rh = d["bleedRect"] if d["bleedImage"] else d["full"]

        def to_page(Q):
            U, V = ((Q * [fw / w, fh / h] + [fx, fy]) - [rx, ry]).T / np.array([[rw], [rh]])
            return np.c_[a * U + c * V + e, b * U + dd * V + f] * PT
        A, B = to_page(Ae), to_page(Be)
    dist = seg_dist(K, A, B)
    ref = M["A-standar", d["name"]]["kiss"] if ("A-standar", d["name"]) in M else None
    by.setdefault(d["name"], []).append((p["rot"], dist.min(), float(np.median(dist)), ref))
for name, v in by.items():
    mins, meds = [x[1] for x in v], [x[2] for x in v]
    spread = max(max(mins) - min(mins), max(meds) - min(meds))
    ref = v[0][3]
    dref = abs(np.median(meds) - ref["p50"]) if ref else 0
    good = len({x[0] for x in v}) == 4 and spread <= 0.02 and dref <= 0.02
    rot_ok &= good
    rot_txt.append(f"{name} rotasi {'/'.join(str(x[0]) for x in v)}: jarak garis kiss ke artwork di PDF min {'/'.join(f2(x) for x in mins)}, "
                   f"median {'/'.join(f2(x) for x in meds)} (selisih antar rotasi {f3(spread)}; vs tanpa rotasi {f3(dref)})")
used = {k: sum(S[s]["rots"][k] for s in S if s != "B-rotasi") for k in (0, 90, 180, 270)}
verdict("B5", rot_ok and rsc["sheets"][0]["bad"] == 0 and not ex["B-rotasi"][1],
        "4 arah rotasi dipakai tanpa merusak geometri (diukur dari PDF export): " + "; ".join(rot_txt)
        + f". Susun otomatis memakai rotasi 0/90/180/270: {'/'.join(str(used[k]) for k in (0, 90, 180, 270))}")

multi = {k: v for k, v in S.items() if v["sheets"] > 1}
verdict("B6", all(v["sheets"] >= v["want_sheets"] and v["last"] and v["last"]["movable"] < v["last"]["stickers"] for v in multi.values())
        and all(r[1] > 0 for v in S.values() for r in v["use"]),
        "lembar tambahan hanya bila perlu: " + "; ".join(f"{k} {v['sheets']} lembar (batas bawah luas {v['want_sheets']}), lembar terakhir "
                                                        f"{v['last']['stickers']} stiker, yang masih muat di lembar sebelumnya {v['last']['movable']}"
                                                        for k, v in multi.items()) + "; tidak ada lembar kosong")
ub = [(k, r) for k, v in S.items() for r in v["use"] if abs(r[2] - r[3]) > 0.005 or r[4] != 0]
verdict("B7", not ub, "pemakaian bahan Printech = luas di dalam garis potong terluar / area cetak, dihitung ulang dari garis: selisih terbesar "
        f"{f2(max(abs(r[2] - r[3]) for v in S.values() for r in v['use']) * 100)} poin persen (batas 0,5); jumlah pcs per lembar = jumlah garis di PDF"
        + "".join(f"; {k} lembar {r[0]}: {pc(r[2])} vs {pc(r[3])}, merah {r[4]}" for k, r in ub))
ui = S.get("B-ui")
if ui:
    tot = sum(r[3] for r in ui["use"]) / len(ui["use"])  # angka Printech (yang tampil di layar); lembar B-ui sama besar
    out(f"Angka untuk dicocokkan dengan layar (uji UI, semua file 01-input): status '{ui['total']} stiker di {ui['sheets']} lembar.', "
        f"tidak muat {ui['tooBig']}, pemakaian bahan total {tot:.0%}, per lembar "
        + ", ".join(f"{r[1]} pcs {r[3]:.0%}" for r in ui["use"]))
out()


# ---------- X. hasil export ----------

out("X. HASIL EXPORT (dicek dari file, bukan dari tampilan canvas)")
NUM = rb"[-+]?(?:\d+\.\d*|\.\d+|\d+)"
KISS_K, DIE_K = ("K", (0.0, 1.0, 0.0, 0.0)), ("K", (1.0, 0.0, 0.0, 0.0))
SCN = [s for s in data if "designs" in s]


def stroke_ops(raw):
    """Preflight content stream: warna stroke aktif di setiap operator stroke, dan semua operator yang dipakai."""
    stack, cur, nums, res, ops = [], None, [], [], set()
    for t in re.findall(rb"\((?:\\.|[^\\)])*\)|/[^\s/\[\]()<>{}]+|" + NUM + rb"|[A-Za-z*'\"]+", raw):
        if re.fullmatch(NUM, t):
            nums.append(float(t))
            continue
        if t[:1] in (b"/", b"("):
            nums = []
            continue
        op = t.decode("latin-1")
        ops.add(op)
        if op == "q":
            stack.append(cur)
        elif op == "Q":
            cur = stack.pop() if stack else None
        elif op in ("K", "RG", "G", "SC", "SCN"):
            cur = (op, tuple(round(v, 4) for v in nums))
        elif op in ("S", "s", "B", "B*", "b", "b*"):
            res.append(cur)
        nums = []
    return res, ops


def fills_k(raw):
    return {tuple(round(float(g), 3) for g in m.groups()) for m in re.finditer(rb"(" + NUM + rb")\s+(" + NUM + rb")\s+(" + NUM + rb")\s+(" + NUM + rb")\s+k\b", raw)}


def matched(want, got):
    """Pasangkan setiap kotak harapan dengan kotak export terdekat: (harapan, kotak export)."""
    G = np.array(got)
    return [(w, G[np.abs(G - w).max(1).argmin()]) for w in want] if got else []


# X1 PDF produksi: halaman, ukuran halaman, ukuran desain dibanding file asli
x1, x1n = [], 0
for sc in SCN:
    doc = fitz.open(RUN / sc["pdf"])
    ds = {d["id"]: d for d in sc["designs"]}
    if len(doc) != len(sc["sheets"]):
        x1.append(f"{sc['name']}: {len(doc)} halaman, harapan {len(sc['sheets'])}")
    for i, sh in enumerate(sc["sheets"]):
        pg = doc[i]
        if abs(pg.rect.width * PT - sc["cw"]) > 0.01 or abs(pg.rect.height * PT - sh["h"]) > 0.01:
            x1.append(f"{sc['name']} lembar {i + 1}: {f2(pg.rect.width * PT)} x {f2(pg.rect.height * PT)} mm, harapan {f2(sc['cw'])} x {f2(sh['h'])}")
        fills = [bbox(np.vstack(flatten(dr["items"], PT))) for dr in pg.get_drawings() if dr["type"] in ("f", "fs")]
        imgs = [np.r_[im["bbox"][:2], im["bbox"][2:]] * PT for im in pg.get_image_info()]
        for p in sh["placements"]:
            d = ds[p["d"]]
            swap = p["rot"] in (90, 270)
            fx, fy, fw, fh = d["full"]
            if d["vector"]:  # tiap path artwork: ukuran di PDF export = ukuran di file input
                subs, W, H = pdf_art(d["source"], d["page"])
                for s in subs:
                    exp = bbox(place(s * [fw / W, fh / H] + [fx, fy], p, d))
                    _, got = matched([exp], fills)[0]
                    size = np.ptp(s, axis=0) * PT
                    size = size[::-1] if swap else size
                    x1n += 1
                    if np.abs((got[2:] - got[:2]) - size).max() > 0.05:
                        x1.append(f"{sc['name']} {d['name']}: path {f2(got[2] - got[0])} x {f2(got[3] - got[1])}, file asli {f2(size[0])} x {f2(size[1])}")
            elif not d["bleedImage"]:  # gambar: konten = lebar yang diminta, gambar penuh sesuai piksel file
                ext = pathlib.Path(d["source"]).suffix.lower()
                if ext == ".svg":
                    size = np.array([d["artW"], d["artH"]])  # svg-vector.svg: artwork memenuhi seluruh viewBox
                elif d["pdfAsImage"]:  # artwork asli dari file input, diputar sesuai /Rotate halaman
                    src = fitz.open(IN / d["source"])[d["page"]]
                    size = np.ptp(np.vstack(pdf_art(d["source"], d["page"])[0]), axis=0) * PT
                    size = size[::-1] if src.rotation % 180 else size
                else:
                    A, B, w, h = raster_edges(d["source"])
                    P = np.r_[A, B]
                    cw_px = P[:, 0].max() - P[:, 0].min()
                    size = np.array([w, h]) * d["artW"] / cw_px
                exp = bbox(place(np.array([[fx, fy], [fx + fw, fy + fh]]), p, d))
                _, got = matched([exp], imgs)[0]
                size = size[::-1] if swap else size
                x1n += 1
                if np.abs((got[2:] - got[:2]) - size).max() > 0.05:
                    x1.append(f"{sc['name']} {d['name']}: gambar {f2(got[2] - got[0])} x {f2(got[3] - got[1])}, harapan {f2(size[0])} x {f2(size[1])}")
verdict("X1", not x1 and not eb,
        f"{len(SCN)} PDF produksi: jumlah dan ukuran halaman sesuai lembar; posisi artwork = E1; "
        f"{x1n} artwork diukur ulang dari PDF: ukuran sama dengan file asli/lebar yang diminta (batas 0,05 mm)" + "".join(f"; {t}" for t in x1[:12]))

# X2 warna garis potong: preflight content stream + separasi CMYK
pre, pages, strokes_n = [], 0, collections.Counter()
for sc in SCN:
    doc = fitz.open(RUN / sc["pdf"])
    ds = {d["id"]: d for d in sc["designs"]}
    if "/OutputIntents" in doc.xref_object(doc.pdf_catalog()):
        pre.append(f"{sc['name']}: ada OutputIntent")
    for i, sh in enumerate(sc["sheets"]):
        pages += 1
        res, ops = stroke_ops(doc[i].read_contents())
        cnt = collections.Counter(res)
        strokes_n.update(cnt)
        wk = sum(1 for p in sh["placements"] if ds[p["d"]]["kiss"])
        wd = sum(1 for p in sh["placements"] if ds[p["d"]]["die"])
        other = {k: v for k, v in cnt.items() if k not in (KISS_K, DIE_K)}
        badop = ops & {"RG", "rg", "CS", "cs", "SC", "SCN", "sc", "scn"}
        cs = doc.xref_get_key(doc[i].xref, "Resources/ColorSpace")[0]
        if cnt[KISS_K] != wk or cnt[DIE_K] != wd or other or badop or cs != "null":
            pre.append(f"{sc['name']} lembar {i + 1}: kiss {cnt[KISS_K]}/{wk}, die {cnt[DIE_K]}/{wd}, warna lain {other}, operator {sorted(badop)}, ColorSpace {cs}")
fitz.TOOLS.set_aa_level(0)  # tanpa antialias: piksel garis = warna garis murni
sep = {"kiss": [0, 0], "die": [0, 0]}
rng = np.random.default_rng(1)
for sc in SCN:
    doc = fitz.open(RUN / sc["pdf"])
    ds = {d["id"]: d for d in sc["designs"]}
    for i, sh in enumerate(sc["sheets"]):
        pg = doc[i]
        for kind, want in (("kiss", (0, 255, 0, 0)), ("die", (255, 0, 0, 0))):
            pts = [place(c, p, ds[p["d"]]) for p in sh["placements"] if ds[p["d"]][kind] for c in parse(ds[p["d"]][kind])[0]]
            if not pts:
                continue
            P = np.vstack(pts)
            for x, y in P[rng.choice(len(P), min(25, len(P)), replace=False)]:
                X, Y = x / PT, y / PT
                pix = pg.get_pixmap(matrix=fitz.Matrix(600 / 72, 600 / 72), clip=fitz.Rect(X - 3, Y - 3, X + 3, Y + 3), colorspace=fitz.csCMYK, alpha=False)
                a = np.frombuffer(pix.samples, np.uint8).reshape(pix.h, pix.w, 4)
                u, v = int(X * 600 / 72) - pix.x, int(Y * 600 / 72) - pix.y
                win = a[max(0, v - 2):v + 3, max(0, u - 2):u + 3].reshape(-1, 4)
                sep[kind][0] += 1
                sep[kind][1] += bool((win == want).all(1).any())
fitz.TOOLS.set_aa_level(8)
verdict("X2", not pre and all(s[0] == s[1] for s in sep.values()),
        f"preflight {pages} halaman: garis di-stroke dengan 0 1 0 0 K sebanyak {strokes_n[KISS_K]}, 1 0 0 0 K sebanyak {strokes_n[DIE_K]}, "
        f"warna stroke lain {sum(v for k, v in strokes_n.items() if k not in (KISS_K, DIE_K))}; operator RGB/ICC/Separation 0; OutputIntent 0. "
        f"Separasi CMYK (render MuPDF, tanpa antialias): kiss {sep['kiss'][1]}/{sep['kiss'][0]} titik tepat C0 M100 Y0 K0, "
        f"die {sep['die'][1]}/{sep['die'][0]} titik tepat C100 M0 Y0 K0" + "".join(f"; {t}" for t in pre[:10]))

# X3 vektor tetap vektor, warna CMYK artwork utuh; PDF yang jadi gambar diberi peringatan
x3, vec_n, ras_pdf = [], 0, []
for sc in SCN:
    doc = fitz.open(RUN / sc["pdf"])
    ds = {d["id"]: d for d in sc["designs"]}
    for i, sh in enumerate(sc["sheets"]):
        pg = doc[i]
        form = set()
        for xref, *_ in pg.get_xobjects():
            raw = doc.xref_stream(xref) or b""
            form |= fills_k(raw)
            if re.search(rb"\b(rg|RG)\b", raw):
                x3.append(f"{sc['name']} lembar {i + 1}: artwork vektor memakai RGB")
        for p in sh["placements"]:
            d = ds[p["d"]]
            ext = pathlib.Path(d["source"]).suffix.lower()
            if d["vector"]:
                vec_n += 1
                want = fills_k(fitz.open(IN / d["source"])[d["page"]].read_contents())
                if not want <= form:
                    x3.append(f"{sc['name']} {d['name']}: warna isi {sorted(want - form)} hilang")
            elif ext in (".pdf", ".ai", ".cdr"):
                ras_pdf.append(f"{sc['name']} {d['name']}")
                if not d["pdfAsImage"] or pathlib.Path(d["source"]).name not in ("pdf-rotated.pdf", "pdf-encrypted.pdf"):
                    x3.append(f"{sc['name']} {d['name']}: PDF jadi gambar tanpa alasan/peringatan")
ras_ok = all(any(pathlib.Path(d["source"]).name == f and d["pdfAsImage"] for d in SC["X-pdf-diputar"]["designs"])
             for f in ("pdf-rotated.pdf", "pdf-encrypted.pdf"))
verdict("X3", not x3 and ras_ok, f"{vec_n} penempatan PDF/AI tetap vektor (Form XObject, tanpa gambar), warna isi CMYK sama persis dengan file input; "
        f"PDF yang jadi gambar hanya pdf-rotated.pdf (/Rotate 90) dan pdf-encrypted.pdf (terenkripsi, gagal ditempel PDFsharp, D8), "
        f"keduanya ditandai untuk peringatan dan export tetap jadi: {len(ras_pdf)} penempatan; SVG ditempel sebagai gambar sesuai PRD"
        + "".join(f"; {t}" for t in x3[:10]))

# X4 preview JPG
x4, files_n, agree = [], 0, [0, 0]
for sc in (s for s in SCN if s["name"].startswith("B-")):
    doc = fitz.open(RUN / sc["pdf"])
    ds = {d["id"]: d for d in sc["designs"]}
    if len(sc["previews"]) != len(sc["sheets"]):
        x4.append(f"{sc['name']}: {len(sc['previews'])} JPG untuk {len(sc['sheets'])} lembar")
    for i, (f, sh) in enumerate(zip(sc["previews"], sc["sheets"])):
        files_n += 1
        im = Image.open(RUN / f)
        w, h = im.size
        k = 2000 / max(sc["cw"], sh["h"])
        if im.format != "JPEG" or max(w, h) != 2000 or abs(w - sc["cw"] * k) > 1 or abs(h - sh["h"] * k) > 1:
            x4.append(f"{sc['name']} {f}: {im.format} {w} x {h} px, harapan {round(sc['cw'] * k)} x {round(sh['h'] * k)}")
        pix = doc[i].get_pixmap(matrix=fitz.Matrix(w / doc[i].rect.width, h / doc[i].rect.height), alpha=False)
        ref = np.asarray(Image.frombytes("RGB", (pix.w, pix.h), pix.samples).convert("L").resize((w, h)), float)
        prv = np.asarray(im.convert("L"), float)
        for p in sh["placements"]:
            d = ds[p["d"]]
            cx, cy = place(np.array([[d["off"] + d["artW"] / 2, d["off"] + d["artH"] / 2]]), p, d)[0] * k
            u, v = int(cx), int(cy)
            a, b = prv[v - 3:v + 4, u - 3:u + 4].mean() < 235, ref[v - 3:v + 4, u - 3:u + 4].mean() < 235
            agree[0] += 1
            agree[1] += a == b
verdict("X4", not x4 and agree[1] >= 0.95 * agree[0], f"{files_n} preview JPG untuk {sum(len(s['sheets']) for s in SCN if s['name'].startswith('B-'))} lembar: "
        f"JPEG, sisi panjang 2000 px, proporsi = ukuran lembar; isi di posisi stiker sama dengan PDF pada {agree[1]}/{agree[0]} stiker"
        + "".join(f"; {t}" for t in x4[:10]))

# X5 Print Ticket
x5, tickets = [], 0
for sc in (s for s in SCN if s["name"].startswith("B-")):
    tickets += 1
    doc = fitz.open(RUN / sc["ticket"])
    pg = doc[0]
    text = pg.get_text()
    ds = {d["id"]: d for d in sc["designs"]}
    if len(doc) != 1 or abs(pg.rect.width * PT - 210) > 0.5 or abs(pg.rect.height * PT - 297) > 0.5:
        x5.append(f"{sc['name']}: {len(doc)} halaman {f2(pg.rect.width * PT)} x {f2(pg.rect.height * PT)} mm")
    rows = S[sc["name"]]["use"]
    fills = [(sc["cw"] - 2 * sc["margin"]) * (sh["h"] - 2 * sc["margin"]) for sh in sc["sheets"]]
    use = sum(r[2] * fa for r, fa in zip(rows, fills)) / sum(fills)

    def num(pat):
        m = re.search(pat, text)
        return int(m.group(1)) if m else None
    checks = [("total lembar", num(r"Total lembar: (\d+)"), len(sc["sheets"]), 0),
              ("total stiker", num(r"Total stiker: (\d+) pcs"), sum(len(sh["placements"]) for sh in sc["sheets"]), 0),
              ("efisiensi bahan %", num(r"Efisiensi bahan: (\d+)\s*%"), round(use * 100), 1)]
    for i, (sh, r) in enumerate(zip(sc["sheets"][:10], rows)):
        m = re.search(rf"Lembar {i + 1}: (\d+) pcs, bahan (\d+)\s*%", text)
        checks += [(f"lembar {i + 1} pcs", int(m.group(1)) if m else None, len(sh["placements"]), 0),
                   (f"lembar {i + 1} bahan %", int(m.group(2)) if m else None, round(r[2] * 100), 1)]
    count = collections.Counter(p["d"] for sh in sc["sheets"] for p in sh["placements"])
    for did, n in list(count.items())[:20]:
        m = re.search(re.escape(ds[did]["name"]) + r" — .*? — (\d+) pcs", text)
        checks.append((f"desain {ds[did]['name']}", int(m.group(1)) if m else None, n, 0))
    for name, got, want, tol in checks:
        if got is None or abs(got - want) > tol:
            x5.append(f"{sc['name']} {name}: tiket {got}, layout {want}")
    for line in sc["ticketLines"] + ["Kertas: A3+"]:
        if line not in text:
            x5.append(f"{sc['name']}: baris '{line}' tidak ada")
verdict("X5", not x5, f"{tickets} Print Ticket: 1 halaman A4; total lembar, total stiker, efisiensi bahan, rincian per lembar dan per desain, "
        f"serta pengaturan potong sesuai layout (efisiensi dibanding hitung ulang dari garis, toleransi pembulatan 1 poin)" + "".join(f"; {t}" for t in x5[:12]))
out()


# ---------- gambar bukti ----------

def panel(name, m, A, B, size=360):
    P = np.vstack([np.vstack(m[k]["conts"]) for k in ("kiss", "die") if k in m] + [A, B])
    lo, hi = P.min(0) - 1, P.max(0) + 1
    k = size / max(hi - lo)
    im = Image.new("RGB", (size, size + 18), "white")
    g = ImageDraw.Draw(im)
    for a, b in zip((A - lo) * k, (B - lo) * k):
        g.line([tuple(a), tuple(b)], fill=(90, 90, 90), width=1)
    for kind, col in (("kiss", (236, 0, 140)), ("die", (0, 174, 239))):
        for c in m.get(kind, {}).get("conts", ()):
            g.line([tuple(q) for q in (c - lo) * k], fill=col, width=1)
    g.text((4, size + 3), name[:52], fill="black")
    return im


std = SC["A-standar"]
tiles = [panel(d["name"], M["A-standar", d["name"]], *art(d)) for d in std["designs"]]
cols = 6
sheet = Image.new("RGB", (cols * 360, math.ceil(len(tiles) / cols) * 378), "white")
for i, t in enumerate(tiles):
    sheet.paste(t, ((i % cols) * 360, (i // cols) * 378))
sheet.save(EVID / f"{DAY}-validasi-garis-potong.png")

d = next(d for d in SC["A-halus-1"]["designs"] if d["name"] == "png-rough-edge.png")
row = Image.new("RGB", (4 * 400, 418), "white")
for i, h in enumerate(hal):
    dh = next(x for x in SC[f"A-halus-{h:g}"]["designs"] if x["name"] == "png-rough-edge.png")
    m = M[f"A-halus-{h:g}", "png-rough-edge.png"]
    A, B = art(dh)
    lo = np.array([dh["full"][0] + dh["full"][2] * 0.80 - 6, dh["full"][1] + dh["full"][3] * 0.40])  # potongan tepi kanan
    k = 400 / 14
    im = Image.new("RGB", (400, 418), "white")
    g = ImageDraw.Draw(im)
    for a, b in zip((A - lo) * k, (B - lo) * k):
        g.line([tuple(a), tuple(b)], fill=(90, 90, 90), width=1)
    for kind, col in (("kiss", (236, 0, 140)), ("die", (0, 174, 239))):
        for c in m[kind]["conts"]:
            g.line([tuple(q) for q in (c - lo) * k], fill=col, width=2)
    g.text((4, 403), f"Halus {f2(h)} mm: kiss {m['kiss']['nodes']} node, die {m['die']['nodes']} node", fill="black")
    row.paste(im, (i * 400, 0))
row.save(EVID / f"{DAY}-validasi-halus.png")
for s in ("B-tidak-beraturan", "B-campuran"):
    fitz.open(RUN / SC[s]["pdf"])[0].get_pixmap(dpi=40).save(EVID / f"{DAY}-validasi-{s}-lembar1.png")
# Separasi CMYK lembar 1 B-campuran: satu pelat per tinta (hitam = tinta 100%), seperti pratinjau separasi di CorelDRAW.
pix = fitz.open(RUN / SC["B-campuran"]["pdf"])[0].get_pixmap(dpi=400, colorspace=fitz.csCMYK, alpha=False,
                                                            clip=fitz.Rect(8 / PT, 8 / PT, 78 / PT, 58 / PT))  # pojok kiri atas, 70 x 50 mm
a = np.frombuffer(pix.samples, np.uint8).reshape(pix.h, pix.w, 4)
seps = Image.new("L", (4 * pix.w + 30, pix.h + 20), 255)
for i, name in enumerate("CMYK"):
    seps.paste(Image.fromarray(255 - a[..., i]), (i * (pix.w + 10), 20))
    ImageDraw.Draw(seps).text((i * (pix.w + 10) + 4, 4), f"pelat {name}", fill=0)
seps.save(EVID / f"{DAY}-validasi-separasi-cmyk.png")

out(f"Gambar bukti: 04-evidence/{DAY}-validasi-garis-potong.png, {DAY}-validasi-halus.png, {DAY}-validasi-B-*-lembar1.png, "
    f"{DAY}-validasi-separasi-cmyk.png")
out(f"RINGKAS: {'SEMUA LULUS' if not failed else f'{failed} kriteria GAGAL'}")
(EVID / f"{DAY}-validasi-garis-potong-nesting.txt").write_text("\n".join(report) + "\n", encoding="utf-8")
sys.exit(1 if failed else 0)
