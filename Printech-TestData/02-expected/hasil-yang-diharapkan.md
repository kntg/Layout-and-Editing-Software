# Hasil yang benar untuk file uji

Angka di dokumen ini berasal dari cara file uji dibuat (skrip [make-testdata.py](../make-testdata.py)), bukan dari menjalankan Printech. Bila Printech menunjukkan angka lain di luar batas selisih yang diterima, berarti ada yang salah di aplikasi atau di file uji.

Validasi otomatis yang lebih teliti untuk garis potong dan susun otomatis (jarak garis ke artwork diukur di setiap titik, tabrakan, jarak antar stiker, margin, rotasi, dan pemakaian bahan) ada di [validasi garis potong dan susunan](validasi-garis-potong-susunan.md).

## Skenario uji dan file

| Skenario | File | Yang diperiksa | Hasil benar | Cara cek |
| --- | --- | --- | --- | --- |
| PDF satu halaman | `pdf-single-page.pdf` | Artwork dan ukurannya | Vektor 60,0 × 30,0 mm. Garis kiss 64 × 34 mm. | Panel Desain, export |
| PDF beberapa halaman | `pdf-multi-page.pdf`, `pdf-irregular-shapes.pdf` | Setiap halaman jadi desain terpisah | 3 desain (`p1`–`p3`) dan 4 desain (`p1`–`p4`), ukuran per halaman sesuai tabel di bawah | Panel Desain |
| AI dan SVG | `ai-compatible.ai`, `svg-vector.svg` | Garis luar, ukuran, dan artwork terbaca benar | 50,0 × 25,0 mm, 1 garis kiss dan 1 garis die. Lingkaran putih di dalam SVG tidak dipotong. | Panel Desain, export |
| CDR yang benar | `cdr-normal.cdr` (dibuat operator, lihat [cdr-normal.BACA.txt](../01-input/cdr-normal.BACA.txt)) | Konversi berhasil; aplikasi utama tidak terhenti | Sama dengan `pdf-single-page.pdf` | Operator di PC dengan CorelDRAW ([skenario uji stabilitas](../../docs/UJI-STABILITAS.md), langkah P7) |
| CDR rusak atau proses macet | `invalid-file.cdr`; proses macet: [skenario uji stabilitas](../../docs/UJI-STABILITAS.md) langkah P9 | Kegagalan tertangani, termasuk batas waktu CDR 30 detik | Dialog "Gagal memuat" dengan pesan yang jelas; file lain tetap masuk; aplikasi tidak tertutup sendiri (crash). Bila macet: setelah 30 detik muncul `CorelDRAW tidak merespons dalam 30 detik. …`; proses pembantu yang mengonversi CDR dihentikan; CorelDRAW tidak ditutup paksa. | Lewat aplikasi; pemeriksaan otomatis P1–P2 (jalur batas waktu); operator langkah P9 |
| PNG transparan dan JPG | `png-transparent.png`, `jpg-low-resolution.jpg`, `png-rough-edge.png` | Batas artwork, pemangkasan, bagian transparan, garis potong | PNG transparan: dipangkas sampai batas isinya. Lubang transparan Ø 10 mm tetap terbuka setelah offset kiss 2 + halus 1, jadi ada garis kiss di dalam lubang (2 garis); die 4 + halus 2 menutup lubang (1 garis). Ini aturan stiker donat dari masukan leader 2026-10-21. JPG (tanpa bagian transparan): seluruh gambar dipakai, latar putih tidak dibuang, dan garis mengelilingi kotak gambar (masukan leader 2026-10-21). | Panel Desain, validasi otomatis bagian A |
| Gambar di bawah 150 dpi | `jpg-low-resolution.jpg` (±122), `png-147dpi.png` (147), `png-152dpi.png` (152) | Peringatan resolusi sesuai spesifikasi: merah bila di bawah 150 dpi | ±122 dan 147 merah; 152 tidak merah | Panel Desain, peringatan |
| Vektor tetap vektor saat export | `pdf-single-page.pdf`, `pdf-rotated.pdf` | Artwork yang seharusnya vektor tidak berubah menjadi gambar tanpa alasan | PDF biasa tetap vektor (warna CMYK sama). PDF yang halamannya diberi perintah putar (/Rotate) ditempel sebagai gambar RGB ±254 dpi (batasan yang tercatat di [kebutuhan aplikasi](../../docs/kebutuhan-aplikasi.md)), dan muncul peringatan "1 PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)". | Peringatan, validasi otomatis X3 |
| Artwork besar atau bentuk tidak beraturan | `pdf-large-a3.pdf`, `pdf-oversize.pdf`, `jpg-large-24mp.jpg`, `pdf-irregular-shapes.pdf`, `png-rough-edge.png` | Pemrosesan stabil; tidak ada bagian yang terpotong keliru | Tiap file dimuat dalam waktu kurang dari 5 detik tanpa crash. `pdf-oversize.pdf` tidak dipasang dan masuk peringatan "1 lebih besar dari kertas". Cincin C: celahnya tertutup, lubangnya tidak dipotong. Garis tipis 0,2 mm tetap dikelilingi garis potong. Lingkaran dengan celah 4 mm menyatu; yang bercelah 15 mm punya garis sendiri. | Panel Desain, validasi otomatis bagian A dan B |

## Kasus khusus (`01-input/khusus/`)

File di folder ini diuji terpisah. Jangan ikut ditambahkan dalam uji "semua file sekaligus".

| File | Hasil benar |
| --- | --- |
| `ai-postscript.ai` (file AI lama tanpa opsi PDF-compatible) | Dialog "Gagal memuat" berisi nama file dan pesan "File AI ini tidak bisa dibaca: biasanya karena disimpan tanpa opsi PDF-compatible…" (sejak 2026-10-28, D9); aplikasi tidak crash |
| `invalid-file.png`, `invalid-file.jpg`, `invalid-file.svg` (isinya teks biasa) | Masing-masing memunculkan dialog "Gagal memuat" dengan pesan "Gambar rusak atau bukan file PNG/JPG yang valid." atau "SVG rusak atau bukan file SVG yang valid." (sejak 2026-10-28, D9); aplikasi tidak crash |
| `pdf-encrypted.pdf` (hanya diberi kata sandi pemilik) | Dimuat, artwork 60,0 × 30,0 mm. Bila Printech bisa menempelnya sebagai vektor, desain tetap vektor. Bila tidak, desain ditempel sebagai gambar dan muncul peringatan "1 PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)" ([kebutuhan aplikasi](../../docs/kebutuhan-aplikasi.md) §8). Export PDF tetap jadi, tanpa galat. |
| Kelima file ditambahkan sekaligus | 4 dialog, ditutup satu per satu; 1 desain masuk; status "1 stiker di 1 lembar."; aplikasi tetap responsif |

## Latar PNG (`01-input/latar/`, temuan D10)

Gambar PNG tanpa bagian transparan, 600 × 400 piksel: persegi biru bersudut bulat 300 × 200 piksel dan lingkaran oranye di tengah. Sejak masukan leader 2026-10-21, latar gambar tanpa bagian transparan tidak pernah dibuang otomatis, termasuk latar putih. Operator membuang latar sendiri lewat Rapikan atau editor garis potong.

| File | Hasil benar (lebar 50 mm) |
| --- | --- |
| `png-latar-abu-muda.png` (latar 242, 242, 242) | Seluruh gambar menjadi artwork 50 × 33,3 mm, 305 dpi; latar abu tampil di layar dan di PDF; garis kiss berupa kotak bersudut tajam sejauh offset dari kotak gambar (desain kotak, sejak 2026-10-23) |
| `png-latar-pink-pastel.png` (latar 255, 240, 245) | Sama dengan abu muda |
| `png-latar-putih.png` (latar putih) | Sama dengan abu muda: seluruh gambar 50 × 33,3 mm, 305 dpi; garis kiss berupa kotak bersudut tajam |

## Pengaturan saat menguji

Pakai nilai bawaan aplikasi:

- kertas A3+ (320 × 480 mm), margin 10 mm, jarak antar stiker 2 mm, bleed 0;
- lebar awal PNG/JPG/SVG 50 mm;
- kiss-cut aktif dengan offset 2 mm, halus 1 mm;
- die-cut mati. Untuk kolom "Garis die", aktifkan die-cut dengan offset 4 mm, halus 2 mm.

Ukuran garis potong = lebar × tinggi kotak terluar garis. Garis kiss = artwork + 2 × 2 mm, garis die = artwork + 2 × 4 mm.

## Per file

| File | Desain yang muncul | Jenis | Ukuran artwork (mm) | dpi | Garis kiss (mm) | Garis die (mm) |
| --- | --- | --- | --- | --- | --- | --- |
| `pdf-single-page.pdf` | 1: `pdf-single-page.pdf` | vektor | 60,0 × 30,0 | tidak tampil | 64 × 34 | 68 × 38 |
| `pdf-multi-page.pdf` | 3: `pdf-multi-page.pdf p1`, `p2`, `p3` | vektor | p1 30,0 × 30,0; p2 80,0 × 20,0; p3 40,0 × 35,0 | tidak tampil | 34 × 34; 84 × 24; 44 × 39 | 38 × 38; 88 × 28; 48 × 43 |
| `pdf-irregular-shapes.pdf` | 4: p1 bintang, p2 cincin C (celah 5 mm), p3 lingkaran + garis tipis 0,2 mm, p4 tiga lingkaran (celah 4 dan 15 mm) | vektor | p1 38,0 × 36,2; p2 40,0 × 40,0; p3 45,0 × 30,0; p4 79,0 × 20,0 | tidak tampil | p1 42,0 × 40,2; p2 44 × 44; p3 49 × 34; p4 83 × 24, **2 garis** | p1 46,0 × 44,2; p2 48 × 48; p3 53 × 38; p4 87 × 28, **2 garis** |
| `pdf-large-a3.pdf` | 1 | vektor | 280,0 × 420,0 | tidak tampil | 284 × 424 | 288 × 428 |
| `pdf-oversize.pdf` | 1; tidak muat di A3+ | vektor | 310,0 × 310,0 | tidak tampil | 314 × 314 | 318 × 318 |
| `pdf-rotated.pdf` | 1; halaman diputar 90° (/Rotate 90), isinya sama dengan `pdf-single-page.pdf` | gambar (diubah menjadi gambar ±254 dpi), dengan peringatan | 30,0 × 60,0 | 254 (tidak merah) | 34 × 64 | 38 × 68 |
| `ai-compatible.ai` | 1: `ai-compatible.ai` | vektor | 50,0 × 25,0 | tidak tampil | 54 × 29 | 58 × 33 |
| `cdr-normal.cdr` | 1: `cdr-normal.cdr` | vektor | 60,0 × 30,0 (sama dengan `pdf-single-page.pdf`) | tidak tampil | 64 × 34 | 68 × 38 |
| `png-transparent.png` | 1 | gambar | 50,0 × 25,0 | 406 (tidak merah) | 54 × 29 | 58 × 33 |
| `png-rough-edge.png` | 1 (logo bertepi bergelombang) | gambar | 50,0 × 48,9 | 366 (tidak merah) | 54,0 × 52,9 | 58,0 × 56,9 |
| `png-147dpi.png` | 1 | gambar | 50,0 × 50,0 | 147, **merah** | 54 × 54 | 58 × 58 |
| `png-152dpi.png` | 1 | gambar | 50,0 × 50,0 | 152 (tidak merah) | 54 × 54 | 58 × 58 |
| `jpg-low-resolution.jpg` | 1 | gambar (seluruh 240 × 240 piksel) | 50,0 × 50,0 | ±122, **merah** | 54 × 54 | 58 × 58 |
| `jpg-large-24mp.jpg` | 1 (6000 × 4000 piksel) | gambar (seluruh gambar) | 50,0 × 33,3 | ±3048 (tidak merah) | 54 × 37,3 | 58 × 41,3 |
| `svg-vector.svg` | 1 | gambar (SVG diubah menjadi gambar ±300 dpi) | 50,0 × 25,0 | ±305 (tidak merah) | 54 × 29 | 58 × 33 |
| `invalid-file.cdr` | 0 | — | — | — | — | — |

Batas selisih yang diterima:

- **Ukuran artwork:**
  - ±0,2 mm untuk vektor, PNG, dan SVG;
  - ±0,5 mm untuk JPG, karena tepi JPG tidak tajam;
  - ±0,3 mm untuk halaman PDF di atas 400 mm (`pdf-large-a3.pdf`). Halaman sebesar itu diubah menjadi gambar paling besar 4000 piksel (±9,1 piksel per mm, bukan 10), dan piksel peralihan di tepinya (antialias) ikut terhitung: +2 piksel ≈ 0,22 mm.
- **Garis potong:** ±0,4 mm.
- **Sudut lancip dan tepi bergerigi** (segitiga p3, bintang, `png-rough-edge.png`): sesuai aturan penjaga jarak di [kebutuhan aplikasi](../../docs/kebutuhan-aplikasi.md), garis boleh 0,3 mm lebih dekat ke artwork, ditambah 1 piksel gambar bantu (mask), yaitu 0,17 mm. Jadi kotak garis boleh lebih kecil sampai 0,47 mm per sisi:
  - segitiga, lebar (dua sudut alas): kiss 43,1–44,4 mm, die 47,1–48,4 mm;
  - segitiga, tinggi (satu sudut puncak): kiss 38,5–39,4 mm, die 42,5–43,4 mm;
  - bintang dan logo bergerigi: lebar dan tinggi boleh kurang sampai 0,95 mm.
- **Garis tipis (p3):** ujung garis tipis adalah satu sudut lancip, jadi lebarnya boleh kurang sampai 0,5 mm.

## Hal lain yang harus benar

- **Garis luar, ditambah garis dalam untuk lubang asli yang tetap terbuka setelah offset** (stiker donat, sejak 2026-10-21). Setiap desain punya 1 garis kiss dan 1 garis die. Pengecualian: `pdf-irregular-shapes.pdf p4` punya 2 + 2, karena lingkaran ketiga berjarak 15 mm, lebih dari 2 × (offset + halus); `png-transparent.png` punya 2 garis kiss (lubang Ø 10 mm tetap terbuka) dan 1 garis die (lubang tertutup offset 4 + halus 2).
  - Rongga yang terbentuk saat offset menutup celah (cincin C) tidak dipotong.
  - Lingkaran di dalam `pdf-single-page.pdf` dan `svg-vector.svg` tidak dipotong.
  - Celah cincin C tertutup oleh offset + halus, dan lubangnya tidak dipotong.
- **Desain kotak** (masukan 2026-10-23). Artwork yang memenuhi kotaknya mendapat garis kotak bersudut tajam: 4 titik dengan garis lurus, sejauh offset dari kotak artwork, tanpa Halus, dan die minimal 1 piksel di luar kiss. File yang termasuk: `jpg-low-resolution.jpg`, `jpg-large-24mp.jpg`, `pdf-single-page.pdf` (persegi panjang penuh), `pdf-multi-page.pdf p2`, `pdf-rotated.pdf`, `pdf-encrypted.pdf`, dan `png-latar-*.png`. Ukuran garisnya tetap sama dengan tabel di atas; yang berubah hanya sudutnya, dari membulat menjadi tajam. Garis tepat di sudut berjarak offset × √2 dari sudut artwork. `svg-vector.svg` (sudut membulat, rx 8) bukan desain kotak.
- **`invalid-file.cdr`:**
  - muncul dialog "Gagal memuat";
  - aplikasi tidak crash, dan file lain yang ditambahkan bersamaan tetap masuk.

  Isi pesannya bergantung pada CorelDRAW:
  - PC tanpa CorelDRAW: `invalid-file.cdr: CorelDRAW tidak terpasang. Export file CDR ke PDF dulu, lalu tambahkan PDF-nya.`
  - PC dengan CorelDRAW: `CorelDRAW gagal membuat PDF. …`, atau setelah 30 detik `CorelDRAW tidak merespons dalam 30 detik. …`.
- **`cdr-normal.cdr`:** di PC tanpa CorelDRAW, hasilnya sama dengan `invalid-file.cdr`. File ini dibuat operator; lihat [cdr-normal.BACA.txt](../01-input/cdr-normal.BACA.txt).
- **Hanya tujuh file pertama** (`pdf-single-page.pdf`, `pdf-multi-page.pdf`, `ai-compatible.ai`, `png-transparent.png`, `jpg-low-resolution.jpg`, `svg-vector.svg`, `invalid-file.cdr`) ditambahkan sekaligus:
  - 8 desain;
  - status "8 stiker di 1 lembar.";
  - peringatan "1 desain resolusi rendah".
- **Semua file `01-input` ditambahkan sekaligus** (tanpa `cdr-normal.cdr`):
  - 19 desain;
  - status "18 stiker di 2 lembar.";
  - peringatan "1 lebih besar dari kertas   2 desain resolusi rendah   1 PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)";
  - pemakaian bahan "54% · sisa 46%";
  - judul lembar "4 pcs / lembar A3+ · 90%" dan "14 pcs / lembar A3+ · 17%". Sejak 2026-10-26 tanda registrasi aktif secara bawaan, jadi area 6 mm di sekitar tanda di pojok lembar dikosongkan; sebelumnya 5 pcs 92% dan 13 pcs 16%. Sebelum 2026-10-23, lembar 1 = 91%; sudut garis kotak yang tajam menambah sedikit luas.

  Asal angka-angka ini:
  - 2 lembar diketahui dari ukuran: artwork A3 hanya menyisakan jalur selebar 14 mm dan 34 mm, sehingga desain 54 × 54 mm butuh lembar kedua.
  - Persentase berasal dari skenario B-ui di validasi otomatis (`validasi.py`). Angka itu sama dengan hasil hitung ulang dari luas di dalam garis potong (selisih paling banyak 0,5 poin, kriteria B7).
- **Export PDF:**
  - 1 halaman 320 × 480 mm per lembar;
  - garis kiss magenta `0 1 0 0` dan garis die cyan `1 0 0 0` (CMYK);
  - artwork PDF/AI tetap vektor; PNG/JPG/SVG ditempel sebagai gambar.
- **File asli tidak berubah** setelah diuji. Jalankan `powershell -ExecutionPolicy Bypass -File cek-original.ps1`: semua baris bertulisan `sama`, dan perintah selesai tanpa galat (exit code 0).

## Cara mengecek

- **Ukuran artwork, jumlah desain, dan dpi:** lihat panel Desain (nama, lebar × tinggi, angka dpi). Status dan peringatan ada di bagian bawah jendela.
- **Ukuran garis potong:** export PDF, buka di CorelDRAW, klik garis magenta atau cyan, lalu baca ukuran W × H di bilah properti (property bar).
- Simpan hasil export di [03-exports](../03-exports) dan bukti (tangkapan layar, catatan, foto) di [04-evidence](../04-evidence).

## Hasil 2026-10-18 (versi rilis)

| Pengecekan | Hasil |
| --- | --- |
| Ukuran tiap file lewat mesin pemuat Printech | 19/19 desain benar. Bukti: [catatan ukuran](../04-evidence/2026-10-18-verifikasi-loader.txt) |
| Lewat aplikasi, semua file sekaligus | Dialog CDR muncul setelah 1,2 detik. Semua file selesai dimuat dan disusun 3,0 detik setelah aplikasi dibuka (2,5–4,0 detik pada 3 kali jalan). Status, peringatan, pemakaian bahan, dan judul lembar sama dengan harapan di atas. Aplikasi tetap responsif. Memori (angka Task Manager) 136–170 MB. Bukti: [catatan uji semua file](../04-evidence/2026-10-18-ui-semua-input.txt), [tangkapan layar](../04-evidence/2026-10-18-semua-input-printech.png) |
| File asli | 15/15 `sama` setelah semua uji. Tujuh file lama juga sama dengan sidik digitalnya sebelum file baru dibuat. |
| Kasus khusus (2026-10-19) | Uji file khusus (`ui-file-khusus.ps1`): 4 dialog "Gagal memuat", 1 desain masuk, status "1 stiker di 1 lembar.", aplikasi responsif. Pesan galat masih teknis (temuan D9). **`pdf-encrypted.pdf` GAGAL:** dimuat sebagai vektor, tetapi export PDF berhenti dengan galat dan seluruh export layout gagal (temuan D8). Bukti: [catatan uji file khusus](../04-evidence/2026-10-19-ui-file-khusus.txt), [catatan PDF terenkripsi](../04-evidence/2026-10-19-pdf-terenkripsi.txt) |
| Kasus khusus (2026-10-28) | Uji file khusus (`ui-file-khusus.ps1`): 4 dialog "Gagal memuat" dengan pesan untuk operator (D9 diperbaiki), 1 desain masuk, status "1 stiker di 1 lembar.", aplikasi responsif. `pdf-encrypted.pdf` ditempel sebagai gambar dengan peringatan. Bukti: [catatan uji file khusus](../04-evidence/2026-10-28-ui-file-khusus.txt) |
| Latar PNG (2026-10-21) | Sebelum perbaikan: latar abu muda dan pink pastel hilang, tinggal 5 % berupa cincin selebar offset kiss, dan logo jadi ±4× lebih besar (temuan D10). Sesudah perbaikan: kedua latar utuh di PDF export (102 % terukur) dan di layar; latar putih tetap dibuang. Bukti: [catatan latar PNG](../04-evidence/2026-10-21-png-latar.txt), [sebelum](../04-evidence/2026-10-21-png-latar-sebelum.png), [sesudah](../04-evidence/2026-10-21-png-latar-sesudah.png), [tampilan aplikasi](../04-evidence/2026-10-21-png-latar-ui.png) |
| PDF terenkripsi (2026-10-21) | Sesudah perbaikan D8: `pdf-encrypted.pdf` dimuat sebagai gambar 60,0 × 30,0 mm dengan peringatan "1 PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)"; export PDF jadi tanpa galat (validasi X3 lulus). Bukti: [catatan PDF terenkripsi](../04-evidence/2026-10-21-pdf-terenkripsi.txt), [tampilan aplikasi](../04-evidence/2026-10-21-pdf-terenkripsi-ui.png) |
| Memori untuk file besar (2026-10-21) | Sesudah perbaikan D4 (angka Task Manager): JPG 24 MP 41 MB, dua PDF besar 53 MB, semua 19 desain 84 MB. Target ±120 MB tercapai. Bukti: [catatan profil memori](../04-evidence/2026-10-21-ram-profil.txt) |
| Masukan leader tahap 1 (2026-10-21) | Latar gambar tanpa bagian transparan tidak dibuang otomatis, dan fitur Compact diganti "Penuhi lembar". Uji semua file lewat aplikasi: status "18 stiker di 2 lembar.", peringatan sama, pemakaian bahan 54 %, judul lembar 92 % dan 16 %, memori 87 MB. Validasi: skenario B-campuran dipenuhi menjadi 198 stiker di 4 lembar tanpa tabrakan (B2–B4 lulus). Bukti: [catatan uji semua file](../04-evidence/2026-10-21-ui-semua-input.txt), [catatan validasi](../04-evidence/2026-10-21-validasi-garis-potong-nesting.txt) |
| Masukan leader tahap 4 (2026-10-21) | Editor garis potong, `png-transparent.png` dengan kiss 2/1 dan die 4/2: editor terbuka dengan kiss 31 titik dan die 21 titik (garis otomatis); Simplify 0,5 mm membuat kiss 22 titik, die tetap 21; Urungkan/Ulangi di editor benar; Simpan memberi tanda "garis manual"; Edit > Urungkan mengembalikan garis otomatis. Uji semua file setelah tahap 4: angka sama dengan tahap 1, memori 81 MB. Menyeret titik dengan mouse belum diuji. Bukti: [catatan uji editor](../04-evidence/2026-10-21-ui-editor-garis.txt), [editor](../04-evidence/2026-10-21-editor-garis-potong.png), [garis manual](../04-evidence/2026-10-21-editor-garis-manual.png), [catatan uji semua file](../04-evidence/2026-10-21-ui-semua-input.txt) |
| Smart select (2026-10-22) | File donat dari pngtree milik user (1200 × 1200 piksel, lebar 50 mm, kiss 2/1; file tidak masuk repo): garis otomatis 1 garis selebar 49,51 mm karena watermark ikut terlingkari; smart select dengan klik di badan donat: 1 garis 44,03 mm (watermark tidak ikut, 26 ms); klik lubang tengah menambah garis dalam 1,36 mm (14 ms). Uji tampilan tanpa mouse 6/6 lulus. Bukti: [catatan smart select](../04-evidence/2026-10-22-smart-select.txt), [editor](../04-evidence/2026-10-22-editor-smart-select.png) |
| Validasi garis potong, susunan, dan export | Lihat [validasi garis potong dan susunan](validasi-garis-potong-susunan.md) |
