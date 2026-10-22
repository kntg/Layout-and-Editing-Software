# Skenario uji: garis potong dengan node sedikit dan kurva mulus

Fitur yang diuji: tuning Potrace (tanpa sudut tajam), penghalusan bentuk lewat slider **Halus**, penjaga jarak ke artwork, dan die yang selalu membungkus kiss. Lihat [PRD.md](PRD.md) F16.

- **Otomatis:** dijalankan oleh `StickerLayout.exe --selfcheck <folder>`. Hasilnya tertulis di `selfcheck.txt` sebagai baris `S1 LULUS ...` atau `S1 GAGAL ...`. Bila satu skenario gagal, selfcheck keluar dengan kode 1.
- **Manual:** dikerjakan operator dengan CorelDRAW, Cutting Master 5, dan mesin Graphtec.
- **Validasi terukur:** jarak garis ke artwork per titik, Halus, die membungkus kiss, opsi garis, nesting, dan rotasi diukur pada file Printech-TestData. Lihat [validasi-garis-potong-nesting.md](../Printech-TestData/02-expected/validasi-garis-potong-nesting.md).
- Pengaturan default: kiss offset 2 mm, halus 1 mm; die offset 4 mm, halus 2 mm.

| ID | Skenario | Kriteria lulus | Cara | Hasil 2026-10-17 |
| --- | --- | --- | --- | --- |
| S1 | Logo JPG bertepi kasar (`rough.jpg`, 40 mm; latar dibuang dulu lewat Rapikan, karena sejak 2026-10-21 latar tidak dibuang otomatis) | Node turun ≥60% dibanding Halus 0; tidak ada sudut tajam | Otomatis | Lulus 2026-10-21: kiss 65 → 16 (−75%), die 42 → 15 (−64%). Sebelumnya (latar putih dibuang otomatis): kiss 66 → 17, die 45 → 16 |
| S2 | Lingkaran 40 mm | Maksimal 4 node; garis tetap bulat (selisih lebar dan tinggi < 0,3 mm); diameter 44 mm ± 0,5 | Otomatis | Lulus: 3 node, 44,03 × 44,02 mm |
| S3 | Teks tebal "PRINTECH" 60 mm | Node lebih sedikit dari Halus 0; tidak ada sudut; huruf tidak terpotong (luas artwork di luar garis < 0,5 mm²) | Otomatis | Lulus: 43 → 20 node, 0,00 mm² |
| S4 | Bintang dengan offset kecil 0,5 mm dan offset 0 (`star.jpg`, latar dibuang lewat Rapikan) | Garis tidak masuk artwork: luas di luar garis < 0,5 mm² (offset 0,5) dan < 3 mm² (offset 0, toleransi piksel) | Otomatis | Lulus 2026-10-21: 0,00 dan 0,25 mm² (sebelumnya 0,00 dan 0,49) |
| S5 | Halus 0 / 0,5 / 1 / 2 / 4 mm pada logo kasar | Semua nilai jalan tanpa error; node selalu lebih sedikit dari Halus 0 | Otomatis | Lulus 2026-10-21: 65 / 29 / 16 / 28 / 44 node (2026-10-18: 66 / 32 / 17 / 29 / 42; sebelumnya 66 / 32 / 16 / 17 / 47, lihat catatan 1) |
| S6 | Kiss selalu di dalam die, termasuk pengaturan tak biasa (kiss halus 3, die halus 0) | Luas kiss di luar die < 1 mm² | Otomatis | Lulus: 0,00 mm² pada 9 desain |
| S7 | Dua bagian artwork berdekatan (celah 3,0–3,9 mm) | Penghalusan tidak memecah satu stiker menjadi dua (jumlah kontur tidak bertambah) | Otomatis | Lulus pada keempat celah |
| S8 | Export PDF lalu dibuka di CorelDRAW | Otomatis: garis potong hanya berisi kurva (0 garis lurus) dan warnanya DeviceCMYK murni (hanya `0 1 0 0 K` dan `1 0 0 0 K`, tanpa operator RGB atau ruang warna lain). Manual: klik tiap garis dengan Shape tool (F10), lihat jumlah node. Harapan untuk lebar 40 mm, nilai default: `circle.png` kiss 3, die 4; `rough.jpg` kiss 16, die 13. Selisih 1 node (titik awal/akhir) masih wajar | Otomatis + manual | Bagian otomatis lulus: 275 kurva, 0 garis lurus, 0 operator RGB. Bagian CorelDRAW belum diuji |
| S9 | Stiker besar 300 mm (gambar 3000 px), kiss + die | Dimuat < 5 detik di build Debug | Otomatis | Lulus: 2,0 detik (Debug), ±1 detik (Release). 2026-10-21: Release 2,1 dan 2,5 detik lulus; Debug 3,9 / 4,1 / 4,9 / 5,3 / 5,4 / 5,8 detik (3 dari 6 gagal) saat CPU PC 42–62% dipakai aplikasi lain. Build HEAD sebelum perubahan hari ini, diukur bergantian di kondisi yang sama: 2,1–7,4 detik, jadi bukan regresi |
| S10 | Potong fisik di Graphtec | Lihat langkah di bawah | Manual | Belum diuji |
| S11 | Cincin "C" (celah 3 mm), 12 titik melingkar + lingkaran tengah, donat lubang Ø 21,5 mm, donat lubang Ø 4,6 mm (40 mm, kiss 2/1, die 4/2) | Rongga dari celah yang tertutup offset dan lubang kecil yang tertutup: 1 kontur. Donat besar: garis dalam kiss dan die (2 kontur), kiss tetap di dalam die | Otomatis | Lulus 2026-10-21: cincin C 1 / 1, titik 1 / 1, donat besar 2 / 2 (kiss di luar die 0,00 mm²), donat kecil 1 / 1. Sebelum aturan donat: lubang asli selalu diisi |
| S12 | Editor garis potong: `circle.png` 40 mm (kiss 2/1, die 4/2) dan donat lubang Ø 21,5 mm. Polygon dari kurva otomatis, garis manual dimuat, titik digeser, lebar 40 → 80 mm, offset kiss 2 → 1 mm, die manual rapat dengan kiss otomatis, export PDF, editor tanpa mouse (tambah, seret, urungkan/ulangi, hapus, kiss keluar die, Simplify, Reset otomatis, hapus garis dalam donat) | Simpangan kurva ke polygon ≤ toleransi Simplify; titik makin sedikit bila Simplify makin besar; path hanya M/L/Z; luas ±2%; grid berubah bila titik digeser; skala ikut lebar; offset tidak mengubah garis manual; kiss tetap di dalam die; PDF berisi garis lurus tanpa kurva, warna CMYK sama; Simpan nonaktif saat kiss keluar die | Otomatis | Lulus 2026-10-21: simpangan 0,125 mm (toleransi 0,2); titik 64 / 32 / 16 (toleransi 0,05 / 0,2 / 0,5); luas selisih 0,9%; grid 1881 → 1887 sel; skala 0,001 mm; kunci offset 0,000 mm; die rapat: kiss di luar die 0,00 mm²; PDF 93 garis lurus, 0 kurva; editor benar, donat 2 → 1 kontur kiss. Seret titik dengan mouse sungguhan belum diuji (lihat uji UI `ui-editor-garis.ps1`) |
| S13 | Smart select: cincin 39,1 mm dengan lubang sempit Ø 2,6 mm, lubang jarum Ø 1,2 mm, takik 5 mm, watermark alpha 38 yang menempel, dan pulau terpisah; gambar opak berlatar putih (kiss 2/1, die 4/2). Lewat mesin dan lewat editor tanpa mouse | Toleransi 64: watermark tidak ikut; takik diikuti (garis otomatis menutupnya); lubang sempit tertutup sampai diklik, klik kedua menghapus; lubang jarum ditolak; die membungkus kiss; Shift+klik menambah pulau; gambar opak memakai Toleransi 32 dan memilih objeknya; editor: urungkan/ulangi, Toleransi menghitung ulang | Otomatis | Lulus 2026-10-22: garis kiss kiri 8,16 mm (Toleransi 64) dan −1,87 mm (16, watermark ikut); garis dalam 0,88 mm, die tanpa garis dalam, kiss di luar die 0,00 mm²; lubang jarum terlalu kecil; gambar opak kiri 8,03 mm; editor 1/2/1/2/3 kontur. Klik kanvas dengan mouse sungguhan belum diuji |

## S10 — langkah potong fisik (operator)

1. Siapkan `rough.jpg`, `circle.png`, dan 2–3 teks/logo customer yang biasa dipotong. File contoh ada di folder `samples/`, atau buat ulang dengan `StickerLayout.exe --selfcheck <folder>`.
2. Di Printech: lebar 40–60 mm, 4 salinan per desain, kertas A3+. Centang kiss dan die, nilai offset dan halus default.
3. Export PDF, buka di CorelDRAW, pasang reg mark dengan cara yang biasa dipakai operator, lalu cetak di Develop.
4. Potong di Graphtec lewat Cutting Master 5. Sebagai pembanding, potong juga desain yang sama dengan garis potong yang biasa dibuat di CorelDRAW.
5. **Lulus bila:**
   - pisau bergerak mulus, tidak tersendat atau berbelok mendadak, terutama di teks dan logo bertepi kasar;
   - tepi potongan halus, tidak bergerigi;
   - garis potong tidak mengenai artwork dan lebar border tampak rata;
   - die tidak memotong stiker kiss;
   - waktu potong tidak lebih lama dari pembanding.
6. Catat hasil per desain. Bila pisau tersendat, catat bentuknya dan nilai Halus yang dipakai.

## Catatan

1. **Halus lebih dari 2 mm tidak menambah kehalusan.** Pada logo kasar 40 mm, Halus 4 mm justru menaikkan node lagi menjadi 47, padahal Halus 1–2 mm hanya 16–17. Dugaan penyebab (belum diukur terpisah): penghalusan besar menarik garis ke dalam, lalu penjaga jarak ke artwork mengembalikan lekukan aslinya. Nilai yang disarankan: Halus 1–2 mm.
2. Skenario S1–S9 membuktikan bentuk garisnya. Apakah gerakan pisau benar-benar mulus hanya bisa dibuktikan lewat S10.
