# Validasi garis potong, susunan, dan export: skenario dan syarat lulus

Syarat di dokumen ini ditetapkan sebelum validasi dijalankan. Kesalahan garis potong dan susunan langsung membuang bahan, jadi setiap syarat diukur dengan angka, bukan hanya dilihat.

## Cara menjalankan

1. Build aplikasi versi rilis: `dotnet build -c Release` di folder `StickerLayout`.
2. Jalankan `pwsh validasi.ps1`. Skrip ini menjalankan mesin Printech (bagian pemuat, penyusun, dan export) untuk setiap skenario. Hasil mentahnya disimpan di `03-exports/validasi/`: `validasi.json` dan satu PDF export per skenario.
3. Jalankan `python validasi.py`. Skrip ini menilai hasilnya, lalu menulis laporan dan gambar bukti ke `04-evidence/`. Exit code 0 berarti semua lulus; 1 berarti ada yang gagal.

Penilai (`validasi.py`) tidak memakai kode Printech. Bentuk artwork yang benar diambil langsung dari file asli di `01-input`:

- **PDF/AI:** garis vektor (path), dibaca dengan pustaka PyMuPDF.
- **PNG/JPG:** piksel, dengan aturan latar yang sama seperti Printech: artwork = piksel yang tingkat tidak-transparannya (alpha) di atas 16, jadi gambar tanpa bagian transparan = seluruh gambar. Latar tidak dibuang otomatis sejak 2026-10-21.
- **SVG:** bentuk dari isi file.

Semua jarak dalam mm. Garis potong diambil titiknya setiap 0,05 mm, lalu jarak setiap titik diukur tepat ke tepi artwork.

## A. Garis potong

### Artwork yang diuji

| Jenis | File |
| --- | --- |
| Sederhana (lingkaran, persegi) | `pdf-multi-page.pdf` p1 (lingkaran) dan p2 (persegi panjang), `pdf-single-page.pdf`, `ai-compatible.ai`, `svg-vector.svg`, `png-152dpi.png`, `jpg-low-resolution.jpg`; besar: `pdf-large-a3.pdf`, `pdf-oversize.pdf`, `jpg-large-24mp.jpg` |
| Tepi tidak beraturan | `png-rough-edge.png` (logo bertepi bergelombang), `pdf-irregular-shapes.pdf` p1 (bintang), p3 (garis tipis 0,2 mm), `pdf-multi-page.pdf` p3 (segitiga lancip) |
| Berlubang | `png-transparent.png` (lubang asli Ø 10 mm, dikelilingi penuh oleh artwork), `pdf-irregular-shapes.pdf` p2 (cincin C, celah 5 mm) |
| Bagian yang berdekatan | `pdf-irregular-shapes.pdf` p4 (tiga lingkaran, celah 4 mm dan 15 mm) |

### Pengaturan

| Skenario | Kiss offset/halus | Die offset/halus |
| --- | --- | --- |
| A-standar | 2 / 1 | 4 / 2 |
| A-offset-kecil | 1 / 1 | 3 / 1 |
| A-offset-besar | 3 / 1 | 6 / 2 |
| A-halus-0, -0.5, -1, -2 | 2 / 0; 0,5; 1; 2 | 4 / sama dengan kiss |
| A-tak-biasa | 2 / 3 | 4 / 0 |
| A-hanya-kiss, A-hanya-die, A-tanpa-garis | satu garis saja, atau keduanya mati | |

### Syarat lulus garis potong

| Kode | Pemeriksaan | Lulus bila |
| --- | --- | --- |
| A1 | Offset sesuai nilai yang dipilih | Pada bentuk sederhana dengan offset kiss 1/2/3 dan die 3/4/6: nilai tengah (median) jarak garis ke artwork = offset ± 0,25 mm, dan jarak terjauh tidak lebih dari offset + 0,5 mm. Batas ini menampung ketelitian gambar bantu (mask) 6 piksel per mm (0,17 mm) dan piksel gambar sumber. Desain kotak (A7, sejak 2026-10-23): jarak terjauh paling banyak offset × √2 + 0,5 mm, karena sudut garis kotak berjarak offset × √2 dari sudut artwork. |
| A2a | Halus benar-benar mengubah garis | Pada logo bertepi kasar dan bintang (kiss dan die): panjang garis selalu turun dari Halus 0 ke 0,5, 1, lalu 2, karena lekukan kecil ditutup. Jumlah titik (node) pada Halus 1 dan 2 lebih sedikit daripada Halus 0. |
| A2b | Pengurangan titik pada logo bergelombang (info) | Hanya dicatat, bukan syarat lulus: berapa persen titik kiss `png-rough-edge.png` berkurang dari Halus 0 ke Halus 1. Target turun minimal 60% hanya berlaku untuk logo kasar seperti `rough.jpg` di pemeriksaan otomatis S1 (keputusan D5, 2026-10-27). |
| A3 | Die membungkus kiss | Tidak ada titik garis kiss di luar garis die, pada semua desain dan pengaturan, termasuk kiss halus 3 + die halus 0. |
| A4 | Jarak terdekat ke artwork | Pada setiap garis: jarak terdekat ke artwork minimal offset − 0,3 mm, dengan toleransi galat kurva 0,15 mm, jadi batas ujinya offset − 0,45 mm (keputusan D3, 2026-10-27). Jumlah garis yang di bawah offset − 0,3 mm tetap dilaporkan. |
| A5 | Pengurangan titik tidak merusak bentuk | Garis hanya berisi kurva, tanpa garis lurus atau sudut tajam. Jumlah garis tertutup (kontur) benar: 1 per desain; untuk bagian yang berdekatan, bagian dengan celah kurang dari 2 × (offset + halus) menyatu. Lubang asli yang tetap terbuka setelah offset + halus ikut dipotong (garis dalam, sejak 2026-10-21), dan lubang die ikut tertutup bila lubang kiss tertutup (die membungkus kiss). Cincin C: celahnya tertutup bila 5 mm < 2 × (offset + halus). Garis bentuk sederhana tetap di dalam rentang [offset − 0,3; offset + 0,5]; batas bawahnya memakai toleransi 0,15 mm seperti A4. Desain kotak (A7) memang terdiri dari garis lurus, jadi tidak dihitung pada aturan "tanpa garis lurus", dan batas atas rentangnya offset × √2 + 0,5. Gambar tumpang (overlay) disimpan untuk dicek dengan mata. |
| A6 | Pilihan garis sesuai pilihan operator | Kiss saja: hanya garis magenta (CMYK 0 1 0 0). Die saja: hanya garis cyan (1 0 0 0). Keduanya: dua garis per stiker. Keduanya mati: muncul pesan, tidak ada desain. Dicek di data dan di PDF export. |
| A7 | Desain kotak (masukan 2026-10-23) | Desain yang artwork-nya memenuhi kotak dinilai di sini. Syaratnya: semua piksel tepi tidak transparan (alpha di atas 16) dan isinya minimal 99,5 %. PDF/AI diubah menjadi gambar 6 piksel per mm berlatar transparan oleh PyMuPDF, lalu dihitung sendiri oleh `validasi.py`. Garis kiss dan die desain ini harus berupa 1 garis tertutup dengan 4 garis lurus. Kotaknya = kotak batas artwork (bbox) ± offset (die minimal offset kiss + 1/6 mm), dengan selisih paling banyak 0,15 mm, juga pada Halus 0–2. |
| E1 | PDF export = data yang dinilai | Di setiap PDF export, garis potong, gambar, dan garis vektor artwork berada di posisi dan rotasi yang sama dengan data (selisih paling banyak 0,05 mm). Jumlahnya juga sama. |

## B. Susun otomatis (A3+ 320 × 480 mm, margin 10 mm, jarak 2 mm)

### Skenario

| Skenario | Isi |
| --- | --- |
| B-sedikit | 3 stiker (2 desain) |
| B-campuran | 160 stiker dari 16 desain berukuran campuran, kiss + die |
| B-tidak-beraturan | 150 stiker berbentuk tidak beraturan (bintang, cincin, garis tipis, tiga lingkaran, logo kasar, segitiga), bleed 1 mm |
| B-banyak-lembar | 2 artwork A3 + 150 lingkaran + 1 artwork yang lebih besar dari kertas |
| B-rotasi | 3 desain tidak simetris, masing-masing diputar 0°, 90°, 180°, dan 270°, diletakkan manual |
| B-ui | Semua file `01-input` dengan pengaturan bawaan aplikasi. Angkanya dicocokkan dengan yang tampil di layar Printech. |

### Syarat lulus susun otomatis

| Kode | Pemeriksaan | Lulus bila |
| --- | --- | --- |
| B1 | Jumlah stiker | Per desain, jumlah yang terpasang = jumlah salinan, kecuali desain yang memang lebih besar dari area cetak (0 terpasang, dihitung "tidak muat"). Terpasang + tidak muat = yang diminta. |
| B2 | Tabrakan | Tidak ada pasangan garis potong terluar yang bersinggungan atau saling memuat. Setiap pasangan yang kotaknya berjarak kurang dari 5 mm dicek dengan bentuk asli garisnya. |
| B3 | Jarak antar stiker | Jarak terdekat antar garis potong terluar dari stiker yang berbeda minimal 2,0 mm. |
| B4 | Margin | Semua titik garis potong terluar minimal 10 mm dari tepi kertas. Untuk stiker dengan bleed, garis yang diberi bleed + bleed juga minimal 10 mm. |
| B5 | Rotasi | Pada B-rotasi, jarak garis kiss ke artwork diukur langsung dari PDF export. Selisih antar 4 rotasi paling banyak 0,02 mm, dan selisih terhadap desain yang sama tanpa rotasi paling banyak 0,02 mm. Printech tidak menandai stiker merah. |
| B6 | Banyak lembar | Jumlah lembar minimal batas bawah dari luas (total luas ÷ area cetak), tidak ada lembar kosong, dan tidak semua stiker di lembar terakhir bisa dipindah ke lembar sebelumnya. Yang terakhir dicoba dengan penempatan otomatis (snap) di 4 rotasi. |
| B7 | Pemakaian bahan | Pemakaian bahan per lembar dari Printech sama dengan luas di dalam garis potong terluar ÷ area cetak, dihitung ulang dari garis (selisih paling banyak 0,5 poin persen). Jumlah pcs per lembar sama dengan jumlah garis di PDF. |

## X. Hasil export (tahap keempat)

Semua dicek dari file hasil export (PDF produksi, preview JPG, Print Ticket), bukan dari tampilan di layar. Warna diperiksa dengan dua cara:

- **Pemeriksaan isi PDF (preflight):** perintah warna dibaca langsung dari isi PDF.
- **Pisah warna CMYK (separasi):** halaman diubah menjadi gambar CMYK tanpa penghalusan tepi (antialias) oleh MuPDF, lalu nilai tiap pelat warna (C, M, Y, K) dibaca di titik-titik garis potong.

Skenario: semua PDF di atas. Preview dan Print Ticket dibuat untuk skenario B. Lembar B-campuran dipenuhi dengan tombol "Penuhi lembar" (dulu bernama Compact, sejak 2026-10-21; sebelumnya lembar terakhir dipadatkan). Tambahan **X-pdf-diputar** berisi `pdf-rotated.pdf` (halaman diputar 90°, /Rotate 90), `pdf-single-page.pdf`, dan `khusus/pdf-encrypted.pdf` (sejak 2026-10-21, temuan D8).

| Kode | Hasil | Lulus bila |
| --- | --- | --- |
| X1 | PDF produksi | Jumlah halaman = jumlah lembar. Ukuran halaman = lebar kertas × tinggi lembar, ±0,01 mm. Posisi artwork sesuai layout (E1, ±0,05 mm). Ukuran desain di PDF sesuai file asli, ±0,05 mm: garis vektor sama dengan garis di file input; gambar = lebar yang diminta (50 mm). |
| X2 | Warna garis potong | Pemeriksaan isi PDF: setiap garis memakai warna CMYK langsung (DeviceCMYK) `0 1 0 0 K` (kiss) atau `1 0 0 0 K` (die); jumlahnya = jumlah garis kiss/die per lembar; tidak ada warna RGB, profil warna (ICC), warna khusus (Separation), atau perintah warna lain (`CS`/`SCN`); tidak ada profil output (OutputIntent). Pisah warna: di setiap titik sampel, garis kiss tepat C 0 M 100 Y 0 K 0, dan garis die tepat C 100 M 0 Y 0 K 0. |
| X3 | Vektor | Desain PDF/AI tetap vektor: ditempel sebagai objek vektor (Form XObject), warna isi CMYK sama persis dengan file input, tanpa gambar. Gambar hanya untuk PNG/JPG/SVG, dan untuk PDF yang memang tidak bisa vektor (halaman diputar, atau PDF terenkripsi yang gagal ditempel oleh pustaka PDFsharp). PDF yang menjadi gambar wajib memunculkan peringatan, sesuai [kebutuhan aplikasi](../../docs/kebutuhan-aplikasi.md), dan export tetap jadi. |
| X4 | Preview JPG | Satu file JPEG per lembar. Sisi panjang 2000 piksel (±1). Perbandingan sisi = ukuran lembar (±1 piksel). Di setiap posisi stiker, preview dan PDF sama-sama berisi artwork (cocok minimal 95%). |
| X5 | Print Ticket | PDF 1 halaman A4 (210 × 297 mm, ±0,5). Isinya: total lembar, total stiker, efisiensi bahan (±1 poin dari hitung ulang), rincian per lembar (pcs dan bahan) dan per desain (pcs), serta pengaturan potong (kiss/die, jarak, bleed). Semua angka sesuai layout. |

## Catatan dokumentasi

- **Ukuran kotak bantu (grid):** susun otomatis di kode memakai grid 1 mm (`Nest.Cell = 1` di [kode mesin](../../StickerLayout/Core.cs)). Angka "grid 2 mm" berasal dari versi sebelum 2026-10-18.
- **Uji tabrakan sebelumnya:** `Printech-Deskripsi.pdf` versi sekarang (di luar repo) menulis "grid 1 mm" dan "0 tabrakan dari 236 pasangan".

## Hasil 2026-10-27 (keputusan D3 dan D5)

Laporan lengkap: [catatan validasi keputusan D3 dan D5](../04-evidence/2026-10-27-validasi-keputusan-d3-d5.txt). Hasil export-nya sama dengan [catatan validasi 2026-10-27](../04-evidence/2026-10-27-validasi-garis-potong-nesting.txt), sesudah perbaikan ujung tajam. Yang berubah hanya syarat A2b, A4, dan A5, sesuai keputusan user.

| Kode | Hasil | Angka |
| --- | --- | --- |
| A2b | Info | Titik kiss logo bergelombang berkurang 54%. `rough.jpg` di pemeriksaan otomatis S1 berkurang 74% (target minimal 60%) |
| A4 | Lulus | 158 dari 158 garis di atas batas dengan toleransi. 43 garis di bawah offset − 0,3 mm, terdalam 0,13 mm. Terdekat: die `pdf-multi-page.pdf p3` 3,57 mm (batas 3,70, dengan toleransi 3,55) |
| A5 | Lulus | 0 garis keluar rentang (sebelumnya 5, semuanya di batas bawah = A4) |
| Lainnya | Lulus | Sama dengan sebelumnya |

`validasi.py` selesai dengan exit code 0.

## Hasil 2026-10-23 (versi rilis, sesudah desain kotak otomatis)

Laporan lengkap: [catatan validasi 2026-10-23](../04-evidence/2026-10-23-validasi-garis-potong-nesting.txt).

Ada 4 desain kotak di skenario A: `jpg-large-24mp.jpg`, `jpg-low-resolution.jpg`, `pdf-multi-page.pdf p2`, dan `pdf-single-page.pdf`. Validasi menemukan keempatnya sendiri, dan hasilnya sama dengan Printech. Syarat lain tidak berubah. Garis desain yang bukan kotak tidak berubah: `validasi.json` hanya berbeda pada desain kotak dan pada hasil penuhi lembar B-campuran yang ikut bergeser.

| Kode | Hasil | Angka |
| --- | --- | --- |
| A1 | Lulus | Median − offset −0,16 s.d. +0,02 mm. Jarak terjauh paling banyak 0,13 mm di atas harapan (harapan = offset; sudut desain kotak = offset × √2) |
| A2a, A3, A6 | Lulus | Sama dengan sebelumnya |
| A2b | **Gagal** | Sama: titik kiss logo kasar berkurang 54% (target minimal 60%) |
| A4 | **Gagal** | 43 dari 158 garis sedikit di bawah batas (sebelumnya 65). Desain kotak kini tepat sejauh offset. Terburuk tetap die `pdf-multi-page.pdf p3` 3,57 mm |
| A5 | **Gagal** (hanya batas bawah rentang, A4) | 0 garis lurus pada 136 garis bukan kotak. Garis tertutup dan lubang 100% benar. Keluar rentang 5 garis (sebelumnya 27): `pdf-large-a3`, `pdf-oversize` (kiss dan die), `svg-vector`, dan `png-152dpi` pada offset kecil |
| A7 | Lulus | 22 garis desain kotak (termasuk Halus 0–2): 4 garis lurus, kotak ± offset, selisih terbesar 0,10 mm |
| E1, B1–B7, X1–X5 | Lulus | B-ui sama dengan layar: "18 stiker di 2 lembar.", "54% · sisa 46%", 90% / 17% per lembar. Sejak 2026-10-26 B-ui menyalakan tanda registrasi seperti bawaan aplikasi (area 6 mm di sekitar tanda dikosongkan); sebelumnya 92% / 16%, dan 91% / 16% sebelum 2026-10-23 |

## Hasil 2026-10-18 (versi rilis)

Laporan lengkap: [catatan validasi 2026-10-18](../04-evidence/2026-10-18-validasi-garis-potong-nesting.txt). Gambar bukti:

- [gambar tumpang garis potong](../04-evidence/2026-10-18-validasi-garis-potong.png): artwork abu-abu, kiss magenta, die cyan;
- [pengaruh Halus](../04-evidence/2026-10-18-validasi-halus.png);
- [lembar 1 B-campuran](../04-evidence/2026-10-18-validasi-B-campuran-lembar1.png);
- [lembar 1 B-tidak-beraturan](../04-evidence/2026-10-18-validasi-B-tidak-beraturan-lembar1.png).

| Kode | Hasil | Angka |
| --- | --- | --- |
| A1 | Lulus | 48 garis bentuk sederhana: median − offset −0,19 s.d. +0,02 mm; jarak terjauh paling banyak offset + 0,19 mm |
| A2a | Lulus | Panjang garis selalu turun seiring Halus naik. Jumlah titik pada Halus 0 lalu 1: logo kasar kiss 84 → 39, die 59 → 35; bintang kiss 27 → 16, die 21 → 16 |
| A2b | **Gagal** | Titik kiss logo kasar berkurang 54% (target minimal 60%). `rough.jpg` di pemeriksaan otomatis S1 tetap berkurang 74%. Logo uji baru punya gelombang nyata 0,9 mm setiap ±7,6 mm yang memang harus diikuti garis, jadi titiknya tidak bisa berkurang sebanyak itu. Target di kebutuhan aplikasi bergantung pada jenis logo. |
| A3 | Lulus | 75 kombinasi desain dan pengaturan: 0 titik kiss di luar die; jarak kiss–die terkecil 0,11 mm |
| A4 | **Gagal** | 63 dari 158 garis sedikit di bawah batas. Terburuk: 0,13 mm di bawah batas (die 4 mm di puncak segitiga 60°: 3,57 mm). Hanya 5 garis yang lebih dari 0,1 mm di bawah batas. Sebelum perbaikan: 79 garis, terburuk 0,29 mm di bawah batas. |
| A5 | **Gagal** (hanya karena A4) | 0 garis lurus atau sudut. Jumlah garis tertutup 100% benar, termasuk tiga lingkaran (2 garis). Lubang benar: celah cincin C tertutup bila 5 < 2 × (offset + halus), dan terbuka pada kiss 1/1 dan Halus 0 sesuai aturan. Yang gagal hanya batas bawah rentang, yaitu A4. |
| A6 | Lulus | Kiss saja: 4 magenta, 0 cyan. Die saja: 0 magenta, 4 cyan. Keduanya: 17 + 17. Keduanya mati: "Aktifkan minimal satu garis potong." |
| E1 | Lulus | 17 PDF export: posisi dan rotasi garis serta artwork sama dengan data (selisih paling banyak 0,020 mm) |
| B1 | Lulus | Semua skenario: terpasang + tidak muat = yang diminta (contoh: B-banyak-lembar 152 + 1 = 153) |
| B2 | Lulus | 0 tabrakan dari 997 pasangan yang dicek (803 berjarak kurang dari 5 mm) |
| B3 | Lulus | Jarak terkecil 2,50 mm. Median jarak ke tetangga terdekat 3,11 mm, paling jauh 4,28 mm |
| B4 | Lulus | Margin terkecil 10,44 mm, termasuk bleed |
| B5 | Lulus | Selisih jarak garis–artwork antar 4 rotasi 0,000 mm (dari PDF export). Susun otomatis memakai 0°/90°/180°/270°: 410/10/53/10 |
| B6 | Lulus | Tidak ada stiker lembar terakhir yang muat di lembar sebelumnya; tidak ada lembar kosong |
| B7 | Lulus | Selisih pemakaian bahan Printech dengan hitung ulang paling banyak 0,41 poin persen. Angka B-ui sama dengan layar: "18 stiker di 2 lembar.", "54% · sisa 46%", 91% / 16% per lembar |
| X1 | Lulus | 17 PDF produksi: jumlah dan ukuran halaman sesuai lembar, termasuk lembar terakhir B-campuran yang dipadatkan. 676 artwork diukur ulang dari PDF: ukurannya sama dengan file asli atau lebar yang diminta (selisih paling banyak 0,05 mm). |
| X2 | Lulus | Pemeriksaan isi PDF, 26 halaman: 576 garis `0 1 0 0 K` dan 253 garis `1 0 0 0 K`; 0 warna garis lain; 0 warna RGB, profil warna (ICC), atau warna khusus (Separation); 0 profil output (OutputIntent). Pisah warna CMYK: kiss 625/625 titik tepat C0 M100 Y0 K0; die 375/375 titik tepat C100 M0 Y0 K0. Gambar pelat: [pisah warna CMYK](../04-evidence/2026-10-18-validasi-separasi-cmyk.png) |
| X3 | Lulus (setelah perbaikan 3) | 459 penempatan PDF/AI tetap vektor (objek vektor, Form XObject), warna isi CMYK sama persis dengan file input. Hanya `pdf-rotated.pdf` yang menjadi gambar, dan sekarang muncul peringatan. SVG menjadi gambar, sesuai kebutuhan aplikasi. |
| X4 | Lulus | 15 preview JPG untuk 15 lembar: JPEG, sisi panjang 2000 piksel, proporsi sesuai lembar. Isi di posisi stiker sama dengan PDF pada 495/495 stiker. |
| X5 | Lulus | 6 Print Ticket: 1 halaman A4. Total lembar, total stiker, efisiensi, per lembar, per desain, dan pengaturan potong sesuai layout. |

### Perbaikan mesin dari validasi ini ([kode mesin](../../StickerLayout/Core.cs))

1. **Jari-jari penjaga jarak dibulatkan ke atas, bukan ke bawah.** Saat dibulatkan ke bawah, penjaga sudah 0,03 mm lebih dekat dari aturan, bahkan sebelum garis diubah menjadi kurva oleh Potrace. Hasil A4 terburuk membaik dari −0,29 menjadi −0,13 mm. Pemeriksaan otomatis tetap 15/15. Jumlah titik `rough.jpg` di S1: kiss 16 → 17, die 13 → 16.
2. **Die minimal 1 piksel (0,17 mm) di luar kiss.** Setelah perbaikan 1, pada pengaturan tak biasa (kiss halus 3, die halus 0), garis kiss menembus die 0,023 mm. Di area itu bentuk die sama persis dengan kiss, dan Potrace membuat kurvanya sedikit berbeda. Sekarang 0 titik di luar.
3. **Peringatan untuk PDF yang ditempel sebagai gambar** ([kode jendela utama](../../StickerLayout/MainWindow.xaml.cs), [teks aplikasi](../../StickerLayout/Strings.cs)).
   - Kebutuhan aplikasi menulis bahwa PDF dengan halaman diputar, atau yang tidak terbaca pustaka PDFsharp, ditempel sebagai gambar, dan aplikasi memberi peringatan.
   - Ternyata peringatan hanya muncul saat PDFsharp gagal ketika export. PDF dengan halaman diputar (/Rotate) diam-diam menjadi gambar RGB 254 dpi.
   - Sekarang baris status menampilkan "1 PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)". Dicek lewat uji tampilan.

### Perlu keputusan

Diputuskan 2026-10-27: A4 memakai pilihan (a), toleransi 0,15 mm; target A2b hanya untuk `rough.jpg` (S1). Hasilnya ada di bagian "Hasil 2026-10-27".

- **A4: sisa selisih paling banyak 0,13 mm berasal dari ketelitian gambar bantu (mask) 6 piksel per mm dan penghalusan kurva oleh Potrace, bukan dari penjaga jarak.**
  - Penjaga jarak sudah dicoba sama dengan offset penuh. Hasilnya: A4 tetap gagal (−0,08 mm, karena piksel JPG 0,25 mm), dan pengurangan titik turun ke 40–45% sehingga S1 dan S2 gagal.
  - Pilihan:
    - (a) terima toleransi, dan tulis aturannya sebagai "minimal offset − 0,3 mm, galat dari piksel sampai 0,15 mm";
    - (b) gambar bantu 10 piksel per mm, belum diuji: perkiraan beban prosesor ±2,8× dan memori lebih besar untuk stiker besar.
  - Saran: (a).
- **A2b:** target "titik berkurang minimal 60%" dipakai hanya untuk `rough.jpg` (S1), atau dibuat bergantung pada jenis logo?
