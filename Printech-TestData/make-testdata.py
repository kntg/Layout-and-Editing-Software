"""Membuat file uji Printech di 01-input/ dengan ukuran yang sudah diketahui sebelum dimasukkan ke aplikasi.

Jalankan ulang: python make-testdata.py  (butuh PyMuPDF dan Pillow).
File yang sudah ada tidak ditimpa. PyMuPDF tidak menghasilkan byte yang sama setiap kali dijalankan, jadi
menimpa file berarti mengubah file original. Hapus file dulu bila memang ingin membuatnya ulang, lalu catat
hash barunya (cek-original.ps1 -Catat).
Hasil benar untuk setiap file ada di 02-expected/hasil-yang-diharapkan.md.
cdr-normal.cdr tidak dibuat di sini karena butuh CorelDRAW (lihat 01-input/cdr-normal.BACA.txt).
"""
import math
import pathlib

import fitz  # PyMuPDF
from PIL import Image, ImageDraw

MM = 72 / 25.4  # point per mm
OUT = pathlib.Path(__file__).parent / "01-input"
BLUE, ORANGE, GREEN = (1, 0.4, 0, 0), (0, 0.6, 1, 0), (0.8, 0, 0.9, 0.2)  # CMYK


def rect(x, y, w, h):
    return fitz.Rect(x * MM, y * MM, (x + w) * MM, (y + h) * MM)


def pt(x, y):
    return fitz.Point(x * MM, y * MM)


def poly(page, points, fill):
    page.draw_polyline([pt(x, y) for x, y in points], color=None, fill=fill, closePath=True)


def rounded(x, y, w, h, r, n=90):
    """Titik persegi panjang w x h mm bersudut bulat radius r mm."""
    pts = []
    for cx, cy, a in ((x + w - r, y + r, -90), (x + w - r, y + h - r, 0), (x + r, y + h - r, 90), (x + r, y + r, 180)):
        pts += [(cx + r * math.cos(math.radians(a + 90 * i / n)), cy + r * math.sin(math.radians(a + 90 * i / n))) for i in range(n + 1)]
    return pts


def save(name, write):
    path = OUT / name
    if path.exists():
        print("tidak ditimpa (sudah ada):", name)
    else:
        write(path)


def pdf(name, pages):
    """pages: daftar (lebar halaman mm, tinggi halaman mm, fungsi gambar)."""
    doc = fitz.open()
    for w, h, draw in pages:
        draw(doc.new_page(width=w * MM, height=h * MM))
    save(name, doc.save)


def single(page):
    # Persegi panjang biru 60 x 30 mm; lingkaran oranye diameter 20 mm di dalamnya. Batas artwork = 60 x 30 mm.
    page.draw_rect(rect(20, 20, 60, 30), color=None, fill=BLUE)
    page.draw_circle(pt(50, 35), 10 * MM, color=None, fill=ORANGE)


def star(page):
    # Bintang 5 sudut: jari-jari luar 20 mm, dalam 8 mm, satu sudut ke atas. Batas artwork 38,04 x 36,18 mm.
    poly(page, [(30 + (20 if k % 2 == 0 else 8) * math.cos(math.radians(-90 + 36 * k)),
                 30 + (20 if k % 2 == 0 else 8) * math.sin(math.radians(-90 + 36 * k))) for k in range(10)], ORANGE)


def ring(page):
    # Cincin "C": diameter luar 40 mm, dalam 24 mm, celah lurus 5 mm ke arah kanan atas (45 derajat).
    # Celah 5 mm lebih lebar dari 2 x offset kiss (4 mm) tapi lebih sempit dari 2 x (offset + halus) (6 mm):
    # yang menutupnya adalah penghalus (closing), bukan offset saja. Batas artwork 40 x 40 mm.
    a0, ho, hi, n = math.radians(-45), math.asin(2.5 / 20), math.asin(2.5 / 12), 720
    outer = [a0 + ho + (2 * math.pi - 2 * ho) * i / n for i in range(n + 1)]
    inner = [a0 + 2 * math.pi - hi - (2 * math.pi - 2 * hi) * i / n for i in range(n + 1)]
    poly(page, [(30 + 20 * math.cos(a), 30 + 20 * math.sin(a)) for a in outer] +
         [(30 + 12 * math.cos(a), 30 + 12 * math.sin(a)) for a in inner], BLUE)


def thin(page):
    # Lingkaran diameter 30 mm dengan garis tipis 0,2 x 15,5 mm menempel di kanan. Batas artwork 45 x 30 mm.
    page.draw_circle(pt(25, 25), 15 * MM, color=None, fill=GREEN)
    page.draw_rect(rect(39.5, 24.9, 15.5, 0.2), color=None, fill=GREEN)


def parts(page):
    # Tiga lingkaran diameter 20 mm sebaris: celah A-B 4 mm, celah B-C 15 mm. Batas artwork 79 x 20 mm.
    for cx in (20, 44, 79):
        page.draw_circle(pt(cx, 20), 10 * MM, color=None, fill=BLUE)


OUT.mkdir(parents=True, exist_ok=True)
pdf("pdf-single-page.pdf", [(120, 80, single)])
pdf("pdf-multi-page.pdf", [
    (60, 60, lambda p: p.draw_circle(pt(30, 30), 15 * MM, color=None, fill=ORANGE)),  # lingkaran diameter 30 mm
    (120, 50, lambda p: p.draw_rect(rect(20, 15, 80, 20), color=None, fill=BLUE)),  # persegi panjang 80 x 20 mm
    (70, 60, lambda p: p.draw_polyline([pt(15, 50), pt(55, 50), pt(35, 15)], color=None, fill=GREEN, closePath=True)),  # segitiga 40 x 35 mm
])
# AI "PDF-compatible" = PDF biasa dengan ekstensi .ai. Isi: elips 50 x 25 mm.
pdf("ai-compatible.ai", [(90, 60, lambda p: p.draw_oval(rect(20, 17.5, 50, 25), color=None, fill=GREEN))])
pdf("pdf-irregular-shapes.pdf", [(60, 60, star), (60, 60, ring), (80, 50, thin), (100, 40, parts)])
# PDF dengan halaman diputar (/Rotate 90): isi sama dengan pdf-single-page.pdf, tampil tegak 30 x 60 mm.
# Spesifikasi: ditempel sebagai gambar (bukan vektor) dan aplikasi memberi peringatan.
pdf("pdf-rotated.pdf", [(120, 80, lambda p: (single(p), p.set_rotation(90)))])
# Artwork besar: 280 x 420 mm (muat satu lembar A3+) dan 310 x 310 mm (lebih besar dari kertas). Sudut radius 20 mm.
pdf("pdf-large-a3.pdf", [(300, 440, lambda p: poly(p, rounded(10, 10, 280, 420, 20), BLUE))])
pdf("pdf-oversize.pdf", [(330, 330, lambda p: poly(p, rounded(10, 10, 310, 310, 20), ORANGE))])

# PNG transparan 1000 x 600 px: persegi panjang hijau 800 x 400 px (sudut membulat 60 px) dengan lubang lingkaran
# transparan diameter 160 px di tengah. Tanpa antialias, jadi batas konten tepat 800 x 400 px.
img = Image.new("RGBA", (1000, 600), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)
draw.rounded_rectangle((100, 100, 899, 499), radius=60, fill=(40, 160, 90, 255))
draw.ellipse((420, 220, 579, 379), fill=(0, 0, 0, 0))
save("png-transparent.png", img.save)

# JPG resolusi rendah 240 x 240 px: latar putih, lingkaran merah diameter 200 px. Pada lebar 50 mm = ±102 dpi.
img = Image.new("RGB", (240, 240), (255, 255, 255))
ImageDraw.Draw(img).ellipse((20, 20, 219, 219), fill=(210, 30, 50))
save("jpg-low-resolution.jpg", lambda p: img.save(p, quality=95))

# Batas peringatan 150 dpi: lingkaran oranye diameter 290 px dan 300 px di PNG transparan.
# Pada lebar 50 mm: 290 px = 147,3 dpi (merah), 300 px = 152,4 dpi (tidak merah).
for name, d in (("png-147dpi.png", 290), ("png-152dpi.png", 300)):
    img = Image.new("RGBA", (d + 40, d + 40), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse((20, 20, 19 + d, 19 + d), fill=(230, 120, 20, 255))
    save(name, img.save)

# Logo bertepi tidak beraturan: gumpalan merah tua di PNG transparan, tepi bergelombang dengan 4 frekuensi
# (5, 17, 43, 97 gelombang per keliling) seperti logo hasil scan. Tanpa antialias.
img = Image.new("RGBA", (1200, 900), (0, 0, 0, 0))
ImageDraw.Draw(img).polygon([(600 + r * math.cos(t), 450 + r * math.sin(t)) for t, r in (
    (t, 330 + 30 * math.sin(5 * t) + 14 * math.sin(17 * t + 0.7) + 7 * math.sin(43 * t + 1.9) + 4 * math.sin(97 * t + 0.3))
    for t in (2 * math.pi * i / 4000 for i in range(4000)))], fill=(150, 20, 40, 255))
save("png-rough-edge.png", img.save)

# Gambar besar 6000 x 4000 px (24 MP, ukuran foto kamera): latar putih, elips biru 5600 x 3600 px.
img = Image.new("RGB", (6000, 4000), (255, 255, 255))
ImageDraw.Draw(img).ellipse((200, 200, 5799, 3799), fill=(40, 120, 200))
save("jpg-large-24mp.jpg", lambda p: img.save(p, quality=90))

# SVG vektor: persegi panjang membulat 100 x 50 (rasio 2:1), lingkaran putih di dalamnya.
save("svg-vector.svg", lambda p: p.write_text(
    '<svg xmlns="http://www.w3.org/2000/svg" width="100mm" height="50mm" viewBox="0 0 100 50">'
    '<rect x="0" y="0" width="100" height="50" rx="8" fill="#2a6fd6"/>'
    '<circle cx="25" cy="25" r="12" fill="#ffffff"/></svg>\n', encoding="utf-8"))

# File rusak: teks biasa dengan ekstensi .cdr.
save("invalid-file.cdr", lambda p: p.write_text("Ini bukan file CorelDRAW. Dipakai untuk menguji pesan error.\n", encoding="utf-8"))

# Kasus khusus (01-input/khusus), diuji terpisah dari "semua file sekaligus":
# - AI lama tanpa PDF-compatible (PostScript): tidak bisa dibaca, harus muncul pesan.
# - PDF terenkripsi (hanya kata sandi pemilik): isi sama dengan pdf-single-page.pdf.
# - PNG/JPG/SVG rusak (teks biasa): harus muncul pesan, file lain tetap masuk.
(OUT / "khusus").mkdir(exist_ok=True)
save("khusus/ai-postscript.ai", lambda p: p.write_text(
    "%!PS-Adobe-3.0 EPSF-3.0\n%%Creator: Adobe Illustrator(R) 8.0\n%%BoundingBox: 0 0 170 85\n%%EndComments\n"
    "1 0.4 0 0 setcmykcolor 0 0 170 85 rectfill\n%%EOF\n", encoding="utf-8"))
enc = fitz.open()
single(enc.new_page(width=120 * MM, height=80 * MM))
save("khusus/pdf-encrypted.pdf", lambda p: enc.save(p, encryption=fitz.PDF_ENCRYPT_AES_256, owner_pw="pemilik", user_pw=""))
for ext in ("png", "jpg", "svg"):
    save(f"khusus/invalid-file.{ext}", lambda p: p.write_text(f"Ini bukan file {ext.upper()}. Dipakai untuk menguji pesan error.\n", encoding="utf-8"))

# Latar PNG opak (01-input/latar, temuan D10): 600 x 400 px, persegi biru membulat 300 x 200 px (radius 30) dan
# lingkaran oranye diameter 80 px di tengah. Latar opak tidak dibuang otomatis (termasuk putih): seluruh gambar = artwork.
(OUT / "latar").mkdir(exist_ok=True)
for name, bg in (("abu-muda", (242, 242, 242)), ("pink-pastel", (255, 240, 245)), ("putih", (255, 255, 255))):
    img = Image.new("RGB", (600, 400), bg)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((150, 100, 450, 300), 30, fill=(0, 112, 192))
    d.ellipse((260, 160, 340, 240), fill=(240, 130, 30))
    save(f"latar/png-latar-{name}.png", lambda p: img.save(p))

print("selesai:", ", ".join(sorted(p.name for p in OUT.iterdir() if p.is_file())))
