# Validasi garis potong, nesting, dan export: skenario dan kriteria lulus

Kriteria di dokumen ini ditetapkan sebelum validasi dijalankan. Kesalahan garis potong dan nesting langsung membuang bahan, jadi setiap kriteria diukur dengan angka, bukan hanya dilihat.

## Cara menjalankan

1. Build Release: `dotnet build -c Release` di folder `StickerLayout`.
2. `pwsh validasi.ps1`. Skrip ini menjalankan mesin Printech (Loader, Nest, Export) untuk setiap skenario. Hasil mentahnya disimpan di `03-exports/validasi/`: `validasi.json` dan satu PDF export per skenario.
3. `python validasi.py`. Skrip ini menilai hasil dan menulis laporan serta gambar bukti ke `04-evidence/`. Exit code 0 berarti semua lulus; 1 berarti ada yang gagal.

Penilai tidak memakai kode Printech. Bentuk artwork yang benar diambil dari file asli di `01-input`:

- **PDF/AI:** path vektor, dibaca dengan PyMuPDF.
- **PNG/JPG:** piksel, dengan aturan latar yang sama seperti Printech (alpha > 16; tanpa alpha: bukan putih < 240 bila minimal separuh tepi gambar putih ≥ 250, selain itu seluruh gambar).
- **SVG:** bentuk dari isi file.

Semua jarak dalam mm. Garis potong diambil sampelnya setiap 0,05 mm, lalu jarak tiap titik diukur tepat ke tepi artwork.

## A. Garis potong

### Artwork yang diuji

| Jenis | File |
| --- | --- |
| Sederhana (lingkaran, persegi) | `pdf-multi-page.pdf` p1 (lingkaran) dan p2 (persegi panjang), `pdf-single-page.pdf`, `ai-compatible.ai`, `svg-vector.svg`, `png-152dpi.png`, `jpg-low-resolution.jpg`; besar: `pdf-large-a3.pdf`, `pdf-oversize.pdf`, `jpg-large-24mp.jpg` |
| Tepi tidak beraturan | `png-rough-edge.png` (logo bertepi bergelombang), `pdf-irregular-shapes.pdf` p1 (bintang), p3 (garis tipis 0,2 mm), `pdf-multi-page.pdf` p3 (segitiga lancip) |
| Berlubang | `png-transparent.png` (lubang tertutup), `pdf-irregular-shapes.pdf` p2 (cincin C, celah 5 mm) |
| Elemen berdekatan | `pdf-irregular-shapes.pdf` p4 (tiga lingkaran, celah 4 mm dan 15 mm) |

### Pengaturan

| Skenario | Kiss offset/halus | Die offset/halus |
| --- | --- | --- |
| A-standar | 2 / 1 | 4 / 2 |
| A-offset-kecil | 1 / 1 | 3 / 1 |
| A-offset-besar | 3 / 1 | 6 / 2 |
| A-halus-0, -0.5, -1, -2 | 2 / 0; 0,5; 1; 2 | 4 / sama dengan kiss |
| A-tak-biasa | 2 / 3 | 4 / 0 |
| A-hanya-kiss, A-hanya-die, A-tanpa-garis | satu garis saja, atau keduanya mati | |

### Kriteria garis potong

| ID | Pemeriksaan | Lulus bila |
| --- | --- | --- |
| A1 | Offset sesuai nilai yang dipilih | Pada bentuk sederhana dengan offset kiss 1/2/3 dan die 3/4/6: median jarak garis ke artwork = offset ± 0,25 mm, dan jarak terjauh tidak lebih dari offset + 0,5 mm. Toleransi ini menampung resolusi mask 6 px/mm (0,17 mm) dan piksel gambar sumber. |
| A2a | Halus benar-benar mengubah kontur | Pada logo bertepi kasar dan bintang (kiss dan die): panjang garis selalu turun dari Halus 0 → 0,5 → 1 → 2, karena lekukan kecil ditutup. Node Halus 1 dan 2 lebih sedikit dari Halus 0. |
| A2b | Target reduksi node | Kiss logo kasar: node Halus 1 ≤ 40% dari Halus 0 (target PRD dan S1: turun ≥ 60%). |
| A3 | Die membungkus kiss | Tidak ada titik garis kiss di luar garis die pada semua desain dan pengaturan, termasuk kiss halus 3 + die halus 0. |
| A4 | Jarak minimum ke artwork | Pada setiap garis: jarak terdekat ke artwork ≥ offset − 0,3 mm. Tanpa toleransi tambahan. |
| A5 | Reduksi node tidak merusak bentuk | Garis hanya berisi kurva, tanpa garis lurus atau sudut tajam. Jumlah kontur benar: 1 per desain; untuk elemen berdekatan, bagian dengan celah < 2 × (offset + halus) menyatu. Lubang tertutup tidak dipotong. Cincin C: celahnya ditutup bila 5 mm < 2 × (offset + halus). Garis bentuk sederhana tetap di dalam pita [offset − 0,3; offset + 0,5]. Gambar overlay disimpan untuk dicek mata. |
| A6 | Opsi sesuai pilihan operator | Kiss saja: hanya garis magenta (CMYK 0 1 0 0). Die saja: hanya garis cyan (1 0 0 0). Keduanya: dua garis per stiker. Keduanya mati: muncul pesan, tidak ada desain. Dicek di data dan di PDF export. |
| E1 | PDF export = data yang dinilai | Di setiap PDF export, garis potong, gambar, dan path artwork berada di posisi dan rotasi yang sama dengan data (selisih ≤ 0,05 mm). Jumlahnya juga sama. |

## B. Nesting (A3+ 320 × 480 mm, margin 10 mm, jarak 2 mm)

### Skenario

| Skenario | Isi |
| --- | --- |
| B-sedikit | 3 stiker (2 desain) |
| B-campuran | 160 stiker dari 16 desain berukuran campuran, kiss + die |
| B-tidak-beraturan | 150 stiker bentuk tidak beraturan (bintang, cincin, garis tipis, tiga lingkaran, logo kasar, segitiga), bleed 1 mm |
| B-banyak-lembar | 2 artwork A3 + 150 lingkaran + 1 artwork lebih besar dari kertas |
| B-rotasi | 3 desain tidak simetris, masing-masing di 0°, 90°, 180°, 270°, diletakkan manual |
| B-ui | Semua file `01-input` dengan pengaturan bawaan aplikasi. Angkanya dicocokkan dengan layar Printech. |

### Kriteria nesting

| ID | Pemeriksaan | Lulus bila |
| --- | --- | --- |
| B1 | Jumlah stiker | Per desain, jumlah terpasang = jumlah salinan, kecuali desain yang memang lebih besar dari area cetak (0 terpasang, dihitung "tidak muat"). Terpasang + tidak muat = diminta. |
| B2 | Tabrakan | Tidak ada pasangan garis potong terluar yang bersinggungan atau saling memuat. Setiap pasangan yang kotaknya berjarak < 5 mm dicek dengan geometri asli garis. |
| B3 | Jarak antarelemen | Jarak terdekat antar garis potong terluar stiker berbeda ≥ 2,0 mm. |
| B4 | Margin | Semua titik garis potong terluar ≥ 10 mm dari tepi kertas. Untuk stiker dengan bleed, garis yang diberi bleed + bleed juga ≥ 10 mm. |
| B5 | Rotasi | Pada B-rotasi, jarak garis kiss ke artwork diukur langsung dari PDF export. Selisih antar 4 rotasi ≤ 0,02 mm, dan selisih terhadap desain yang sama tanpa rotasi ≤ 0,02 mm. Printech tidak menandai stiker merah. |
| B6 | Banyak lembar | Jumlah lembar ≥ batas bawah luas (total luas ÷ area cetak), tidak ada lembar kosong, dan tidak semua stiker lembar terakhir bisa dipindah ke lembar sebelumnya. Yang terakhir dicoba dengan `Nest.Snap` di 4 rotasi. |
| B7 | Efisiensi bahan | Pemakaian bahan per lembar dari Printech sama dengan luas di dalam garis potong terluar ÷ area cetak, dihitung ulang dari garis (selisih ≤ 0,5 poin persen). Jumlah pcs per lembar sama dengan jumlah garis di PDF. |

## X. Hasil export (tahap keempat)

Semua dicek dari file hasil export (PDF produksi, preview JPG, Print Ticket), bukan dari tampilan canvas. Warna diperiksa dengan dua cara:

- **Preflight isi PDF:** membaca operator warna langsung dari content stream.
- **Separasi CMYK:** halaman dirender MuPDF ke CMYK tanpa antialias, lalu nilai pelat C/M/Y/K dibaca di titik-titik garis potong.

Skenario: semua PDF di atas. Preview dan Print Ticket dibuat untuk skenario B-*. Lembar terakhir B-campuran dipadatkan ("Compact lembar terakhir"). Tambahan **X-pdf-diputar** berisi `pdf-rotated.pdf` (halaman /Rotate 90), `pdf-single-page.pdf`, dan `khusus/pdf-encrypted.pdf` (sejak 2026-10-21, temuan D8).

| ID | Output | Lulus bila |
| --- | --- | --- |
| X1 | PDF produksi | Jumlah halaman = jumlah lembar. Ukuran halaman = lebar kertas × tinggi lembar (termasuk lembar yang dipadatkan), ±0,01 mm. Posisi artwork sesuai layout (E1, ±0,05 mm). Ukuran desain di PDF sesuai file asli, ±0,05 mm: path vektor sama dengan path di file input; gambar = lebar yang diminta (50 mm). |
| X2 | Warna garis potong | Preflight: setiap garis yang di-stroke memakai DeviceCMYK `0 1 0 0 K` (kiss) atau `1 0 0 0 K` (die); jumlahnya = jumlah garis kiss/die per lembar; tidak ada operator RGB, ICC, Separation, atau `CS`/`SCN`; tidak ada OutputIntent. Separasi: di setiap titik sampel, garis kiss tepat C 0 M 100 Y 0 K 0, dan garis die tepat C 100 M 0 Y 0 K 0. |
| X3 | Vektor | Desain PDF/AI tetap vektor: digambar sebagai Form XObject, warna isi CMYK sama persis dengan file input, tanpa gambar. Gambar hanya untuk PNG/JPG/SVG, dan untuk PDF yang memang tidak bisa vektor (halaman diputar, atau PDF terenkripsi yang gagal ditempel PDFsharp). PDF yang jadi gambar wajib memunculkan peringatan, sesuai PRD, dan export tetap jadi. |
| X4 | Preview JPG | Satu file JPEG per lembar. Sisi panjang 2000 px (±1). Perbandingan sisi = ukuran lembar (±1 px). Di setiap posisi stiker, preview dan PDF sama-sama berisi artwork (cocok ≥ 95%). |
| X5 | Print Ticket | PDF 1 halaman A4 (210 × 297 mm, ±0,5). Isinya: total lembar, total stiker, efisiensi bahan (±1 poin dari hitung ulang), rincian per lembar (pcs dan bahan) dan per desain (pcs), serta pengaturan potong (kiss/die, jarak, bleed). Semua angka sesuai layout. |

## Catatan dokumentasi

- **Ukuran grid:** kode memakai grid nesting 1 mm (`Nest.Cell = 1`, [Core.cs](../../StickerLayout/Core.cs)). Angka "grid 2 mm" berasal dari versi sebelum 2026-10-18.
- **Uji tabrakan sebelumnya:** `Printech-Deskripsi.pdf` versi sekarang (di luar repo) menulis "grid 1 mm" dan "0 tabrakan dari 236 pasangan".

## Hasil 2026-10-18 (build Release)

Laporan lengkap: [2026-10-18-validasi-garis-potong-nesting.txt](../04-evidence/2026-10-18-validasi-garis-potong-nesting.txt). Gambar bukti:

- [overlay garis potong](../04-evidence/2026-10-18-validasi-garis-potong.png): artwork abu-abu, kiss magenta, die cyan;
- [pengaruh Halus](../04-evidence/2026-10-18-validasi-halus.png);
- [lembar 1 B-campuran](../04-evidence/2026-10-18-validasi-B-campuran-lembar1.png);
- [lembar 1 B-tidak-beraturan](../04-evidence/2026-10-18-validasi-B-tidak-beraturan-lembar1.png).

| ID | Hasil | Angka |
| --- | --- | --- |
| A1 | Lulus | 48 garis bentuk sederhana: median − offset −0,19 s.d. +0,02 mm; jarak terjauh maksimal offset + 0,19 mm |
| A2a | Lulus | Panjang garis selalu turun seiring Halus naik. Node pada Halus 0 → 1: logo kasar kiss 84 → 39, die 59 → 35; bintang kiss 27 → 16, die 21 → 16 |
| A2b | **Gagal** | Kiss logo kasar turun 54% (target ≥ 60%). `rough.jpg` di selfcheck S1 tetap turun 74%. Logo uji baru punya gelombang nyata 0,9 mm setiap ±7,6 mm yang memang harus diikuti garis, jadi node tidak bisa turun sebanyak itu. Target PRD bergantung pada jenis logo. |
| A3 | Lulus | 75 desain × pengaturan: 0 titik kiss di luar die; jarak kiss–die terkecil 0,11 mm |
| A4 | **Gagal** | 63 dari 158 garis sedikit di bawah batas. Terburuk: 0,13 mm di bawah batas (die 4 mm di puncak segitiga 60°: 3,57 mm). Hanya 5 garis yang lebih dari 0,1 mm di bawah batas. Sebelum perbaikan: 79 garis, terburuk 0,29 mm di bawah batas. |
| A5 | **Gagal** (hanya karena A4) | 0 garis lurus atau sudut. Jumlah kontur 100% benar, termasuk tiga lingkaran (2 kontur). Lubang benar: celah cincin C tertutup bila 5 < 2 × (offset + halus), dan terbuka pada kiss 1/1 dan Halus 0 sesuai aturan. Yang gagal hanya batas bawah pita, yaitu A4. |
| A6 | Lulus | Kiss saja: 4 magenta, 0 cyan. Die saja: 0 magenta, 4 cyan. Keduanya: 17 + 17. Keduanya mati: "Aktifkan minimal satu garis potong." |
| E1 | Lulus | 17 PDF export: posisi dan rotasi garis serta artwork sama dengan data (selisih ≤ 0,020 mm) |
| B1 | Lulus | Semua skenario: terpasang + tidak muat = diminta (contoh: B-banyak-lembar 152 + 1 = 153) |
| B2 | Lulus | 0 tabrakan dari 997 pasangan yang dicek (803 berjarak < 5 mm) |
| B3 | Lulus | Jarak terkecil 2,50 mm. Median jarak ke tetangga terdekat 3,11 mm, maksimal 4,28 mm |
| B4 | Lulus | Margin terkecil 10,44 mm, termasuk bleed |
| B5 | Lulus | Selisih jarak garis–artwork antar 4 rotasi 0,000 mm (dari PDF export). Susun otomatis memakai 0°/90°/180°/270°: 410/10/53/10 |
| B6 | Lulus | Tidak ada stiker lembar terakhir yang muat di lembar sebelumnya; tidak ada lembar kosong |
| B7 | Lulus | Selisih pemakaian bahan Printech vs hitung ulang ≤ 0,41 poin persen. Angka B-ui sama dengan layar: "18 stiker di 2 lembar.", "54% · sisa 46%", 91% / 16% per lembar |
| X1 | Lulus | 17 PDF produksi: jumlah dan ukuran halaman sesuai lembar, termasuk lembar terakhir B-campuran yang dipadatkan. 676 artwork diukur ulang dari PDF: ukurannya sama dengan file asli atau lebar yang diminta (≤ 0,05 mm). |
| X2 | Lulus | Preflight 26 halaman: 576 garis `0 1 0 0 K` dan 253 garis `1 0 0 0 K`; 0 warna stroke lain; 0 operator RGB/ICC/Separation; 0 OutputIntent. Separasi CMYK: kiss 625/625 titik tepat C0 M100 Y0 K0; die 375/375 titik tepat C100 M0 Y0 K0. Gambar pelat: [separasi-cmyk](../04-evidence/2026-10-18-validasi-separasi-cmyk.png) |
| X3 | Lulus (setelah perbaikan 3) | 459 penempatan PDF/AI tetap vektor (Form XObject), warna isi CMYK sama persis dengan file input. Hanya `pdf-rotated.pdf` yang menjadi gambar, dan sekarang muncul peringatan. SVG menjadi gambar sesuai PRD. |
| X4 | Lulus | 15 preview JPG untuk 15 lembar: JPEG, sisi panjang 2000 px, proporsi sesuai lembar. Isi di posisi stiker sama dengan PDF pada 495/495 stiker. |
| X5 | Lulus | 6 Print Ticket: 1 halaman A4. Total lembar, total stiker, efisiensi, per lembar, per desain, dan pengaturan potong sesuai layout. |

### Perbaikan mesin dari validasi ini ([Core.cs](../../StickerLayout/Core.cs))

1. **Jari-jari penjaga jarak dibulatkan ke atas, bukan ke bawah.** Dibulatkan ke bawah, penjaga sudah 0,03 mm lebih dekat dari aturan sebelum kurva Potrace. A4 terburuk membaik dari −0,29 menjadi −0,13 mm. Selfcheck tetap 15/15. Node `rough.jpg` S1: kiss 16 → 17, die 13 → 16.
2. **Die minimal 1 piksel (0,17 mm) di luar kiss.** Setelah perbaikan 1, pada pengaturan tak biasa (kiss halus 3, die halus 0), garis kiss menembus die 0,023 mm. Di area itu bentuk die sama persis dengan kiss, dan Potrace mencocokkan kurvanya sedikit berbeda. Sekarang 0 titik di luar.
3. **Peringatan PDF yang ditempel sebagai gambar** (`Sticker.PdfAsImage`, [MainWindow.xaml.cs](../../StickerLayout/MainWindow.xaml.cs), [Strings.cs](../../StickerLayout/Strings.cs)).
   - PRD menulis bahwa PDF dengan halaman diputar atau yang tidak terbaca PDFsharp ditempel sebagai gambar, dan aplikasi memberi peringatan.
   - Ternyata peringatan hanya ada saat impor PDFsharp gagal ketika export. PDF ber-/Rotate diam-diam menjadi gambar RGB 254 dpi.
   - Sekarang status bar menampilkan "1 PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)". Dicek di uji UI.

### Perlu keputusan

- **A4: sisa ≤ 0,13 mm berasal dari resolusi mask 6 px/mm dan penghalusan kurva Potrace, bukan dari penjaga.**
  - Penjaga dicoba sama dengan offset penuh. Hasilnya: A4 tetap gagal (−0,08 mm, karena piksel JPG 0,25 mm), dan reduksi node turun ke 40–45% sehingga S1 dan S2 gagal.
  - Pilihan:
    - (a) terima toleransi dan tulis aturan sebagai "≥ offset − 0,3 mm, galat raster sampai 0,15 mm";
    - (b) mask 10 px/mm, belum diuji: perkiraan CPU ±2,8× dan RAM lebih besar untuk stiker besar.
  - Saran: (a).
- **A2b:** target "node turun ≥ 60%" dipakai hanya untuk `rough.jpg` (S1), atau dibuat bergantung jenis logo?
